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
using BAnalyzerCore;
using ScottPlot;

namespace BAnalyzer.Controls;

/// <summary>
/// Interaction logic for AssetAnalysisControl.xaml
/// </summary>
public partial class AssetAnalysisControl : INotifyPropertyChanged, IDisposable
{
    private BAnalyzerCore.Binance _client = null;

    private System.Timers.Timer _updateTimer;

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
        Task.Run(async () => await UpdateChartAsync(forceCompleteUpdate: false));

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

        Assets = assets;
        Assets.CollectionChanged += Assets_CollectionChanged;

        AssetManager.PropertyChanged += AssetManager_PropertyChanged;

        Symbols = new ObservableCollection<string>(exchangeSymbols);
        Chart.PropertyChanged += ChartOnPropertyChanged;

        _updateTimer = new System.Timers.Timer(1000);
        _updateTimer.Elapsed += _updateTimer_Elapsed;  

        _updateTimer.Start();
    }

    /// <summary>
    /// Handles "elapsed" event of the timer.
    /// </summary>
    private async void _updateTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        await UpdateChartAsync(forceCompleteUpdate: true);
        await UpdatePrice();
    }

    /// <summary>
    /// Indicates whether the control is activated or not.
    /// </summary>
    private bool IsActivated => _client != null;

    /// <summary>
    /// Does opposite to what <see cref="Activate"/> does.
    /// </summary>
    public void Deactivate()
    {
        Chart.RegisterToSynchronizationController(null);

        if (_updateTimer != null)
        {
            _updateTimer.Elapsed -= _updateTimer_Elapsed;
            _updateTimer.Stop();
            _updateTimer.Dispose();
            _updateTimer = null;
        }

        if (Assets != null)
        {
            Assets.CollectionChanged -= Assets_CollectionChanged;
            Assets = null;
        }

        AssetManager.PropertyChanged -= AssetManager_PropertyChanged;

        Chart.PropertyChanged -= ChartOnPropertyChanged;

        Settings = null;
        _client = null;
    }

    /// <summary>
    /// Handles change of properties of the "asset manager".
    /// </summary>
    private void AssetManager_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is AssetManagerControl control && e.PropertyName == nameof(control.Assets))
            Task.Run(async () => await UpdateChartAsync(forceCompleteUpdate: false));
    }

    /// <summary>
    /// Handles change of chart properties.
    /// </summary>
    private void ChartOnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is ExchangeChartControl chart && e.PropertyName == nameof(chart.TimeFrameEndLocalTime))
            Task.Run(async () => await UpdateChartAsync(forceCompleteUpdate: false));
    }

    /// <summary>
    /// Handles change of the collection of assets.
    /// </summary>
    private void Assets_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        Task.Run(async () => await UpdateChartAsync(forceCompleteUpdate: false));

    /// <summary>
    /// Visualizes the given sticks-and-price data. Must be called in UI thread.
    /// </summary>
    private void VisualizeSticks(ChartData chartData)
    {
        if (chartData == null || !chartData.IsValid())
            Chart.UpdatePlots(null);
        else
            Chart.UpdatePlots(chartData);
    }

    /// <summary>
    /// Visualizes the given <paramref name="price"/>
    /// </summary>
    private void VisualizePrice(double price)
    {
        Price = double.IsNaN(price) ? "N/A" : $"Value: {DataFormatter.FloatToCompact(price, "{0:0.###}")} USDT";
    }

    /// <summary>
    /// Returns the given <param name="valueStick"/> with the values
    /// of its "open" and "close" fields incremented by the value of
    /// the given <param name="asset"/> evaluated at the price suggested
    /// by the given <param name="priceStick"/>.
    /// </summary>
    private static OHLC Add(OHLC valueStick, OHLC priceStick, AssetRecord asset)
    {
        if (!TimeIsPracticallyTheSame(priceStick.DateTime, valueStick.DateTime))
            throw new InvalidOperationException("Sticks must correspond to the same time-frame.");

        return valueStick with
        {
            Open = valueStick.Open + asset.Value(priceStick.Open),
            Close = valueStick.Close + asset.Value(priceStick.Close),
            Low = valueStick.Low + asset.Value(priceStick.Low),
            High = valueStick.High + asset.Value(priceStick.High),
        };
    }

    /// <summary>
    /// Returns value of the given <param name="asset"/> evaluated
    /// at the price suggested by the given <param name="priceStick"/>.
    /// </summary>
    private static OHLC ToValue(OHLC priceStick, AssetRecord asset)
    {
        return new OHLC()
        {
            DateTime = priceStick.DateTime,
            TimeSpan = priceStick.TimeSpan,
            Open = asset.Value(priceStick.Open),
            Close = asset.Value(priceStick.Close),
            Low = asset.Value(priceStick.Low),
            High = asset.Value(priceStick.High),
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
    /// Appends value of the given asset to the given collection of value sticks.
    /// </summary>
    private static OHLC[] Append(OHLC[] value, OHLC[] price, AssetRecord asset)
    {
        var (offsetValue, offsetPrice) = FindCommonBeginOffset(value, price);

        if (offsetPrice < 0)
            return value;

        var resultLength = Math.Max(value.Length - offsetValue, price.Length - offsetPrice);

        var result = new OHLC[resultLength];

        var commonLength = Math.Min(value.Length - offsetValue, price.Length - offsetPrice);

        for (var itemId = 0; itemId < commonLength; itemId++)
            result[itemId] = Add(value[itemId + offsetValue], price[itemId + offsetPrice], asset);

        for (var itemId = commonLength; itemId < value.Length - offsetValue; itemId++)
            result[itemId] = value[itemId + offsetValue];

        for (var itemId = commonLength; itemId < price.Length - offsetPrice; itemId++)
            result[itemId] = ToValue(price[itemId + offsetPrice], asset);

        return result;
    }

    /// <summary>
    /// Returns current total price of all selected assets.
    /// </summary>
    private static async Task<PriceData> RetrievePrice(UpdateRequestMinimal request,
        BAnalyzerCore.Binance client)
    {
        var assets = request.Assets.Select(x => x.Copy()).ToList();
        var prices = new double[assets.Count];

        await Task.WhenAll(assets.Select(async (asset, assetId) =>
        {
            if (!asset.Selected)
                return Task.CompletedTask;

            var price = await client.GetCurrentPrice(asset.Symbol);

            prices[assetId] = asset.Value(price != null ? (double)price.Price : double.NaN);
            return Task.CompletedTask;
        }));

        return new PriceData(prices.Sum(), request.UpdateRequestId);
    }

    /// <summary>
    /// Returns an empty data that will clear the chart.
    /// </summary>
    private static ChartData EmptyChartData(int updateRequestId) => new([], null, updateRequestId,
        [], [], 0, 0);

    /// <summary>
    /// Retrieves the "k-line" data for the given time interval.
    /// </summary>
    private static async Task<ChartData> RetrieveKLines(UpdateRequest request, BAnalyzerCore.Binance client)
    {
        var timeFrame = request.TimeFrame;
        var assets = request.Assets.Select(x => x.Copy()).ToList();

        if (timeFrame == null || timeFrame.Discretization == default ||
            assets.Count == 0 || client == null || !request.IsRequestStillRelevant())
            return null;

        // In case of "three days" granularity k-lines can be misaligned for entire day or two.
        // This, apparently, is a problem if we want to add them over different assets.
        // The current solution is just skip the evaluation if we have more
        // than one asset to process.
        if (timeFrame.Discretization == KlineInterval.ThreeDay && assets.Count(x => x.Selected) > 1)
            return EmptyChartData(request.UpdateRequestId);

        var frameDuration = timeFrame.Duration;
        var frameBegin = timeFrame.Begin.Subtract(frameDuration);
        var frameEnd = timeFrame.End.Add(frameDuration);

        var valueSticks = Array.Empty<OHLC>();

        foreach (var asset in assets)
        {
            if (!asset.Selected)
                continue;

            var symbol = asset.Symbol;

            var (data, success) = await client.GetCandleSticksAsync(frameBegin, frameEnd,
                timeFrame.Discretization, symbol, request.Force);

            if (!success || data.IsNullOrEmpty())
                return null;

            var priceSticks = data.Select(x => x.ToScottPlotCandleStick()).Reverse().ToArray();

            valueSticks = valueSticks == null ? priceSticks.Select(x => ToValue(x, asset)).ToArray() :
                Append(valueSticks, priceSticks, asset);

            if (!request.IsRequestStillRelevant())
                return null;
        }

        valueSticks = valueSticks.Reverse().ToArray();

        return new ChartData(valueSticks, null, request.UpdateRequestId, [], [],
            0, frameDuration.TotalDays);
    }

    /// <summary>
    /// The input needed to request current price of selected "assets".
    /// </summary>
    private class UpdateRequestMinimal(IList<AssetRecord> assets, int updateRequestId,
        IUpdateControllerReadOnly updateController)
    {
        /// <summary>
        /// Returns "true" if the request should be processed further on.
        /// </summary>
        public virtual bool IsRequestStillRelevant() => updateController.IsRequestStillRelevant(UpdateRequestId);

        /// <summary>
        /// Request ID of the update to ensure that updates are applied in a chronological order.
        /// </summary>
        public int UpdateRequestId { get; } = updateRequestId;

        /// <summary>
        /// Collection of assets to process.
        /// </summary>
        public IList<AssetRecord> Assets { get; } = assets;
    }

    /// <summary>
    /// All the input needed to request update of chart data.
    /// </summary>
    private class UpdateRequest : UpdateRequestMinimal
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public UpdateRequest(TimeFrame timeFrame, IList<AssetRecord> assets,
            int updateRequestId, bool force, IUpdateControllerReadOnly updateController) :
            base(assets, updateRequestId, updateController)
        {
            TimeFrame = timeFrame;
            Force = force;
        }

        /// <summary>
        /// Returns "true" if the request should be processed further on.
        /// </summary>
        public override bool IsRequestStillRelevant() => Force || base.IsRequestStillRelevant();

        /// <summary>
        /// Time frame of the update.
        /// </summary>
        public TimeFrame TimeFrame { get; }

        /// <summary>
        /// Determines whether the request should be applied despite,
        /// for example, the system being overloaded by other requests.
        /// </summary>
        public bool Force { get; }
    }

    private readonly UpdateController _kLineUpdateController = new();

    /// <summary>
    /// Builds update request for "k-line" data according to the current state of the system.
    /// </summary>
    private UpdateRequest BuildKLineRequest(bool force)
    {
        return IsActivated && _kLineUpdateController.PendingRequestsCount == 0 ?
            new UpdateRequest(new TimeFrame(TimeDiscretization, Settings.StickRange,
                DateTimeUtils.LocalToUtcOad(Chart.TimeFrameEndLocalTime)),
            Assets.ToArray(), _kLineUpdateController.IssueNewRequest(), force, _kLineUpdateController) : null;
    }

    /// <summary>
    /// Method to update chart.
    /// </summary>
    private async Task UpdateChartAsync(bool forceCompleteUpdate)
    {
        try
        {
            var kLineUpdateRequest = Dispatcher.Invoke(() => BuildKLineRequest(forceCompleteUpdate));

            if (kLineUpdateRequest == null)
                return;

            var sticks = await RetrieveKLines(kLineUpdateRequest, _client);

            Dispatcher.Invoke(() =>
            {
                if (_kLineUpdateController.TryApplyRequest(kLineUpdateRequest.UpdateRequestId) && sticks != null)
                    VisualizeSticks(sticks);
            });
        }
        catch (TaskCanceledException) { /*Ignore*/ }
    }

    private readonly UpdateController _priceUpdateController = new();

    /// <summary>
    /// Builds update request for "price" data according to the current state of the system.
    /// </summary>
    private UpdateRequestMinimal BuildPriceRequest() => IsActivated && _priceUpdateController.PendingRequestsCount == 0 ?
        new UpdateRequestMinimal(Assets.ToArray(), _priceUpdateController.IssueNewRequest(),
            _priceUpdateController) : null;

    /// <summary>
    /// Method to update price label.
    /// </summary>
    private async Task UpdatePrice()
    {
        try
        {
            var priceUpdateRequest = Dispatcher.Invoke(BuildPriceRequest);

            if (priceUpdateRequest == null)
                return;

            var price = await RetrievePrice(priceUpdateRequest, _client);

            Dispatcher.Invoke(() =>
            {
                if (_priceUpdateController.TryApplyRequest(price.UpdateRequestId))
                    VisualizePrice(price.Price);
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