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

using BAnalyzer.Controllers;
using BAnalyzer.DataStructures;
using BAnalyzer.Interfaces;
using BAnalyzer.Utils;
using BAnalyzerCore;
using BAnalyzerCore.DataStructures;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Threading;

namespace BAnalyzer.Controls;

/// <summary>
/// Interaction logic for ExchangeChartControl.xaml
/// </summary>
public partial class CryptoExchangeControl : INotifyPropertyChanged,
    IDisposable, ISynchronizableExchangeControl
{
    private readonly ExchangeClient _client = null;

    private readonly DispatcherTimer _updateTimer;

    private readonly ObservableCollection<string> _symbols = null!;

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
    public TimeGranularity TimeDiscretization
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
    
    private readonly ObservableCollection<TimeGranularity> _availableTimeIntervals = null!;

    /// <summary>
    /// Collection of available time intervals.
    /// </summary>
    public ObservableCollection<TimeGranularity> AvailableTimeIntervals
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
    private static async Task<ChartData> RetrieveSticks(UpdateRequest request, ExchangeClient client)
    {
        var timeFrame = request.TimeFrame;
        var exchangeDescriptor = request.ExchangeDescriptor;

        if (!timeFrame.Discretization.IsValid ||
            exchangeDescriptor is null or "" || client == null || request.Cancelled)
            return null;

        var frameExtension = 0.25 * timeFrame.Duration;
        var (sticks, success) = await client.GetCandleSticksAsync(timeFrame.Begin.Subtract(frameExtension),
            timeFrame.End.Add(frameExtension), timeFrame.Discretization, exchangeDescriptor, request.FundamentalUpdate);

        if (!success || request.Cancelled)
            return null;

        if (sticks.IsNullOrEmpty())
            return ChartData.CreateInvalid;

        var (priceIndicators, volumeIndicators, windowSize) =
            await CalculateIndicatorPoints(sticks, ChartData.ToTradeVolumes(sticks), request.AnalysisSettings);

        return new ChartData(sticks, priceIndicators,
            volumeIndicators, windowSize, timeFrame.Duration.TotalDays);
    }

    /// <summary>
    /// Retrieves order data for the current symbol.
    /// </summary>
    private static async Task<IOrderBook> RetrieveOrderBook(UpdateRequestMinimal request, ExchangeClient client)
    {
        if (request.ExchangeDescriptor is null or "" || client == null) return null;
            
        return await client.GetOrders(request.ExchangeDescriptor);
    }

    /// <summary>
    /// Visualizes the given sticks-and-price data. Must be called in UI thread.
    /// </summary>
    private void VisualizeSticks(ChartData chartData) =>
        Chart.UpdatePlots(chartData is not { IsValid: true } ? null : chartData);

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
    private void VisualizeOrders(IOrderBook orderBook) => Orders.Update(orderBook);

    /// <summary>
    /// Returns "true" if the thread in which this function is called is the UI one.
    /// </summary>
    private static bool IsUiThread => Dispatcher.CurrentDispatcher.Thread == Thread.CurrentThread;

    /// <summary>
    /// The minimal input data needed to request something from Binance server.
    /// </summary>
    private class UpdateRequestMinimal(string exchangeDescriptor)
    {
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
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        /// <summary>
        /// Returns "true"if cancellation was requested.
        /// </summary>
        public bool Cancelled => _cancellationTokenSource.Token.IsCancellationRequested;

        /// <summary>
        /// Cancels the request.
        /// </summary>
        public void Cancel() => _cancellationTokenSource.Cancel();

        /// <summary>
        /// Constructor.
        /// </summary>
        public UpdateRequest(TimeFrame timeFrame, string exchangeDescriptor, bool fundamentalUpdate, AnalysisSettings settings) :
            base(exchangeDescriptor)
        {
            TimeFrame = timeFrame;
            FundamentalUpdate = fundamentalUpdate;
            AnalysisSettings = settings;
        }

        /// <summary>
        /// Analysis settings.
        /// </summary>
        public AnalysisSettings AnalysisSettings { get; }

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
    /// Builds "k-line" update request according to the current state of the system.
    /// </summary>
    private UpdateRequest BuildKLineRequest(bool force) => new (new TimeFrame(TimeDiscretization, Settings.StickRange,
            DateTimeUtils.LocalToUtcOad(Chart.TimeFrameEndLocalTime)), ExchangeDescriptor, force,
        new AnalysisSettings(Settings.CurrentAnalysisIndicator, Settings.MainAnalysisWindow));

    /// <summary>
    /// Updates chart.
    /// </summary>
    private async Task UpdateChartAsync(bool force)
    {
        if (!IsUiThread)
            throw new InvalidOperationException("This is supposed to me the UI thread");

        var tempRequest = BuildKLineRequest(force);

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

                var sticks = await Task.Run(async () => await RetrieveSticks(request, _client));

                if (sticks != null && !request.Cancelled)
                {
                    VisualizeSticks(sticks);

                    if (!sticks.IsValid) // reset time frame
                        Chart.TimeFrameEndLocalTime = double.PositiveInfinity;
                }

            } while (request != _kLineRequest);
        }
        finally
        {
            _kLineRequestIsBeingProcessed = false;
        }
    }

    private bool _priceRequestIsBeingProcessed = false;

    private UpdateRequestMinimal _priceRequest = null;

    /// <summary>
    /// Builds price update request according to the current state of the system.
    /// </summary>
    private UpdateRequestMinimal BuildPriceRequest() => new (ExchangeDescriptor);

    /// <summary>
    /// Updates "price" label.
    /// </summary>
    private async Task UpdatePriceAsync()
    {
        if (!IsUiThread)
            throw new InvalidOperationException("This is supposed to me the UI thread");

        _priceRequest = BuildPriceRequest();

        if (_priceRequestIsBeingProcessed) return;

        _priceRequestIsBeingProcessed = true;

        try
        {
            do
            {
                var request = _priceRequest;
                _priceRequest = null;

                var price = await Task.Run(async () => await _client.GetCurrentPrice(request.ExchangeDescriptor));

                if (price != null)
                    VisualizePrice(price.Price);

            } while (_priceRequest != null);
        }
        finally
        {
            _priceRequestIsBeingProcessed = false;
        }
    }

    private UpdateRequestMinimal _orderBookRequest = null;

    private bool _orderBookRequestIsBeingProcessed = false;

    /// <summary>
    /// Builds order update request according to the current state of the system.
    /// </summary>
    private UpdateRequestMinimal BuildOrderRequest() => new (ExchangeDescriptor);

    /// <summary>
    /// Updates orders.
    /// </summary>
    private async Task UpdateOrdersAsync()
    {
        if (!IsUiThread)
            throw new InvalidOperationException("This is supposed to me the UI thread");

        if (!OrdersTab.IsSelected) return;

        _orderBookRequest = BuildOrderRequest();

        if (_orderBookRequestIsBeingProcessed)
            return;

        _orderBookRequestIsBeingProcessed = true;

        try
        {
            do
            {
                var request = _orderBookRequest;
                _orderBookRequest = null;

                var orderBook = await Task.Run(async () => await RetrieveOrderBook(request, _client));

                if (orderBook != null)
                    VisualizeOrders(orderBook);

            } while (_orderBookRequest != null);
        }
        finally
        {
            _orderBookRequestIsBeingProcessed = false;
        }
    }

    private readonly ExchangeSettings _settings;

    /// <inheritdoc/>
    public ISynchronizableChart SyncChart => Chart;

    /// <inheritdoc/>
    IExchangeSettings ISynchronizableExchangeControl.Settings => _settings;

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
    private async void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e) =>
        await UpdateChartAsync(force: false);

    /// <summary>
    /// Constructor.
    /// </summary>
    public CryptoExchangeControl(ExchangeClient client, IList<string> exchangeSymbols,
        ExchangeSettings settings, IChartSynchronizationController syncController)
    {
        Settings = settings;
        _client = client;
        AvailableTimeIntervals = new ObservableCollection<TimeGranularity>(client.Granularities);

        InitializeComponent();

        syncController?.Register(this);

        Chart.PropertyChanged += Chart_PropertyChanged;
        Symbols = new ObservableCollection<string>(exchangeSymbols);

        _updateTimer = new DispatcherTimer()
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
        await UpdateChartAsync(force: true);
        await UpdatePriceAsync();
        await UpdateOrdersAsync();
    }

    /// <summary>
    /// Handles changes of the chart properties.
    /// </summary>
    private async void Chart_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is ExchangeChartControl && e.PropertyName == nameof(ExchangeChartControl.TimeFrameEndLocalTime))
            await UpdateChartAsync(force: false);
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
    public void Dispose() { _updateTimer.Stop(); }

    /// <summary>
    /// Handles selection-changed event for tabs.
    /// </summary>
    private async void TabControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OrdersTab.IsSelected)
            await UpdateOrdersAsync();
    }
}