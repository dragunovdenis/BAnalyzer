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
using BAnalyzer.DataStructures;
using BAnalyzerCore;
using Binance.Net.Enums;
using Binance.Net.Interfaces;

namespace BAnalyzer.Controls;

/// <summary>
/// Interaction logic for ExchangeChartControl.xaml
/// </summary>
public partial class CryptoExchangeControl : INotifyPropertyChanged, IDisposable
{
    private readonly BAnalyzerCore.Binance _client = null!;

    private ObservableCollection<string> _symbols = null!;

    private Timer _updateTimer;
    private TimeFrame CurrentTimeFrame => new(TimeDiscretization, _timeStamp, Settings.StickRange);
    private ExchangeData CurrentExchangeData => new(ExchangeDescriptor, _timeStamp);
    private DateTime _timeStamp;

    private void UpdateTimeStamp() => _timeStamp = DateTime.Now;

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
    /// Representation of a time frame.
    /// </summary>
    private class TimeFrame(KlineInterval discretization, DateTime stamp, int sticksPerChart)
    {
        /// <summary>
        /// Begin point.
        /// </summary>
        public DateTime Begin => DateTime.UtcNow.Subtract(Discretization.ToTimeSpan().Multiply(sticksPerChart));

        /// <summary>
        /// End point.
        /// </summary>
        public DateTime End => DateTime.UtcNow;

        /// <summary>
        /// Discretization of measurements (in time).
        /// </summary>
        public KlineInterval Discretization { get; } = discretization;

        /// <summary>
        /// Time stamp.
        /// </summary>
        public DateTime Stamp { get; } = stamp;
    }

    /// <summary>
    /// Representation of an exchange data.
    /// </summary>
    private class ExchangeData(string exchangeDescriptor, DateTime stamp)
    {
        /// <summary>
        /// Descriptor of the exchange (also known as "symbol").
        /// </summary>
        public string ExchangeDescriptor { get; } = exchangeDescriptor;

        /// <summary>
        /// Time stamp.
        /// </summary>
        public DateTime Stamp { get; } = stamp;
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
    /// Returns price and volume indicators calculated according to the current settings.
    /// </summary>
    private async Task<(IList<int> PriceIndicators, IList<int> VolumeIndicators, int WindowSize)> CalculateIndicatorPoints(
        IList<IBinanceKline> sticks, List<double> tradeVolumeData)
    {
        return await Task.Run(async () =>
        {
            var (spikeIndicator, windowSize) = Dispatcher.Invoke(() => (Settings.CurrentAnalysisIndicator, Settings.MainAnalysisWindow));

            var priceDataLow = sticks.Select(x => new TimeSeriesAnalyzer.Input
                { InData = (float)x.LowPrice }).ToArray();

            var priceDataHigh = sticks.Select(x => new TimeSeriesAnalyzer.Input
                { InData = (float)x.HighPrice }).ToArray();

            var priceIndicatorsLow = await CalculateIndicators(spikeIndicator, priceDataLow, windowSize);
            var priceIndicatorsHigh = await CalculateIndicators(spikeIndicator, priceDataHigh, windowSize);
            var priceIndicators = priceIndicatorsLow.ToHashSet().Concat(priceIndicatorsHigh).ToArray();

            var volumeData = tradeVolumeData.Select(x => new TimeSeriesAnalyzer.Input { InData = (float)x }).ToArray();
            var volumeIndicators = await CalculateIndicators(spikeIndicator, volumeData, windowSize);

            return (priceIndicators, volumeIndicators, windowSize);
        });
    }

    /// <summary>
    /// Retrieves the sticks-and-price data for the given time interval.
    /// </summary>
    private async Task<ChartData> RetrieveSticksAndPrice()
    {
        var (timeFrame, exchange, client) = Dispatcher.Invoke(() => (CurrentTimeFrame, CurrentExchangeData, _client));

        if (timeFrame == null || timeFrame.Discretization == default ||
            exchange == null || exchange.ExchangeDescriptor is null or "" || client == null)
            return null;

        var sticks = await client.GetCandleSticksAsync(timeFrame.Begin, timeFrame.End,
            timeFrame.Discretization, exchange.ExchangeDescriptor);
        var price = await client.GetCurrentPrice(exchange.ExchangeDescriptor);

        var (priceIndicators, volumeIndicators, windowSize) =
            await CalculateIndicatorPoints(sticks, ChartData.ToTradeVolumes(sticks));

        return new ChartData(sticks, price, exchange.Stamp, timeFrame.Stamp,
            priceIndicators, volumeIndicators, windowSize);
    }

    /// <summary>
    /// Retrieves order data for the current symbol.
    /// </summary>
    private async Task<OrderBook> RetrieveOrderBook()
    {
        var (exchange, client) = Dispatcher.Invoke(() => (CurrentExchangeData, _client));
            
        if (exchange == null || exchange.ExchangeDescriptor is null or "" || client == null) return null;
            
        return new OrderBook(await _client.GetOrders(exchange.ExchangeDescriptor), exchange.Stamp);
    }

    /// <summary>
    /// Visualizes the given sticks-and-price data. Must be called in UI thread.
    /// </summary>
    private void VisualizeSticksAndPrice(ChartData chartData)
    {
        if (chartData == null || !chartData.IsValid() || CurrentTimeFrame == null ||
            CurrentExchangeData == null)
        {
            Chart.UpdatePlots(null);
            Price = "N/A";
            return;
        }

        if (chartData.ExchangeStamp != CurrentExchangeData.Stamp ||
            chartData.TimeFrameStamp != CurrentTimeFrame.Stamp)
            return;

        Chart.UpdatePlots(chartData);

        var priceData = chartData.Price;
        if (priceData != null)
            Price = $"Price: {priceData.Price,7:F5} USDT";
    }

    /// <summary>
    /// Updates order book control with the given content.
    /// </summary>
    private void VisualizeOrders(OrderBook orderBook)
    {
        if (orderBook?.Book == null)
        {
            Orders.Update(null);
            return;
        }

        if (orderBook.Stamp != CurrentExchangeData.Stamp)
            return;
        
        Orders.Update(orderBook.Book);
    }
        
    /// <summary>
    /// Method to update chart asynchronously
    /// </summary>
    private void UpdateChartInBackground()
    {
        Task.Run(async () =>
        {
            var sticksAndPrice = await RetrieveSticksAndPrice();
            var orderBook = await RetrieveOrderBook();
            Dispatcher.Invoke(() =>
            {
                VisualizeSticksAndPrice(sticksAndPrice);
                VisualizeOrders(orderBook);
            });
        });
    }

    private ExchangeSettings _settings;

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
    private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(_settings.TimeDiscretization) &&
            e.PropertyName is not nameof(_settings.ExchangeDescriptor))
            return;

        UpdateTimeStamp();
        UpdateChartInBackground();
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    public CryptoExchangeControl(BAnalyzerCore.Binance client, IList<string> exchangeSymbols, ExchangeSettings settings)
    {
        Settings = settings;
        _client = client;
        InitializeComponent();

        AvailableTimeIntervals =
            new ObservableCollection<KlineInterval>(Enum.GetValues(typeof(KlineInterval)).Cast<KlineInterval>().
                Where(x => x != KlineInterval.OneSecond));
            
        Symbols = new ObservableCollection<string>(exchangeSymbols);
        _updateTimer = new Timer(x => UpdateChartInBackground(),
            new AutoResetEvent(false), 1000, 1000);
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