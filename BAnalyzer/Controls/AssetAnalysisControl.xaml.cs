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

using BAnalyzer.Controllers;
using BAnalyzer.DataStructures;
using BAnalyzer.Interfaces;
using BAnalyzer.Utils;
using BAnalyzerCore;
using ScottPlot;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using BAnalyzerCore.Clients;
using BAnalyzerCore.DataStructures;

namespace BAnalyzer.Controls;

/// <summary>
/// Interaction logic for AssetAnalysisControl.xaml
/// </summary>
public partial class AssetAnalysisControl : INotifyPropertyChanged,
    IDisposable, ISynchronizableExchangeControl
{
    private IMultiExchange _client = null;

    private DispatcherTimer _updateTimer;

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

    private ObservableCollection<TimeGranularity> _availableTimeIntervals = null!;

    /// <summary>
    /// Collection of available time intervals.
    /// </summary>
    public ObservableCollection<TimeGranularity> AvailableTimeIntervals
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
    public TimeGranularity TimeDiscretization
    {
        get => _settings.TimeDiscretization;
        set => _settings.TimeDiscretization = value;
    }

    private double _value;

    /// <summary>
    /// Current value of all the assets.
    /// </summary>
    public double Value
    {
        get => _value;
        private set => SetField(ref _value, value);
    }

    private double _profit;

    /// <summary>
    /// Current profit yielded by all the assets.
    /// </summary>
    public double Profit
    {
        get => _profit;
        set => SetField(ref _profit, value);
    }

    /// <inheritdoc/>
    public ISynchronizableChart SyncChart => Chart;

    /// <inheritdoc/>
    IExchangeSettings ISynchronizableExchangeControl.Settings => _settings;

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
    private async void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e) =>
        await UpdateChartAsync(fundamentalUpdate: false);

    private bool _showValueChart = true;

    /// <summary>
    /// Flag determining whether to show "value" or "profit" candle-sticks.
    /// </summary>
    public bool ShowValueChart
    {
        get => _showValueChart;
        set => SetField(ref _showValueChart, value);
    }

    /// <summary>
    /// Event handler.
    /// </summary>
    private async void RadioButton_OnChecked(object sender, RoutedEventArgs e) =>
        await UpdateChartAsync(fundamentalUpdate: false);

    /// <summary>
    /// Constructor.
    /// </summary>
    public AssetAnalysisControl() => InitializeComponent();

    private IChartSynchronizationController _syncController;

    private ObservableCollection<ExchangeId> _exchanges;

    /// <summary>
    /// IDs of available exchanges.
    /// </summary>
    public ObservableCollection<ExchangeId> Exchanges
    {
        get => _exchanges;
        private set => SetField(ref _exchanges, value);
    }

    /// <summary>
    /// Activates the control.
    /// </summary>
    public void Activate(IMultiExchange client, ObservableCollection<AssetRecord> assets,
        ExchangeSettings settings, IChartSynchronizationController syncController)
    {
        AvailableTimeIntervals = new ObservableCollection<TimeGranularity>(client.Granularities);
        Exchanges = new ObservableCollection<ExchangeId>(client.Exchanges);

        Settings = settings;

        _syncController?.UnRegister(this);
        _syncController = syncController;
        _syncController?.Register(this);

        Assets = assets;
        Assets.CollectionChanged += Assets_CollectionChanged;

        AssetManager.Activate();
        AssetManager.PropertyChanged += AssetManager_PropertyChanged;

        Symbols = new ObservableCollection<string>(client.Symbols);
        Chart.PropertyChanged += ChartOnPropertyChanged;

        _client = client;

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += TimerTickEventHandler;  
        _updateTimer.Start();
    }

    /// <summary>
    /// Handles "tick" event of the timer.
    /// </summary>
    private async void TimerTickEventHandler(object sender, EventArgs e)
    {
        await UpdateChartAsync(fundamentalUpdate: true);
        await UpdatePrice();
    }

    /// <summary>
    /// Indicates whether the control is activated or not.
    /// </summary>
    private bool IsActivated => _client != null && Settings != null;

    /// <summary>
    /// Does opposite to what <see cref="Activate"/> does.
    /// </summary>
    public void Deactivate()
    {
        if (!IsActivated) return;

        _syncController?.UnRegister(this);

        if (_updateTimer != null)
        {
            _updateTimer.Tick -= TimerTickEventHandler;
            _updateTimer.Stop();
            _updateTimer = null;
        }

        if (Assets != null)
        {
            Assets.CollectionChanged -= Assets_CollectionChanged;
            Assets = null;
        }

        AssetManager.PropertyChanged -= AssetManager_PropertyChanged;
        AssetManager.Deactivate();

        Chart.PropertyChanged -= ChartOnPropertyChanged;

        Settings = null;
        _client = null;
    }

    /// <summary>
    /// Handles change of properties of the "asset manager".
    /// </summary>
    private async void AssetManager_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is AssetManagerControl && e.PropertyName == nameof(AssetManagerControl.Assets))
            await UpdateChartAsync(fundamentalUpdate: false);
    }

    /// <summary>
    /// Handles change of chart properties.
    /// </summary>
    private async void ChartOnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is ExchangeChartControl && e.PropertyName == nameof(ExchangeChartControl.TimeFrameEndLocalTime))
            await UpdateChartAsync(fundamentalUpdate: false);
    }

    /// <summary>
    /// Handles change of the collection of assets.
    /// </summary>
    private async void Assets_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) =>
        await UpdateChartAsync(fundamentalUpdate: false);

    /// <summary>
    /// Visualizes the given sticks-and-price data. Must be called in UI thread.
    /// </summary>
    private void VisualizeSticks(ChartData chartData) =>
        Chart.UpdatePlots(chartData is not { IsValid: true } ? null : chartData);

    /// <summary>
    /// Visualizes the given <paramref name="value"/> and <paramref name="profit"/>.
    /// </summary>
    private void VisualizeValueAndProfit(double value, double profit)
    {
        Value = value;
        Profit = profit;
    }

    /// <summary>
    /// Returns the given <param name="valueStick"/> with the values
    /// of its fields incremented by the values of price-points from
    /// <param name="priceStick"/> converted via given <param name="priceConverter"/>.
    /// </summary>
    private static OHLC Add(OHLC valueStick, OHLC priceStick, Func<double, double> priceConverter)
    {
        if (!TimeIsPracticallyTheSame(priceStick.DateTime, valueStick.DateTime))
            throw new InvalidOperationException("Sticks must correspond to the same time-frame.");

        return valueStick with
        {
            Open = valueStick.Open + priceConverter(priceStick.Open),
            Close = valueStick.Close + priceConverter(priceStick.Close),
            Low = valueStick.Low + priceConverter(priceStick.Low),
            High = valueStick.High + priceConverter(priceStick.High),
        };
    }

    /// <summary>
    /// Converts given <paramref name="priceStick"/> into the corresponding
    /// "value-stick" according to <paramref name="priceConverter"/>
    /// </summary>
    private static OHLC ToValue(OHLC priceStick, Func<double, double> priceConverter)
    {
        return new OHLC()
        {
            DateTime = priceStick.DateTime,
            TimeSpan = priceStick.TimeSpan,
            Open = priceConverter(priceStick.Open),
            Close = priceConverter(priceStick.Close),
            Low = priceConverter(priceStick.Low),
            High = priceConverter(priceStick.High),
        };
    }

    /// <summary>
    /// Returns true if the two given time points are almost the same.
    /// </summary>
    private static bool TimeIsPracticallyTheSame(DateTime time0, DateTime time1) =>
        Math.Abs((time0 - time1).TotalSeconds) < BinanceConstants.KLineTimeGapSec;

    /// <summary>
    /// Returns the least possible offsets from the beginning
    /// of the two given data collections that correspond to
    /// the items (one in each collection) covering the same
    /// time interval.
    /// </summary>
    private static (int Offset0, int Offset1) FindCommonBeginOffset(OHLC[] data0, OHLC[] data1)
    {
        if (data0.Length == 0 || data1.Length == 0 ||
            TimeIsPracticallyTheSame(data0[0].DateTime, data1[0].DateTime))
            return (0, 0);

        if (data0[0].DateTime > data1[0].DateTime)
        {
            var offset0 = 0;

            while(offset0 < data0.Length &&
                  !TimeIsPracticallyTheSame(data0[offset0].DateTime, data1[0].DateTime))
                offset0++;

            if (offset0 == data0.Length)
                offset0 = -1;

            return (offset0, 0);
        }

        var (off1, off0) = FindCommonBeginOffset(data1, data0);

        return (off0, off1);
    }

    /// <summary>
    /// Appends price values from <paramref name="price"/> converted
    /// via <paramref name="priceConverter"/> to the corresponding fields
    /// of the "sticks" from <paramref name="value"/>.
    /// </summary>
    private static OHLC[] Append(OHLC[] value, OHLC[] price, Func<double, double> priceConverter)
    {
        var (offsetValue, offsetPrice) = FindCommonBeginOffset(value, price);

        if (offsetPrice < 0 || offsetValue < 0)
            return [];

        var resultLength = Math.Min(value.Length - offsetValue, price.Length - offsetPrice);

        var result = new OHLC[resultLength];

        for (var itemId = 0; itemId < resultLength; itemId++)
            result[itemId] = Add(value[itemId + offsetValue], price[itemId + offsetPrice], priceConverter);

        return result;
    }

    /// <summary>
    /// Returns current total value of all selected assets and the corresponding profit.
    /// </summary>
    private static async Task<(double Value, double Profit)> RetrieveValueAndProfit(UpdateRequestMinimal request,
        IClientCached client)
    {
        if (client == null) return new (double.NaN, double.NaN);// deactivated state

        var assets = request.Assets.Select(x => x.Copy()).ToList();
        var values = new double[assets.Count];
        var profits = new double[assets.Count];

        await Task.WhenAll(assets.Select(async (asset, assetId) =>
        {
            if (!asset.Selected)
                return Task.CompletedTask;

            var price = await client.GetPriceAsync(asset.Symbol, 500);

            var priceExpanded = price?.Price ?? double.NaN;
            values[assetId] = asset.Value(priceExpanded);
            profits[assetId] = asset.Profit(priceExpanded);
            return Task.CompletedTask;
        }));

        return (values.Sum(), profits.Sum());
    }

    /// <summary>
    /// Retrieves the "k-line" data for the given time interval.
    /// </summary>
    private static async Task<ChartData> RetrieveKLines(UpdateRequest request,
        IClientCached client, bool calculateValue)
    {
        if (client == null) return null;// deactivated state

        var timeFrame = request.TimeFrame;
        var assets = request.Assets.Select(x => x.Copy()).ToList();

        if (!timeFrame.Discretization.IsValid || assets.Count == 0 || request.Cancelled)
            return null;

        // In case of "three days" granularity k-lines can be misaligned for entire day or two.
        // This, apparently, is a problem if we want to add them over different assets.
        // The current solution is just skip the evaluation if we have more
        // than one asset to process.
        if (timeFrame.Discretization.Span.TotalDays.Equals(3) && assets.Count(x => x.Selected) > 1)
            return ChartData.CreateInvalid;

        var frameExtension = 0.25 * timeFrame.Duration;
        var frameBegin = timeFrame.Begin.Subtract(frameExtension);
        var frameEnd = timeFrame.End.Add(frameExtension);

        var valueSticks = Array.Empty<OHLC>();

        double PriceToValueConverter(AssetRecord a, double p) => a.Value(p);
        double PriceToProfitConverter(AssetRecord a, double p) => a.Profit(p);

        foreach (var asset in assets)
        {
            if (!asset.Selected)
                continue;

            var symbol = asset.Symbol;

            var (data, success) = await client.GetKLinesAsync(symbol,
                timeFrame.Discretization, frameBegin, frameEnd, request.FundamentalUpdate);

            if (!success || request.Cancelled)
                return null;

            if (data.IsNullOrEmpty())
                return ChartData.CreateInvalid;

            var priceSticks = data.Select(x => x.ToScottPlotCandleStick()).Reverse().ToArray();

            Func<double, double> priceConverter = calculateValue ? x => PriceToValueConverter(asset, x) :
                x => PriceToProfitConverter(asset, x);

            valueSticks = valueSticks.Length == 0 ? priceSticks.Select(x => ToValue(x, priceConverter)).ToArray() :
                Append(valueSticks, priceSticks, priceConverter);

            if (valueSticks.IsNullOrEmpty())
                return ChartData.CreateInvalid;
        }

        valueSticks = valueSticks.Reverse().ToArray();

        return new ChartData(valueSticks, null, [], [],
            0, timeFrame.Duration.TotalDays);
    }

    /// <summary>
    /// The input needed to request current price of selected "assets".
    /// </summary>
    private class UpdateRequestMinimal(IList<AssetRecord> assets, ExchangeId exchangeId)
    {
        /// <summary>
        /// Collection of assets to process.
        /// </summary>
        public IList<AssetRecord> Assets { get; } = assets;

        /// <summary>
        /// ID of the exchange to retrieve data from.
        /// </summary>
        public ExchangeId ExchangeId { get; } = exchangeId;
    }

    /// <summary>
    /// All the input needed to request update of chart data.
    /// </summary>
    private class UpdateRequest : UpdateRequestMinimal
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        /// <summary>
        /// Returns "true" if cancellation was requested.
        /// </summary>
        public bool Cancelled => _cancellationTokenSource.Token.IsCancellationRequested;

        /// <summary>
        /// Cancels the request.
        /// </summary>
        public void Cancel() => _cancellationTokenSource.Cancel();

        /// <summary>
        /// Constructor.
        /// </summary>
        public UpdateRequest(TimeFrame timeFrame, IList<AssetRecord> assets, ExchangeId exchangeId,
            bool fundamentalUpdate) : base(assets, exchangeId)
        {
            TimeFrame = timeFrame;
            FundamentalUpdate = fundamentalUpdate;
        }

        /// <summary>
        /// Time frame of the update.
        /// </summary>
        public TimeFrame TimeFrame { get; }

        /// <summary>
        /// If "true" the request can possibly result in some queries to Binance server, whereas if "false"
        /// the request most probably will be resolved using chased data (if available).
        /// </summary>
        public bool FundamentalUpdate { get; }
    }

    private bool _kLineRequestIsBeingProcessed = false;

    private UpdateRequest _kLineRequest = null;

    /// <summary>
    /// Builds update request for "k-line" data according to the current state of the system.
    /// </summary>
    private UpdateRequest BuildKLineRequest(bool force)
    {
        return IsActivated ? new UpdateRequest(new TimeFrame(TimeDiscretization, Settings.StickRange,
                DateTimeUtils.LocalToUtcOad(Chart.TimeFrameEndLocalTime)), Assets.ToArray(), Settings.ExchangeId, force) : null;
    }

    /// <summary>
    /// Returns "true" if the thread in which this function is called is the UI one.
    /// </summary>
    private static bool IsUiThread => Dispatcher.CurrentDispatcher.Thread == Thread.CurrentThread;

    /// <summary>
    /// Method to update chart.
    /// </summary>
    private async Task UpdateChartAsync(bool fundamentalUpdate)
    {
        if (!IsUiThread)
            throw new InvalidOperationException("This is supposed to me the UI thread");

        var tempRequest = BuildKLineRequest(fundamentalUpdate);

        if (tempRequest == null)
            return;

        // Cancel heavy fundamental update requests if those are coming faster than we can process them.
        if (_kLineRequest is { FundamentalUpdate: true, Cancelled: false })
        {
            if (tempRequest is { FundamentalUpdate: false })
                _kLineRequest.Cancel();
            else if (_kLineRequestIsBeingProcessed)
                return;
        }

        _kLineRequest = tempRequest;

        if (_kLineRequestIsBeingProcessed)
            return;

        _kLineRequestIsBeingProcessed = true;

        try
        {
            UpdateRequest request;

            do
            {
                request = _kLineRequest;

                var sticks = await Task.Run(async () =>
                    await RetrieveKLines(request, _client[request.ExchangeId], ShowValueChart));

                if (sticks != null && !request.Cancelled) VisualizeSticks(sticks);

            } while (_kLineRequest != request);
        }
        finally
        {
            _kLineRequestIsBeingProcessed = false;
        }
    }

    private bool _priceRequestIsBeingProcessed = false;

    private UpdateRequestMinimal _priceRequest = null;

    /// <summary>
    /// Builds update request for "price" data according to the current state of the system.
    /// </summary>
    private UpdateRequestMinimal BuildPriceRequest() => IsActivated ?
        new UpdateRequestMinimal(Assets.ToArray(), Settings.ExchangeId) : null;

    /// <summary>
    /// Method to update price label.
    /// </summary>
    private async Task UpdatePrice()
    {
        if (!IsUiThread)
            throw new InvalidOperationException("This is supposed to me the UI thread");

        _priceRequest = BuildPriceRequest();

        if (_priceRequest == null || _priceRequestIsBeingProcessed)
            return;

        _priceRequestIsBeingProcessed = true;

        try
        {
            do
            {
                var request = _priceRequest;
                _priceRequest = null;

                var (value, profit) = await Task.Run(async () =>
                    await RetrieveValueAndProfit(request, _client[request.ExchangeId]));

                VisualizeValueAndProfit(value, profit);

            } while (_priceRequest != null);
        }
        finally
        {
            _priceRequestIsBeingProcessed = false;
        }
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
    public void Dispose() => Deactivate();
}