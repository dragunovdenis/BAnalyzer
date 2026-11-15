//Copyright (c) 2026 Denys Dragunov, dragunovdenis@gmail.com
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files(the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and /or sell
//copies of the Software, and to permit persons to whom the Software is furnished
//to do so, subject to the following conditions :

//The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
//INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
//PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
//HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
//OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
//SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using BAnalyzer.Controllers;
using BAnalyzer.DataStructures;
using BAnalyzer.Utils;
using BAnalyzerCore.DataStructures;
using BAnalyzerCore.TradeSim;
using BAnalyzerCore.Utils;

namespace BAnalyzer.Controls;

/// <summary>
/// Interaction logic for TradeSimControl.xaml
/// </summary>
/// <remarks>
/// Usage: <see cref="Initialize"/> once, then <see cref="Activate"/> while the control is shown
/// and <see cref="Deactivate"/> while it is not (an inactive control does not poll the exchanges).
/// </remarks>
public partial class TradeSimControl : INotifyPropertyChanged
{
    /// <summary>
    /// A row of the holdings grid.
    /// </summary>
    public sealed record HoldingRow(string Coin, string Amount, string Price, string Value);

    /// <summary>
    /// A row of the trade history grid.
    /// </summary>
    public sealed record TradeRow(string Time, string Side, Brush SideBrush, string Price,
        string Coin, string Quote, string Fee, string Exchange);

    /// <summary>
    /// State of the displayed price.
    /// </summary>
    private enum PriceState
    {
        Loading,
        Valid,
        Unavailable,
    }

    /// <summary>
    /// A deposit or a withdrawal.
    /// </summary>
    private delegate bool CashOperation(decimal amount, out string error);

    private const string QuoteAsset = TradeSimulator.QuoteAsset;

    private const int AmountDecimals = TradeSimulator.AmountDecimals;

    private const int FeePercentDecimals = 4;

    private const int PricePollStalenessMs = 500;

    private const int ValuationStalenessMs = 5000;

    private static readonly TimeSpan PriceUpdatePeriod = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan ValuationPeriod = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximal age of the displayed price that can still be traded at.
    /// </summary>
    private static readonly TimeSpan PriceFreshness = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan ExecutionPriceTimeout = TimeSpan.FromSeconds(5);

    private readonly DispatcherTimer _priceTimer;

    private readonly DispatcherTimer _valuationTimer;

    private readonly CoalescingRunner _priceUpdater;

    private readonly CoalescingRunner _valuationUpdater;

    /// <summary>
    /// The last known prices of the held coins; a price is kept until a new one arrives.
    /// </summary>
    private readonly Dictionary<string, decimal> _coinPrices = new();

    private IMultiExchange _client;

    private TradeSimSettings _settings;

    private TradeSimController _controller;

    private bool _isActive;

    private PriceState _priceState = PriceState.Loading;

    private decimal _price;

    private DateTime _priceTimeUtc;

    /// <summary>
    /// "True" while an order is being executed.
    /// </summary>
    private bool _busy;

    /// <summary>
    /// The unit of the field that keeps its value (the other one is derived from it).
    /// </summary>
    private AmountUnit _primary = AmountUnit.Quote;

    /// <summary>
    /// "True" while an amount field is changed by code rather than by the user.
    /// </summary>
    private bool _recalculating;

    /// <summary>
    /// "True" while the holdings grid is being rebuilt (its selection changes are not the user's).
    /// </summary>
    private bool _rebuildingHoldings;

    /// <summary>
    /// Constructor.
    /// </summary>
    public TradeSimControl()
    {
        InitializeComponent();

        // The behaviors go first so that the other handlers never see a text that is about to be reverted.
        NumericTextBoxBehavior.Attach(CoinBox, AmountDecimals);
        NumericTextBoxBehavior.Attach(QuoteBox, AmountDecimals);
        NumericTextBoxBehavior.Attach(CashBox, AmountDecimals);
        NumericTextBoxBehavior.Attach(FeeBox, FeePercentDecimals);

        CoinBox.TextChanged += AmountBox_TextChanged;
        QuoteBox.TextChanged += AmountBox_TextChanged;

        _priceUpdater = new CoalescingRunner(UpdatePriceAsync);
        _valuationUpdater = new CoalescingRunner(UpdateValuationAsync);

        // The timer also refreshes the "stale" state of the price when the requests do not return.
        _priceTimer = new DispatcherTimer { Interval = PriceUpdatePeriod };
        _priceTimer.Tick += (_, _) =>
        {
            UpdatePriceText();
            UpdateTradeState();
            RequestPrice();
        };

        _valuationTimer = new DispatcherTimer { Interval = ValuationPeriod };
        _valuationTimer.Tick += (_, _) => RequestValuation();

        UpdatePrimaryVisuals();
    }

    #region Bindable properties

    private IReadOnlyList<ExchangeId> _exchanges;

    /// <summary>
    /// The exchanges to choose from.
    /// </summary>
    public IReadOnlyList<ExchangeId> Exchanges
    {
        get => _exchanges;
        private set => SetField(ref _exchanges, value);
    }

    private IReadOnlyList<string> _symbols;

    /// <summary>
    /// The symbols to choose from.
    /// </summary>
    public IReadOnlyList<string> Symbols
    {
        get => _symbols;
        private set => SetField(ref _symbols, value);
    }

    private ExchangeId _selectedExchange;

    /// <summary>
    /// The exchange the prices are taken from.
    /// </summary>
    public ExchangeId SelectedExchange
    {
        get => _selectedExchange;
        set
        {
            if (!SetField(ref _selectedExchange, value))
                return;

            _settings.Exchange = value;
            OnMarketChanged();
            // The holdings are priced on the selected exchange first.
            RequestValuation();
        }
    }

    private string _selectedSymbol;

    /// <summary>
    /// The traded symbol.
    /// </summary>
    public string SelectedSymbol
    {
        get => _selectedSymbol;
        set
        {
            // An editable combo-box reports "null" while the user types a text that matches no symbol.
            if (value == null || !SetField(ref _selectedSymbol, value))
                return;

            _settings.Symbol = value;

            // An amount of the old coin means nothing for the new one.
            if (_primary == AmountUnit.Coin)
                ClearAmounts();

            OnMarketChanged();
        }
    }

    private string _priceText = "Price: …";

    /// <summary>
    /// The price label.
    /// </summary>
    public string PriceText
    {
        get => _priceText;
        private set => SetField(ref _priceText, value);
    }

    private string _coinAsset = "?";

    /// <summary>
    /// The traded coin.
    /// </summary>
    public string CoinAsset
    {
        get => _coinAsset;
        private set => SetField(ref _coinAsset, value);
    }

    private string _coinAvailableText;

    /// <summary>
    /// The balance of the traded coin.
    /// </summary>
    public string CoinAvailableText
    {
        get => _coinAvailableText;
        private set => SetField(ref _coinAvailableText, value);
    }

    private string _quoteAvailableText;

    /// <summary>
    /// The USDT balance (next to the USDT amount field).
    /// </summary>
    public string QuoteAvailableText
    {
        get => _quoteAvailableText;
        private set => SetField(ref _quoteAvailableText, value);
    }

    private string _feePreview;

    /// <summary>
    /// The fee rate and what each of the buttons would yield at the displayed price.
    /// </summary>
    public string FeePreview
    {
        get => _feePreview;
        private set => SetField(ref _feePreview, value);
    }

    private bool _canBuy;

    /// <summary>
    /// "True" if the "Buy" button is enabled.
    /// </summary>
    public bool CanBuy
    {
        get => _canBuy;
        private set => SetField(ref _canBuy, value);
    }

    private bool _canSell;

    /// <summary>
    /// "True" if the "Sell" button is enabled.
    /// </summary>
    public bool CanSell
    {
        get => _canSell;
        private set => SetField(ref _canSell, value);
    }

    private string _buyToolTip;

    /// <summary>
    /// Tool-tip of the "Buy" button: why it is disabled.
    /// </summary>
    public string BuyToolTip
    {
        get => _buyToolTip;
        private set => SetField(ref _buyToolTip, value);
    }

    private string _sellToolTip;

    /// <summary>
    /// Tool-tip of the "Sell" button: why it is disabled.
    /// </summary>
    public string SellToolTip
    {
        get => _sellToolTip;
        private set => SetField(ref _sellToolTip, value);
    }

    private string _statusText;

    /// <summary>
    /// Outcome of the last trade.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    private Brush _statusBrush;

    /// <summary>
    /// Color of <see cref="StatusText"/>.
    /// </summary>
    public Brush StatusBrush
    {
        get => _statusBrush;
        private set => SetField(ref _statusBrush, value);
    }

    private string _saveWarning;

    /// <summary>
    /// Warning about a failed save.
    /// </summary>
    public string SaveWarning
    {
        get => _saveWarning;
        private set
        {
            if (SetField(ref _saveWarning, value))
                OnPropertyChanged(nameof(HasSaveWarning));
        }
    }

    /// <summary>
    /// "True" if <see cref="SaveWarning"/> should be shown.
    /// </summary>
    public bool HasSaveWarning => !string.IsNullOrEmpty(SaveWarning);

    private string _usdtBalanceText;

    /// <summary>
    /// The USDT balance (in the account section).
    /// </summary>
    public string UsdtBalanceText
    {
        get => _usdtBalanceText;
        private set => SetField(ref _usdtBalanceText, value);
    }

    /// <summary>
    /// The held coins.
    /// </summary>
    public ObservableCollection<HoldingRow> Holdings { get; } = new();

    private string _totalText;

    /// <summary>
    /// Total value of the portfolio.
    /// </summary>
    public string TotalText
    {
        get => _totalText;
        private set => SetField(ref _totalText, value);
    }

    private string _accountStatusText;

    /// <summary>
    /// Outcome of the last account operation.
    /// </summary>
    public string AccountStatusText
    {
        get => _accountStatusText;
        private set => SetField(ref _accountStatusText, value);
    }

    private Brush _accountStatusBrush;

    /// <summary>
    /// Color of <see cref="AccountStatusText"/>.
    /// </summary>
    public Brush AccountStatusBrush
    {
        get => _accountStatusBrush;
        private set => SetField(ref _accountStatusBrush, value);
    }

    private string _historyHeader = "Trade history";

    /// <summary>
    /// Header of the history panel.
    /// </summary>
    public string HistoryHeader
    {
        get => _historyHeader;
        private set => SetField(ref _historyHeader, value);
    }

    /// <summary>
    /// Trades of the selected coin, newest first.
    /// </summary>
    public ObservableCollection<TradeRow> History { get; } = new();

    private string _historyEmptyText;

    /// <summary>
    /// The text shown instead of an empty history.
    /// </summary>
    public string HistoryEmptyText
    {
        get => _historyEmptyText;
        private set => SetField(ref _historyEmptyText, value);
    }

    private bool _isHistoryEmpty = true;

    /// <summary>
    /// "True" if there are no trades to show.
    /// </summary>
    public bool IsHistoryEmpty
    {
        get => _isHistoryEmpty;
        private set => SetField(ref _isHistoryEmpty, value);
    }

    #endregion

    /// <summary>
    /// The simulation.
    /// </summary>
    private TradeSimulator Sim => _controller.Simulator;

    /// <summary>
    /// Text box of the primary amount.
    /// </summary>
    private TextBox PrimaryBox => _primary == AmountUnit.Coin ? CoinBox : QuoteBox;

    /// <summary>
    /// Text box of the amount derived from the primary one.
    /// </summary>
    private TextBox SecondaryBox => _primary == AmountUnit.Coin ? QuoteBox : CoinBox;

    /// <summary>
    /// "True" if the displayed price is valid but too old to trade at.
    /// </summary>
    private bool IsPriceStale => _priceState == PriceState.Valid && DateTime.UtcNow - _priceTimeUtc > PriceFreshness;

    /// <summary>
    /// Binds the control to the given simulation; must be called once, before the control is activated.
    /// </summary>
    public void Initialize(IMultiExchange client, TradeSimSettings settings, TradeSimController controller)
    {
        if (_controller != null)
            throw new InvalidOperationException("The control is already initialized.");

        // The setters of the selected market rely on these.
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));

        Exchanges = client.Exchanges;
        Symbols = client.Symbols;

        SelectedExchange = Exchanges.Contains(settings.Exchange) ? settings.Exchange : Exchanges.FirstOrDefault();
        SelectedSymbol = Symbols.Contains(settings.Symbol) ? settings.Symbol :
            Symbols.Contains(TradeSimSettings.DefaultSymbol) ? TradeSimSettings.DefaultSymbol : Symbols.FirstOrDefault();

        // The setters do nothing if the values have not changed.
        settings.Exchange = SelectedExchange;
        settings.Symbol = SelectedSymbol;

        FeeBox.Text = FormatFeePercent(Sim.FeeRate);
    }

    /// <summary>
    /// Starts showing the simulation and polling the prices.
    /// </summary>
    public void Activate()
    {
        if (_isActive)
            return;

        _isActive = true;
        _controller.Updated += Controller_Updated;

        // The simulation might have changed since the deactivation and the price shown then is outdated.
        RefreshAccount();
        OnMarketChanged();
        RequestValuation();

        _priceTimer.Start();
        _valuationTimer.Start();

        var warning = _controller.TakeLoadWarning();

        if (warning != null)
            Dispatcher.BeginInvoke(() => MessageBox.Show(Window.GetWindow(this), warning, "Trade Sim",
                MessageBoxButton.OK, MessageBoxImage.Warning));
    }

    /// <summary>
    /// Stops polling the prices and following the simulation.
    /// </summary>
    public void Deactivate()
    {
        if (!_isActive)
            return;

        _isActive = false;
        _priceTimer.Stop();
        _valuationTimer.Stop();
        _controller.Updated -= Controller_Updated;

        // Every change is saved immediately; this only retries a save that has failed.
        _controller.Flush();
    }

    #region Price

    /// <summary>
    /// Handles a change of the exchange or of the symbol.
    /// </summary>
    private void OnMarketChanged()
    {
        _priceState = PriceState.Loading;
        _price = 0m;

        UpdatePriceText();
        RefreshCoin();
        Recalculate();
        RequestPrice();
    }

    /// <summary>
    /// Requests an update of the displayed price (if the control is active).
    /// </summary>
    private void RequestPrice()
    {
        if (_isActive)
            _ = _priceUpdater.RunAsync();
    }

    /// <summary>
    /// Fetches and shows the price of the selected market.
    /// </summary>
    private async Task UpdatePriceAsync()
    {
        if (!_isActive)
            return;

        var exchange = SelectedExchange;
        var symbol = SelectedSymbol;
        var data = await FetchPriceAsync(exchange, symbol, PricePollStalenessMs);

        // A price of the market selected before is of no use (another update is requested already).
        if (exchange == SelectedExchange && symbol == SelectedSymbol)
            ApplyPrice(data);
    }

    /// <summary>
    /// Returns the price of the given symbol on the given exchange or "null" if it is not available.
    /// </summary>
    private async Task<IPriceData> FetchPriceAsync(ExchangeId exchange, string symbol, int acceptableStalenessMs)
    {
        var client = _client[exchange];

        if (client == null || string.IsNullOrEmpty(symbol))
            return null;

        try
        {
            return await Task.Run(() => client.GetPriceAsync(symbol, acceptableStalenessMs));
        }
        catch (Exception)
        {
            // A failed request means just that there is no price at the moment.
            return null;
        }
    }

    /// <summary>
    /// Shows the given price of the selected market; "null" (no price) keeps the last valid price,
    /// which becomes stale if no new one arrives.
    /// </summary>
    private void ApplyPrice(IPriceData data)
    {
        if (data != null && TradeSimulator.TryToDecimalPrice(data.Price, out var price))
        {
            _price = price;
            _priceTimeUtc = data.TimeStamp;
            _priceState = PriceState.Valid;
        }
        else if (_priceState != PriceState.Valid)
            _priceState = PriceState.Unavailable;

        UpdatePriceText();
        Recalculate();
    }

    /// <summary>
    /// Updates the price label.
    /// </summary>
    private void UpdatePriceText() => PriceText = _priceState switch
    {
        PriceState.Loading => "Price: …",
        PriceState.Unavailable => $"Price: N/A on {SelectedExchange}",
        _ => $"Price: {FormatPrice(_price)} {QuoteAsset}" + (IsPriceStale ? " (stale)" : string.Empty),
    };

    #endregion

    #region Amounts

    /// <summary>
    /// Makes the given field primary.
    /// </summary>
    private void SetPrimary(AmountUnit unit)
    {
        if (_primary == unit)
            return;

        _primary = unit;
        UpdatePrimaryVisuals();
        Recalculate();
    }

    /// <summary>
    /// Highlights the primary field.
    /// </summary>
    private void UpdatePrimaryVisuals()
    {
        CoinBox.BorderThickness = new Thickness(_primary == AmountUnit.Coin ? 2 : 1);
        QuoteBox.BorderThickness = new Thickness(_primary == AmountUnit.Quote ? 2 : 1);

        const string primaryHint = "This amount is kept fixed, the other one follows the price";
        CoinBox.ToolTip = _primary == AmountUnit.Coin ? primaryHint : null;
        QuoteBox.ToolTip = _primary == AmountUnit.Quote ? primaryHint : null;
    }

    /// <summary>
    /// Coin field focus handler: the focused field becomes primary.
    /// </summary>
    private void CoinBox_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        SetPrimary(AmountUnit.Coin);

    /// <summary>
    /// USDT field focus handler: the focused field becomes primary.
    /// </summary>
    private void QuoteBox_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        SetPrimary(AmountUnit.Quote);

    /// <summary>
    /// Text change handler of both amount fields.
    /// </summary>
    private void AmountBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_recalculating)
            Recalculate();
    }

    /// <summary>
    /// Recalculates the secondary amount from the primary one and the displayed price
    /// (and, consequently, the state of the buttons).
    /// </summary>
    private void Recalculate()
    {
        var amount = NumericTextBoxBehavior.Parse(PrimaryBox.Text);
        var other = amount is > 0 && _priceState == PriceState.Valid
            ? TradeSimulator.DeriveOtherAmount(amount.Value, _primary, _price) : null;

        SetTextSilently(SecondaryBox, other != null ? FormatAmount(other.Value) : string.Empty);
        UpdateTradeState();
    }

    /// <summary>
    /// Sets the text of an amount field without triggering the recalculation.
    /// </summary>
    private void SetTextSilently(TextBox box, string text)
    {
        if (box.Text == text)
            return;

        _recalculating = true;

        try
        {
            box.Text = text;
        }
        finally
        {
            _recalculating = false;
        }
    }

    /// <summary>
    /// Clears both amount fields.
    /// </summary>
    private void ClearAmounts()
    {
        SetTextSilently(CoinBox, string.Empty);
        SetTextSilently(QuoteBox, string.Empty);
        UpdateTradeState();
    }

    /// <summary>
    /// Puts the given balance into the given field and makes it primary.
    /// </summary>
    private void UseBalance(TextBox box, AmountUnit unit, decimal balance)
    {
        box.Focus();
        SetPrimary(unit);
        box.Text = balance > 0 ? FormatAmount(balance) : string.Empty;
        box.CaretIndex = box.Text.Length;
    }

    /// <summary>
    /// "Available" coin click handler.
    /// </summary>
    private void CoinAvailable_OnClick(object sender, RoutedEventArgs e) =>
        UseBalance(CoinBox, AmountUnit.Coin, Sim.GetBalance(CoinAssetOrNull()));

    /// <summary>
    /// "Available" USDT click handler.
    /// </summary>
    private void QuoteAvailable_OnClick(object sender, RoutedEventArgs e) =>
        UseBalance(QuoteBox, AmountUnit.Quote, Sim.GetBalance(QuoteAsset));

    #endregion

    #region Trading

    /// <summary>
    /// Returns the traded coin or "null" if the selected symbol is not a USDT pair.
    /// </summary>
    private string CoinAssetOrNull() => TradeSimulator.TryGetAsset(SelectedSymbol, out var asset) ? asset : null;

    /// <summary>
    /// Returns an order of the given side for the amount in the primary field or "null" if there is no amount.
    /// </summary>
    private TradeOrder CreateOrder(TradeSide side) => NumericTextBoxBehavior.Parse(PrimaryBox.Text) is > 0 and var amount
        ? new TradeOrder(SelectedExchange.ToString(), SelectedSymbol, side, amount, _primary)
        : null;

    /// <summary>
    /// Updates the state of the buttons and the fee preview.
    /// </summary>
    private void UpdateTradeState()
    {
        var buyOrder = CreateOrder(TradeSide.Buy);

        var commonReason = _busy ? "Executing…" :
            CoinAssetOrNull() == null ? "Select a USDT-quoted symbol." :
            _priceState == PriceState.Loading ? "Waiting for the price…" :
            _priceState == PriceState.Unavailable ? $"There is no price on {SelectedExchange}." :
            IsPriceStale ? "The price is stale." :
            buyOrder == null ? "Enter an amount." : null;

        // The quotes are shown even while the price is stale, as an estimate.
        var canQuote = buyOrder != null && _priceState == PriceState.Valid && CoinAssetOrNull() != null;
        var buy = canQuote ? Sim.Quote(buyOrder, _price) : null;
        var sell = canQuote ? Sim.Quote(buyOrder with { Side = TradeSide.Sell }, _price) : null;

        BuyToolTip = commonReason ?? (buy.IsValid ? null : buy.Message);
        SellToolTip = commonReason ?? (sell.IsValid ? null : sell.Message);
        CanBuy = BuyToolTip == null;
        CanSell = SellToolTip == null;

        var feeText = $"Fee {FormatFeePercent(Sim.FeeRate)} %";
        FeePreview = canQuote ? $"{feeText}:   Buy → {DescribeYield(buy)}   ·   Sell → {DescribeYield(sell)}" : feeText;
    }

    /// <summary>
    /// Describes what the user would receive by executing the given quote (or why it is not possible).
    /// </summary>
    private static string DescribeYield(TradeQuote quote) => quote.Rejection switch
    {
        TradeRejection.None => $"you receive {FormatAmount(quote.NetReceived, true)} {quote.ReceivedAsset}",
        TradeRejection.InsufficientFunds => "insufficient " +
                                            (quote.Order.Side == TradeSide.Buy ? QuoteAsset : quote.Asset),
        _ => quote.Message,
    };

    /// <summary>
    /// Sets the "busy" state.
    /// </summary>
    private void SetBusy(bool busy)
    {
        _busy = busy;
        UpdateTradeState();
    }

    /// <summary>
    /// "Buy" click handler.
    /// </summary>
    private async void Buy_OnClick(object sender, RoutedEventArgs e) => await ExecuteAsync(TradeSide.Buy);

    /// <summary>
    /// "Sell" click handler.
    /// </summary>
    private async void Sell_OnClick(object sender, RoutedEventArgs e) => await ExecuteAsync(TradeSide.Sell);

    /// <summary>
    /// Executes a market order of the given side for the entered amount at a freshly fetched price,
    /// provided it is close enough to the displayed one.
    /// </summary>
    private async Task ExecuteAsync(TradeSide side)
    {
        var order = CreateOrder(side);

        if (_busy || order == null || _priceState != PriceState.Valid || IsPriceStale)
            return;

        var exchange = SelectedExchange;
        var symbol = SelectedSymbol;
        var referencePrice = _price;

        SetBusy(true);
        SetStatus("Executing…", null);

        try
        {
            var data = await FetchExecutionPriceAsync(exchange, symbol);

            if (data == null || !TradeSimulator.TryToDecimalPrice(data.Price, out var executionPrice))
            {
                SetStatus($"✖ Could not get the current price from {exchange}; nothing was traded.", Brushes.Red);
                return;
            }

            var result = Sim.Execute(order, executionPrice, referencePrice,
                TradeSimulator.DefaultPriceTolerance, DateTime.UtcNow);

            // The user may have changed the market or the amount while the price was being fetched.
            var orderUnchanged = CreateOrder(side) == order;

            if (result.Success)
            {
                if (orderUnchanged)
                    ClearAmounts();

                SetStatus(DescribeTrade(result.Trade), Brushes.Green);
                return;
            }

            // The next click must be judged against the price the user can see.
            if (result.Rejection == TradeRejection.PriceMoved && exchange == SelectedExchange && symbol == SelectedSymbol)
                ApplyPrice(data);

            SetStatus("✖ " + result.Message, Brushes.Red);
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// Fetches the price bypassing the cache; returns "null" if it does not arrive in time.
    /// </summary>
    private async Task<IPriceData> FetchExecutionPriceAsync(ExchangeId exchange, string symbol)
    {
        var fetch = FetchPriceAsync(exchange, symbol, 0);
        var completed = await Task.WhenAny(fetch, Task.Delay(ExecutionPriceTimeout));

        return completed == fetch ? await fetch : null;
    }

    /// <summary>
    /// Describes the given executed trade.
    /// </summary>
    private static string DescribeTrade(TradeRecord trade)
    {
        var price = FormatPrice(trade.Price);
        var fee = $"{FormatAmount(trade.Fee, true)} {trade.FeeAsset}";

        return trade.Side == TradeSide.Buy
            ? $"✔ Bought {FormatAmount(trade.CoinAmount - trade.Fee, true)} {trade.Asset} for " +
              $"{FormatAmount(trade.QuoteAmount, true)} {QuoteAsset} at {price} (fee {fee})"
            : $"✔ Sold {FormatAmount(trade.CoinAmount, true)} {trade.Asset} for " +
              $"{FormatAmount(trade.QuoteAmount - trade.Fee, true)} {QuoteAsset} at {price} (fee {fee})";
    }

    /// <summary>
    /// Shows the given message in the trade status line ("null" brush means the default color).
    /// </summary>
    private void SetStatus(string text, Brush brush)
    {
        StatusText = text;
        StatusBrush = brush ?? DefaultBrush;
    }

    #endregion

    #region Account

    /// <summary>
    /// Handles changes of the simulation.
    /// </summary>
    private void Controller_Updated(object sender, EventArgs e)
    {
        RefreshAccount();
        // A coin might have been bought; the valuation is mostly served from the cache anyway.
        RequestValuation();
    }

    /// <summary>
    /// Refreshes everything that depends on the state of the simulation.
    /// </summary>
    private void RefreshAccount()
    {
        var cash = FormatAmount(Sim.GetBalance(QuoteAsset), true);
        UsdtBalanceText = cash;
        QuoteAvailableText = $"Available: {cash} {QuoteAsset}";

        var error = _controller.LastSaveError;
        SaveWarning = error == null ? null :
            $"⚠ Could not save the simulation: {error} It will be retried on the next change.";

        if (!FeeBox.IsKeyboardFocusWithin)
            FeeBox.Text = FormatFeePercent(Sim.FeeRate);

        RefreshHoldings();
        RefreshCoin();
        UpdateTradeState();
    }

    /// <summary>
    /// Refreshes everything that depends on the selected coin.
    /// </summary>
    private void RefreshCoin()
    {
        var asset = CoinAssetOrNull();
        CoinAsset = asset ?? "?";
        HistoryCoinColumn.Header = CoinAsset;
        History.Clear();

        if (asset == null)
        {
            CoinAvailableText = null;
            HistoryHeader = "Trade history";
            HistoryEmptyText = "Select a USDT-quoted symbol.";
            IsHistoryEmpty = true;
            return;
        }

        CoinAvailableText = $"Available: {FormatAmount(Sim.GetBalance(asset), true)} {asset}";
        HistoryHeader = $"{asset} trade history";
        HistoryEmptyText = $"No {asset} trades yet.";

        foreach (var trade in Sim.GetTrades(asset))
            History.Add(new TradeRow(
                trade.TimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                trade.Side.ToString(), trade.Side == TradeSide.Buy ? Brushes.Green : Brushes.Red,
                FormatPrice(trade.Price), FormatAmount(trade.CoinAmount, true), FormatAmount(trade.QuoteAmount, true),
                $"{FormatAmount(trade.Fee, true)} {trade.FeeAsset}", trade.Exchange));

        IsHistoryEmpty = History.Count == 0;
    }

    /// <summary>
    /// Rebuilds the holdings grid and the total value from the last known prices.
    /// </summary>
    private void RefreshHoldings()
    {
        var valuation = Sim.Valuate(_coinPrices);

        _rebuildingHoldings = true;

        try
        {
            Holdings.Clear();

            foreach (var h in valuation.Holdings)
                Holdings.Add(new HoldingRow(h.Asset, FormatAmount(h.Amount, true),
                    h.Price != null ? FormatPrice(h.Price.Value) : "N/A",
                    h.Value != null ? FormatValue(h.Value.Value) : "N/A"));
        }
        finally
        {
            _rebuildingHoldings = false;
        }

        TotalText = valuation.IsComplete
            ? $"Total value: {FormatValue(valuation.Total)} {QuoteAsset}"
            : $"Total value: ≥ {FormatValue(valuation.Total)} {QuoteAsset} (some prices unavailable)";
    }

    /// <summary>
    /// Requests an update of the prices of the held coins (if the control is active).
    /// </summary>
    private void RequestValuation()
    {
        if (_isActive)
            _ = _valuationUpdater.RunAsync();
    }

    /// <summary>
    /// Prices all the held coins (on the selected exchange if possible, otherwise on any other one)
    /// and refreshes the holdings.
    /// </summary>
    private async Task UpdateValuationAsync()
    {
        if (!_isActive)
            return;

        var selected = SelectedExchange;
        var exchanges = _client.Exchanges.OrderBy(x => x != selected).ToArray();
        var assets = Sim.Balances.Keys.Where(x => x != QuoteAsset).ToArray();

        var prices = await Task.WhenAll(assets.Select(x => PriceAssetAsync(x + QuoteAsset, exchanges)));

        // A failed request keeps the last known price; the prices of the coins not held anymore are dropped.
        foreach (var asset in _coinPrices.Keys.Except(assets).ToArray())
            _coinPrices.Remove(asset);

        for (var i = 0; i < assets.Length; i++)
            if (prices[i] != null)
                _coinPrices[assets[i]] = prices[i].Value;

        RefreshHoldings();
    }

    /// <summary>
    /// Returns the price of the given symbol on the first of the given exchanges that has it or "null".
    /// </summary>
    private async Task<decimal?> PriceAssetAsync(string symbol, IEnumerable<ExchangeId> exchanges)
    {
        foreach (var exchange in exchanges)
        {
            var data = await FetchPriceAsync(exchange, symbol, ValuationStalenessMs);

            if (data != null && TradeSimulator.TryToDecimalPrice(data.Price, out var price))
                return price;
        }

        return null;
    }

    /// <summary>
    /// Holdings grid selection handler: selects the clicked coin for trading.
    /// </summary>
    private void HoldingsGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_rebuildingHoldings || e.AddedItems.Count == 0 || e.AddedItems[0] is not HoldingRow row)
            return;

        var symbol = row.Coin + QuoteAsset;

        if (Symbols.Contains(symbol))
            SelectedSymbol = symbol;
    }

    /// <summary>
    /// "Deposit" click handler.
    /// </summary>
    private void Deposit_OnClick(object sender, RoutedEventArgs e) => ChangeCash(Sim.Deposit, "Deposited");

    /// <summary>
    /// "Withdraw" click handler.
    /// </summary>
    private void Withdraw_OnClick(object sender, RoutedEventArgs e) => ChangeCash(Sim.Withdraw, "Withdrew");

    /// <summary>
    /// Applies the given operation to the amount entered into the cash field.
    /// </summary>
    private void ChangeCash(CashOperation operation, string verb)
    {
        var amount = NumericTextBoxBehavior.Parse(CashBox.Text);

        if (amount is not > 0)
            SetAccountStatus("✖ Enter a positive amount of USDT.", Brushes.Red);
        else if (!operation(amount.Value, out var error))
            SetAccountStatus("✖ " + error, Brushes.Red);
        else
        {
            CashBox.Text = string.Empty;
            SetAccountStatus($"✔ {verb} {FormatAmount(amount.Value, true)} {QuoteAsset}.", Brushes.Green);
        }
    }

    /// <summary>
    /// Fee field key handler: "Enter" applies the fee.
    /// </summary>
    private void FeeBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        ApplyFee();
        e.Handled = true;
    }

    /// <summary>
    /// Fee field focus loss handler: applies the fee.
    /// </summary>
    private void FeeBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => ApplyFee();

    /// <summary>
    /// Applies the fee entered into the fee field (an invalid one is replaced by the current fee).
    /// </summary>
    private void ApplyFee()
    {
        var percent = NumericTextBoxBehavior.Parse(FeeBox.Text);
        var previous = Sim.FeeRate;

        if (percent == null)
            SetAccountStatus("✖ Enter the fee in percent, e.g. 0.1.", Brushes.Red);
        else if (!Sim.SetFeeRate(percent.Value / 100, out var error))
            SetAccountStatus("✖ " + error, Brushes.Red);
        else if (Sim.FeeRate != previous)
            SetAccountStatus($"✔ The fee is now {FormatFeePercent(Sim.FeeRate)} % (applies to new trades).",
                Brushes.Green);

        FeeBox.Text = FormatFeePercent(Sim.FeeRate);
    }

    /// <summary>
    /// "Reset" click handler.
    /// </summary>
    private void Reset_OnClick(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(Window.GetWindow(this),
            "Reset the simulation? All balances and the trade history will be deleted. This cannot be undone.",
            "Trade Sim", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        if (answer != MessageBoxResult.Yes)
            return;

        ClearAmounts();
        Sim.Reset();
        SetStatus(null, null);
        SetAccountStatus("✔ The simulation has been reset.", Brushes.Green);
    }

    /// <summary>
    /// Shows the given message in the account status line ("null" brush means the default color).
    /// </summary>
    private void SetAccountStatus(string text, Brush brush)
    {
        AccountStatusText = text;
        AccountStatusBrush = brush ?? DefaultBrush;
    }

    #endregion

    #region Formatting

    /// <summary>
    /// The default text color.
    /// </summary>
    private Brush DefaultBrush => TryFindResource("ForegroundColor") as Brush ?? Brushes.Gray;

    /// <summary>
    /// Formats a price.
    /// </summary>
    private static string FormatPrice(decimal price) =>
        DataFormatter.FormatPrice((double)price, CultureInfo.InvariantCulture).Trim();

    /// <summary>
    /// Formats an amount (all the significant decimals).
    /// </summary>
    private static string FormatAmount(decimal value, bool grouping = false) =>
        DataFormatter.FormatAmount(value, AmountDecimals, grouping);

    /// <summary>
    /// Formats a value in USDT (cents).
    /// </summary>
    private static string FormatValue(decimal value) => DataFormatter.FormatAmount(value, 2, true);

    /// <summary>
    /// Formats the given fee rate as percent (without the "%" sign).
    /// </summary>
    private static string FormatFeePercent(decimal rate) =>
        DataFormatter.FormatAmount(rate * 100, FeePercentDecimals);

    #endregion

    #region INotifyPropertyChanged

    /// <inheritdoc/>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Raises the "property changed" event.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Sets the field and raises the "property changed" event if the value has changed.
    /// </summary>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}
