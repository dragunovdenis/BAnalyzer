//Copyright (c) 2024 Denys Dragunov, dragunovdenis@gmail.com
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
using System.Windows.Controls;
using BAnalyzer.Controllers;
using BAnalyzer.DataStructures;
using BAnalyzer.Utils;
using BAnalyzerCore;
using BAnalyzerCore.DataStructures;
using Binance.Net.Enums;

namespace BAnalyzer.Controls;

/// <summary>
/// Interaction logic for ExchangeChartControl.xaml
/// </summary>
public partial class CryptoExchangeControl : INotifyPropertyChanged, IDisposable
{
    private readonly BAnalyzerCore.Binance _client = null;

    private Timer _updateTimer;

    private ObservableCollection<string> _symbols = null!;

    /// <summary>
    /// Collection of available symbols
    /// </summary>
    public ObservableCollection<string> Symbols
    {
        get => _symbols;
        private init => SetField(ref _symbols, value);
    }

    /// <summary>
    /// The selected exchange descriptor.
    /// </summary>
    public string ExchangeDescriptor
    {
        get => _settings.ExchangeDescriptor;
        set => _settings.ExchangeDescriptor = value;
    }

    /// <summary>
    /// Currently selected time interval.
    /// </summary>
    public KlineInterval TimeDiscretization
    {
        get => _settings.TimeDiscretization;
        set => _settings.TimeDiscretization = value;
    }

    private string _price = null!;

    /// <summary>
    /// Current price
    /// </summary>
    public string Price
    {
        get => _price;
        private set => SetField(ref _price, value);
    }
    
    private readonly ObservableCollection<KlineInterval> _availableTimeIntervals = null!;

    /// <summary>
    /// Collection of available time intervals.
    /// </summary>
    public ObservableCollection<KlineInterval> AvailableTimeIntervals
    {
        get => _availableTimeIntervals;
        private init => SetField(ref _availableTimeIntervals, value);
    }

    private bool _darkMode = true;

    /// <summary>
    /// Switches between white and dark modes.
    /// </summary>
    public bool DarkMode
    {
        get => _darkMode;
        set => SetField(ref _darkMode, value);
    }

    /// <summary>
    /// Calculates indicators of the corresponding type.
    /// </summary>
    private static async Task<int[]> CalculateIndicators(AnalysisIndicatorType type, TimeSeriesAnalyzer.Input[] input, int windowSize)
    {
        switch (type)
        {
            case AnalysisIndicatorType.None: return [];
            case AnalysisIndicatorType.Spike: return await TimeSeriesAnalyzer.DetectSpikesAsync(input, windowSize);
            case AnalysisIndicatorType.Change:
                return await TimeSeriesAnalyzer.DetectChangePointsAsync(input, windowSize);
            default: throw new InvalidOperationException("Unknown analysis type");
        }
    }

    /// <summary>
    /// Analysis settings.
    /// </summary>
    private readonly record struct AnalysisSettings(AnalysisIndicatorType AnalysisIndicator, int AnalysisWindowSize);

    /// <summary>
    /// Returns price and volume indicators calculated according to the current settings.
    /// </summary>
    private static async Task<(IList<int> PriceIndicators, IList<int> VolumeIndicators, int WindowSize)>
        CalculateIndicatorPoints(IList<KLine> sticks, IList<double> tradeVolumeData, AnalysisSettings settings)
    {
        var analysisIndicator = settings.AnalysisIndicator;
        var analysisWindowSize = settings.AnalysisWindowSize;

        var priceDataLow = sticks.Select(x => new TimeSeriesAnalyzer.Input
            { InData = (float)x.LowPrice }).ToArray();

        var priceDataHigh = sticks.Select(x => new TimeSeriesAnalyzer.Input
            { InData = (float)x.HighPrice }).ToArray();

        var priceIndicatorsLow = await CalculateIndicators(analysisIndicator, priceDataLow, analysisWindowSize);
        var priceIndicatorsHigh = await CalculateIndicators(analysisIndicator, priceDataHigh, analysisWindowSize);
        var priceIndicators = priceIndicatorsLow.ToHashSet().Concat(priceIndicatorsHigh).ToArray();

        var volumeData = tradeVolumeData.Select(x => new TimeSeriesAnalyzer.Input { InData = (float)x }).ToArray();
        var volumeIndicators = await CalculateIndicators(analysisIndicator, volumeData, analysisWindowSize);

        return (priceIndicators, volumeIndicators, analysisWindowSize);
    }

    /// <summary>
    /// Retrieves the sticks-and-price data for the given time interval.
    /// </summary>
    private static async Task<ChartData> RetrieveSticks(UpdateRequest request,
        BAnalyzerCore.Binance client, AnalysisSettings settings)
    {
        var timeFrame = request.TimeFrame;
        var exchangeDescriptor = request.ExchangeDescriptor;

        if (timeFrame == null || timeFrame.Discretization == default ||
            exchangeDescriptor is null or "" || client == null || !request.IsRequestStillRelevant())
            return null;

        var frameDuration = timeFrame.Duration;
        var sticks = await client.GetCandleSticksAsync(timeFrame.Begin.Subtract(frameDuration),
            timeFrame.End.Add(frameDuration), timeFrame.Discretization, exchangeDescriptor, request.Force);

        if (!request.IsRequestStillRelevant())
            return null;

        var (priceIndicators, volumeIndicators, windowSize) =
            await CalculateIndicatorPoints(sticks, ChartData.ToTradeVolumes(sticks), settings);

        return new ChartData(sticks, request.UpdateRequestId,
            priceIndicators, volumeIndicators, windowSize, frameDuration.TotalDays);
    }

    /// <summary>
    /// Retrieves order data for the current symbol.
    /// </summary>
    private static async Task<OrderBook> RetrieveOrderBook(UpdateRequestMinimal request, BAnalyzerCore.Binance client)
    {
        if (request.ExchangeDescriptor is null or "" || client == null || !request.IsRequestStillRelevant()) return null;
            
        return new OrderBook(await client.GetOrders(request.ExchangeDescriptor), request.UpdateRequestId);
    }

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
        if (price > 1e-3)
            Price = double.IsNaN(price) ? "N/A" : $"Value: {price,7:F5} USDT";
        else
            Price = double.IsNaN(price) ? "N/A" : $"Value: {price,5:E4} USDT";
    }

    /// <summary>
    /// Updates order book control with the given content.
    /// </summary>
    private void VisualizeOrders(OrderBook orderBook) => Orders.Update(orderBook?.Book);

    /// <summary>
    /// The minimal input data needed to request something from Binance server.
    /// </summary>
    private class UpdateRequestMinimal(string exchangeDescriptor,
        int updateRequestId, IUpdateControllerReadOnly updateController)
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
        /// Descriptor of the exchange (also known as "symbol").
        /// </summary>
        public string ExchangeDescriptor { get; } = exchangeDescriptor;
    }

    /// <summary>
    /// All the data needed to ensure update request chart data from Binance server.
    /// </summary>
    private class UpdateRequest : UpdateRequestMinimal
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public UpdateRequest(TimeFrame timeFrame, string exchangeDescriptor,
            int updateRequestId, bool force, IUpdateControllerReadOnly updateController) :
            base(exchangeDescriptor, updateRequestId, updateController)
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
    /// Builds "k-line" update request according to the current state of the system.
    /// </summary>
    private UpdateRequest BuildKLineRequest(bool force) =>
        new(new TimeFrame(TimeDiscretization, Settings.StickRange, DateTimeUtils.LocalToUtcOad(Chart.TimeFrameEndLocalTime)),
        ExchangeDescriptor, _kLineUpdateController.IssueNewRequest(), force, _kLineUpdateController);

    /// <summary>
    /// Updates chart.
    /// </summary>
    private async Task UpdateChartAsync(bool force)
    {
        try
        {
            var (updateRequest, settings) = Dispatcher.Invoke(() => (BuildKLineRequest(force),
                new AnalysisSettings(Settings.CurrentAnalysisIndicator, Settings.MainAnalysisWindow)));

            var sticks = await RetrieveSticks(updateRequest, _client, settings);

            if (sticks == null)
                return;

            Dispatcher.Invoke(() =>
            {
                if (_kLineUpdateController.TryApplyRequest(sticks.UpdateRequestId))
                    VisualizeSticks(sticks);
            });
        }
        catch (TaskCanceledException) { /*Just ignore*/ }
    }

    private readonly UpdateController _priceUpdateController = new();

    /// <summary>
    /// Builds price update request according to the current state of the system.
    /// </summary>
    private UpdateRequestMinimal BuildPriceRequest() =>
        new(ExchangeDescriptor, _priceUpdateController.IssueNewRequest(), _priceUpdateController);

    /// <summary>
    /// Updates "price" label.
    /// </summary>
    private async Task UpdatePriceAsync()
    {
        try
        {
            var updateRequest = Dispatcher.Invoke(BuildPriceRequest);
            var price = await _client.GetCurrentPrice(updateRequest.ExchangeDescriptor);

            if (price == null)
                return;

            Dispatcher.Invoke(() =>
            {
                if (_priceUpdateController.TryApplyRequest(updateRequest.UpdateRequestId))
                    VisualizePrice((double)price.Price);
            });
        }
        catch (TaskCanceledException) { /*Just ignore*/ }
    }

    private readonly UpdateController _orderUpdateController = new();

    /// <summary>
    /// Builds order update request according to the current state of the system.
    /// </summary>
    private UpdateRequestMinimal BuildOrderRequest() =>
        new(ExchangeDescriptor, _orderUpdateController.IssueNewRequest(), _orderUpdateController);

    /// <summary>
    /// Updates orders.
    /// </summary>
    private async Task UpdateOrdersAsync()
    {
        try
        {
            var (updateRequest, tabSelected) = Dispatcher.Invoke(() => (BuildOrderRequest(), OrdersTab.IsSelected));

            if (!tabSelected)
                return;

            var orderBook = await RetrieveOrderBook(updateRequest, _client);

            if (orderBook == null)
                return;

            Dispatcher.Invoke(() =>
            {
                if (_orderUpdateController.TryApplyRequest(updateRequest.UpdateRequestId))
                    VisualizeOrders(orderBook);
            });
        }
        catch (TaskCanceledException) { /*Just ignore*/ }
    }

    private readonly ExchangeSettings _settings;

    /// <summary>
    /// Settings.
    /// </summary>
    public ExchangeSettings Settings
    {
        get => _settings;

        private init 
        {
            if (_settings != value)
            {
                if (_settings !=  null)
                    _settings.PropertyChanged -= Settings_PropertyChanged;

                _settings = value;

                if (_settings != null)
                    _settings.PropertyChanged += Settings_PropertyChanged;
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
    public CryptoExchangeControl(BAnalyzerCore.Binance client, IList<string> exchangeSymbols,
        ExchangeSettings settings, IChartSynchronizationController syncController)
    {
        Settings = settings;
        _client = client;
        InitializeComponent();

        Chart.RegisterToSynchronizationController(syncController);
        AvailableTimeIntervals =
            new ObservableCollection<KlineInterval>(Enum.GetValues(typeof(KlineInterval)).Cast<KlineInterval>().
                Where(x => x != KlineInterval.OneSecond));

        Chart.PropertyChanged += Chart_PropertyChanged;
        Symbols = new ObservableCollection<string>(exchangeSymbols);
        _updateTimer = new Timer(async _ =>
            {
                await UpdateChartAsync(force: true);
                await UpdatePriceAsync();
                await UpdateOrdersAsync();
            },
            new AutoResetEvent(false), 1000, 1000);
    }

    /// <summary>
    /// Handles changes of the chart properties.
    /// </summary>
    private void Chart_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is ExchangeChartControl chart && e.PropertyName == nameof(chart.TimeFrameEndLocalTime))
            Task.Run(async () => await UpdateChartAsync(force: false));
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

    /// <summary>
    /// Handles selection-changed event for tabs.
    /// </summary>
    private async void TabControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OrdersTab.IsSelected)
            await UpdateOrdersAsync();
    }
}