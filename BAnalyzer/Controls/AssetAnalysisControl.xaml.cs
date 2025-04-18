//Copyright (c) 2025 Denys Dragunov, dragunovdenis@gmail.com
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
using System.Runtime.CompilerServices;
using BAnalyzer.DataStructures;
using Binance.Net.Enums;
using BAnalyzer.Controllers;
using System.Windows;
using BAnalyzer.Utils;
using ScottPlot;

namespace BAnalyzer.Controls;

/// <summary>
/// Interaction logic for AssetAnalysisControl.xaml
/// </summary>
public partial class AssetAnalysisControl : INotifyPropertyChanged, IDisposable
{
    private BAnalyzerCore.Binance _client = null;

    private Timer _updateTimer;

    private ObservableCollection<string> _symbols = null!;

    /// <summary>
    /// Collection of available symbols
    /// </summary>
    public ObservableCollection<string> Symbols
    {
        get => _symbols;
        private set => SetField(ref _symbols, value);
    }

    private ObservableCollection<AssetRecord> _assets = new ();

    /// <summary>
    /// Collection of assets.
    /// </summary>
    public ObservableCollection<AssetRecord> Assets
    {
        get => _assets;
        private set => SetField(ref _assets, value);
    }

    private ObservableCollection<KlineInterval> _availableTimeIntervals = null!;

    /// <summary>
    /// Collection of available time intervals.
    /// </summary>
    public ObservableCollection<KlineInterval> AvailableTimeIntervals
    {
        get => _availableTimeIntervals;
        private set => SetField(ref _availableTimeIntervals, value);
    }

    /// <summary>
    /// The corresponding dependency property.
    /// </summary>
    public static readonly DependencyProperty DarkModeProperty =
        DependencyProperty.Register(
            name: nameof(DarkMode),
            propertyType: typeof(bool),
            ownerType: typeof(AssetAnalysisControl),
            typeMetadata: new FrameworkPropertyMetadata(defaultValue: false));

    /// <summary>
    /// Switches between white/black color modes.
    /// </summary>
    public bool DarkMode
    {
        get => (bool)GetValue(DarkModeProperty);
        set => SetValue(DarkModeProperty, value);
    }

    /// <summary>
    /// Currently selected time interval.
    /// </summary>
    public KlineInterval TimeDiscretization
    {
        get => _settings.TimeDiscretization;
        set => _settings.TimeDiscretization = value;
    }

    private string _price = "";

    /// <summary>
    /// Current price
    /// </summary>
    public string Price
    {
        get => _price;
        private set => SetField(ref _price, value);
    }

    private ExchangeSettings _settings;

    /// <summary>
    /// Settings.
    /// </summary>
    public ExchangeSettings Settings
    {
        get => _settings;

        private set
        {
            if (_settings != value)
            {
                if (_settings != null)
                    _settings.PropertyChanged -= Settings_PropertyChanged;

                _settings = value;

                if (_settings != null)
                    _settings.PropertyChanged += Settings_PropertyChanged;

                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Handles property changed events of the settings.
    /// </summary>
    private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e) =>
        Task.Run(async () => await UpdateChartAsync(force: false));

    /// <summary>
    /// Constructor.
    /// </summary>
    public AssetAnalysisControl() => InitializeComponent();

    /// <summary>
    /// Activates the control.
    /// </summary>
    public void Activate(BAnalyzerCore.Binance client, IList<string> exchangeSymbols,
        ObservableCollection<AssetRecord> assets, ExchangeSettings settings, IChartSynchronizationController syncController)
    {
        Settings = settings;
        _client = client;

        Chart.RegisterToSynchronizationController(syncController);
        AvailableTimeIntervals =
            new ObservableCollection<KlineInterval>(Enum.GetValues(typeof(KlineInterval)).Cast<KlineInterval>().
                Where(x => x != KlineInterval.OneSecond));

        Assets.CollectionChanged += Assets_CollectionChanged;
        Assets = assets;

        AssetManager.PropertyChanged += AssetManager_PropertyChanged;

        Symbols = new ObservableCollection<string>(exchangeSymbols);
        Chart.PropertyChanged += ChartOnPropertyChanged;

        _updateTimer = new Timer(async _ => await UpdateChartAsync(force: true),
            new AutoResetEvent(false), 1000, 1000);
    }

    /// <summary>
    /// Handles change of properties of the "asset manager".
    /// </summary>
    private void AssetManager_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is AssetManagerControl control && e.PropertyName == nameof(control.Assets))
            Task.Run(async () => await UpdateChartAsync(force: false));
    }

    /// <summary>
    /// Handles change of chart properties.
    /// </summary>
    private void ChartOnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is ExchangeChartControl chart && e.PropertyName == nameof(chart.TimeFrameEndLocalTime))
            Task.Run(async () => await UpdateChartAsync(force: false));
    }

    /// <summary>
    /// Handles change of the collection of assets.
    /// </summary>
    private void Assets_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        Task.Run(async () => await UpdateChartAsync(force: false));

    /// <summary>
    /// Visualizes the given sticks-and-price data. Must be called in UI thread.
    /// </summary>
    private void VisualizeSticksAndPrice(ChartData chartData)
    {
        if (chartData == null || !chartData.IsValid())
        {
            Chart.UpdatePlots(null);
            Price = "N/A";
            return;
        }

        Chart.UpdatePlots(chartData);

        Price = $"Value: {chartData.Price,7:F5} USDT";
    }

    /// <summary>
    /// Returns the given <param name="valueStick"/> with the values
    /// of its "open" and "close" fields incremented by the value of
    /// the given <param name="asset"/> evaluated at the price suggested
    /// by the given <param name="priceStick"/>.
    /// </summary>
    private static OHLC Add(OHLC valueStick, OHLC priceStick, AssetRecord asset)
    {
        if (priceStick.DateTime != valueStick.DateTime)
            throw new InvalidOperationException("Sticks must correspond to the same time-frame.");

        return AdjustHighLow(valueStick with
        {
            Open = valueStick.Open + asset.Value(priceStick.Open),
            Close = valueStick.Close + asset.Value(priceStick.Close),
        });
    }

    /// <summary>
    /// Returns the given <param name="valueStick"/> with its "high" and "low" field values
    /// adjusted according to the "open" and "close" field values.
    /// </summary>
    private static OHLC AdjustHighLow(OHLC valueStick)
    {
        var maxValue = Math.Max(valueStick.Open, valueStick.Close);
        var minValue = Math.Min(valueStick.Open, valueStick.Close);

        return valueStick with { High = maxValue, Low = minValue };
    }

    /// <summary>
    /// Returns value of the given <param name="asset"/> evaluated
    /// at the price suggested by the given <param name="priceStick"/>.
    /// </summary>
    private static OHLC ToValue(OHLC priceStick, AssetRecord asset)
    {
        var result = new OHLC()
        {
            DateTime = priceStick.DateTime,
            TimeSpan = priceStick.TimeSpan,
            Open = asset.Value(priceStick.Open),
            Close = asset.Value(priceStick.Close),
        };

        return AdjustHighLow(result);
    }

    /// <summary>
    /// Appends value of the given asset to the given collection of value sticks.
    /// </summary>
    private static OHLC[] Append(OHLC[] value, OHLC[] price, AssetRecord asset)
    {
        var resultLength = Math.Max(value.Length, price.Length);

        var result = new OHLC[resultLength];

        var minLength = Math.Min(value.Length, price.Length);

        for (var itemId = 0; itemId < minLength; itemId++)
            result[itemId] = Add(value[itemId], price[itemId], asset);

        for (var itemId = minLength; itemId < resultLength; itemId++)
            result[itemId] = ToValue(price[itemId], asset);

        return result;
    }

    /// <summary>
    /// Retrieves the sticks-and-price data for the given time interval.
    /// </summary>
    private static async Task<ChartData> RetrieveSticksAndPrice(UpdateRequest request, BAnalyzerCore.Binance client)
    {
        var timeFrame = request.TimeFrame;
        var assets = request.Assets;

        if (timeFrame == null || timeFrame.Discretization == default ||
            assets.Count == 0 || client == null || !request.IsRequestStillRelevant())
            return null;

        var frameDuration = timeFrame.Duration;
        var frameBegin = timeFrame.Begin.Subtract(frameDuration);
        var frameEnd = timeFrame.End.Add(frameDuration);

        var valueSticks = Array.Empty<OHLC>();
        var currentTotalValue = 0.0;

        foreach (var asset in assets)
        {
            if (!asset.Selected)
                continue;

            var symbol = asset.Symbol;

            var priceSticks = (await client.GetCandleSticksAsync(frameBegin, frameEnd,
                timeFrame.Discretization, symbol)).Select(x => x.ToScottPlotCandleStick()).Reverse().ToArray();

            if (priceSticks.Length == 0)
                continue;

            valueSticks = valueSticks == null ? priceSticks.Select(x => ToValue(x, asset)).ToArray() :
                Append(valueSticks, priceSticks, asset);

            if (!request.IsRequestStillRelevant())
                return null;

            var price = await client.GetCurrentPrice(symbol);

            currentTotalValue += price != null ? asset.Value((double)price.Price) : double.NaN;

            if (!request.IsRequestStillRelevant())
                return null;
        }

        valueSticks = valueSticks.Reverse().ToArray();

        return new ChartData(valueSticks, null,
            currentTotalValue, request.UpdateRequestId, [], [],
            0, frameDuration.TotalDays);
    }

    private readonly UpdateController _updateController = new();

    /// <summary>
    /// All the data needed to ensure update of chart data.
    /// </summary>
    private class UpdateRequest(TimeFrame timeFrame, IList<AssetRecord> assets,
        int updateRequestId, bool force, IUpdateControllerReadOnly updateController)
    {
        /// <summary>
        /// Returns "true" if the request should be processed further on.
        /// </summary>
        public bool IsRequestStillRelevant() => Force || updateController.IsRequestStillRelevant(UpdateRequestId);

        /// <summary>
        /// Time frame of the update.
        /// </summary>
        public TimeFrame TimeFrame { get; } = timeFrame;

        /// <summary>
        /// Collection of descriptor of the exchange (also known as "symbol").
        /// </summary>
        public IList<AssetRecord> Assets { get; } = assets;

        /// <summary>
        /// Request ID of the update to ensure that updates are applied in a chronological order.
        /// </summary>
        public int UpdateRequestId { get; } = updateRequestId;

        /// <summary>
        /// Determines whether the request should be applied despite,
        /// for example, the system being overloaded by other requests.
        /// </summary>
        private bool Force { get; } = force;
    }

    /// <summary>
    /// Builds update request according to the current "situation".
    /// </summary>
    private UpdateRequest BuildRequest(bool force) =>
        new(new TimeFrame(TimeDiscretization, Settings.StickRange, DateTimeUtils.LocalToUtcOad(Chart.TimeFrameEndLocalTime)),
        Assets.ToArray(), _updateController.IssueNewRequest(), force, _updateController);

    /// <summary>
    /// Method to update chart
    /// </summary>
    private async Task UpdateChartAsync(bool force)
    {
        try
        {
            var (updateRequest, client) = Dispatcher.Invoke(() => (BuildRequest(force), _client));

            var sticksAndPrice = await RetrieveSticksAndPrice(updateRequest, client);

            Dispatcher.Invoke(() =>
            {
                if (sticksAndPrice == null || !_updateController.TryApplyRequest(sticksAndPrice.UpdateRequestId))
                    return;

                VisualizeSticksAndPrice(sticksAndPrice);
            });
        }
        catch (TaskCanceledException) { /*Ignore*/ }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Property changed handler.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Field setter.
    /// </summary>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        _updateTimer?.Dispose();
        _updateTimer = null;
    }
}