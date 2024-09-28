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
using System.Windows.Data;
using System.Windows.Input;
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
    private TimeFrame _currentTimeFrame = null!;
    private ExchangeData _currentExchangeData = null!;

    private bool _spikeIndicator = true;

    /// <summary>
    /// Flag determining indicator type
    /// </summary>
    public bool SpikeIndicator
    {
        get => _spikeIndicator;
        set => SetField(ref _spikeIndicator, value);
    }

    /// <summary>
    /// Updates current exchange data field.
    /// </summary>
    private void UpdateExchangeData(string exchangeDescriptor)
    {
        _currentExchangeData = new ExchangeData(exchangeDescriptor, DateTime.Now);
    }

    /// <summary>
    /// Updates current time frame.
    /// </summary>
    private void UpdateCurrentTimeFrame(KlineInterval discretization)
    {
        _currentTimeFrame = new TimeFrame(discretization, DateTime.Now);
    }

    /// <summary>
    /// Collection of available symbols
    /// </summary>
    public ObservableCollection<string> Symbols
    {
        get => _symbols;
        private init => SetField(ref _symbols, value);
    }

    private string _selectedSymbol = null!;

    /// <summary>
    /// The selected symbol.
    /// </summary>
    public string SelectedSymbol
    {
        get => _selectedSymbol;
        set
        {
            if (SetField(ref _selectedSymbol, value))
            {
                UpdateExchangeData(_selectedSymbol);
                UpdateCurrentTimeFrame(_currentTimeInterval);
                UpdateChartInBackground();
            }
        }
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
        
    private string _priceColor = "Black";

    /// <summary>
    /// Color of the price tag in use
    /// </summary>
    public string PriceColor
    {
        get => _priceColor;
        private set => SetField(ref _priceColor, value);
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

    private KlineInterval _currentTimeInterval;

    /// <summary>
    /// Currently selected time interval,
    /// </summary>
    public KlineInterval CurrentTimeInterval
    {
        get => _currentTimeInterval;
        set
        {
            if (SetField(ref _currentTimeInterval, value))
            {
                UpdateCurrentTimeFrame(_currentTimeInterval);
                UpdateChartInBackground();
            }
        }
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
    private class TimeFrame(KlineInterval discretization, DateTime stamp)
    {
        /// <summary>
        /// Begin point.
        /// </summary>
        public DateTime Begin => DateTime.UtcNow.Subtract(Discretization.ToTimeSpan().Multiply(100));

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
    /// Returns price and volume indicators calculated according to the current settings.
    /// </summary>
    private async Task<(IList<int> PriceIndicators, IList<int> VolumeIndicators)> CalculateIndicatorPoints(
        IList<IBinanceKline> sticks, List<double> tradeVolumeData, int windowSize)
    {
        return await Task.Run(async () =>
        {
            var spikeIndicator = Dispatcher.Invoke(() => _spikeIndicator);

            var priceDataLow = sticks.Select(x => new TimeSeriesAnalyzer.Input
                { InData = (float)x.LowPrice }).ToArray();

            var priceDataHigh = sticks.Select(x => new TimeSeriesAnalyzer.Input
                { InData = (float)x.HighPrice }).ToArray();

            var priceIndicatorsLow = spikeIndicator ? await TimeSeriesAnalyzer.DetectSpikesAsync(priceDataLow, windowSize) :
                await TimeSeriesAnalyzer.DetectChangePointsAsync(priceDataLow, windowSize);

            var priceIndicatorsHigh = spikeIndicator ? await TimeSeriesAnalyzer.DetectSpikesAsync(priceDataHigh, windowSize) :
                await TimeSeriesAnalyzer.DetectChangePointsAsync(priceDataHigh, windowSize);

            var priceIndicators = priceIndicatorsLow.ToHashSet().Concat(priceIndicatorsHigh).ToArray();

            var volumeData = tradeVolumeData.Select(x => new TimeSeriesAnalyzer.Input { InData = (float)x }).ToArray();

            var volumeIndicators = spikeIndicator ? await TimeSeriesAnalyzer.DetectSpikesAsync(volumeData, windowSize) :
                await TimeSeriesAnalyzer.DetectChangePointsAsync(volumeData, windowSize);

            return (priceIndicators, volumeIndicators);
        });
    }

    /// <summary>
    /// Retrieves the sticks-and-price data for the given time interval.
    /// </summary>
    private async Task<ChartData> RetrieveSticksAndPrice()
    {
        var (timeFrame, exchange, client) =
            Dispatcher.Invoke(() => (_currentTimeFrame, _currentExchangeData, _client));

        if (timeFrame == null || timeFrame.Discretization == default ||
            exchange == null || exchange.ExchangeDescriptor is null or "" || client == null)
            return null;

        var sticks = await client.GetCandleSticksAsync(timeFrame.Begin, timeFrame.End,
            timeFrame.Discretization, exchange.ExchangeDescriptor);
        var price = await client.GetCurrentPrice(exchange.ExchangeDescriptor);

        var windowSize = 10;

        var (priceIndicators, volumeIndicators) =
            await CalculateIndicatorPoints(sticks, ChartData.ToTradeVolumes(sticks), windowSize);

        return new ChartData(sticks, price, exchange.Stamp, timeFrame.Stamp,
            priceIndicators, volumeIndicators, windowSize);
    }

    /// <summary>
    /// Retrieves order data for the current symbol.
    /// </summary>
    private async Task<OrderBook> RetrieveOrderBook()
    {
        var (exchange, client) = Dispatcher.Invoke(() => (_currentExchangeData, _client));
            
        if (exchange == null || exchange.ExchangeDescriptor is null or "" || client == null) return null;
            
        return new OrderBook(await _client.GetOrders(exchange.ExchangeDescriptor), exchange.Stamp);
    }

    /// <summary>
    /// Visualizes the given sticks-and-price data. Must be called in UI thread.
    /// </summary>
    private void VisualizeSticksAndPrice(ChartData chartData)
    {
        if (chartData == null || !chartData.IsValid() || _currentTimeFrame == null ||
            _currentExchangeData == null)
        {
            Chart.UpdatePlots(null);
            Price = "N/A";
            return;
        }

        if (chartData.ExchangeStamp != _currentExchangeData.Stamp ||
            chartData.TimeFrameStamp != _currentTimeFrame.Stamp)
            return;

        Chart.UpdatePlots(chartData);

        var priceData = chartData.Price;
        if (priceData != null)
        {
            Price = $"{priceData.Price:0.#####}";
            PriceColor = chartData.IsPriceUp() ? "Green" : "Red";
        }
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

        if (orderBook.Stamp != _currentExchangeData.Stamp)
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

    /// <summary>
    /// Constructor.
    /// </summary>
    public CryptoExchangeControl(BAnalyzerCore.Binance client, IList<string> exchangeSymbols)
    {
        InitializeComponent();

        AvailableTimeIntervals =
            new ObservableCollection<KlineInterval>(Enum.GetValues(typeof(KlineInterval)).Cast<KlineInterval>().
                Where(x => x != KlineInterval.OneSecond));
            
        _client = client;
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
    /// Event handler.
    /// </summary>
    private void UIElement_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var comboBox = (ComboBox)sender;
        var text = comboBox.Text;
        // Filter the items in the ComboBox based on the text
        var view = CollectionViewSource.GetDefaultView(comboBox.ItemsSource);
        view.Filter = item => item.ToString()!.StartsWith(text, StringComparison.InvariantCultureIgnoreCase);
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