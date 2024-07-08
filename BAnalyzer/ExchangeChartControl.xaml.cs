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

using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;
using ScottPlot;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using BAnalyzerCore;
using ScottPlot.Plottables;
using ScottPlot.WPF;

namespace BAnalyzer
{
    /// <summary>
    /// Interaction logic for ExchangeChartControl.xaml
    /// </summary>
    public partial class ExchangeChartControl : INotifyPropertyChanged, IDisposable
    {
        private BAnalyzerCore.Binance _client = null!;

        private ObservableCollection<string> _symbols = null!;

        private Timer _updateTimer;
        private TimeFrame _currentTimeFrame = null!;
        private ExchangeData _currentExchangeData = null!;

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
            private set => SetField(ref _symbols, value);
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

        private string _priceTime = null!;

        private string _infoTipString;

        /// <summary>
        /// Field bound to the info-popup.
        /// </summary>
        public string InfoTipString
        {
            get => _infoTipString;
            private set => SetField(ref _infoTipString, value);
        }
        
        /// <summary>
        /// Timestamp of the current price.
        /// </summary>
        public string PriceTime
        {
            get => _priceTime;
            private set => SetField(ref _priceTime, value);
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

        /// <summary>
        /// Returns "true" if the given instance of a candle-stick is green,
        /// i.e. its close price is above its open price.
        /// </summary>
        private static bool IsGreen(OHLC stick)
        {
            return stick.Close >= stick.Open;
        }
        
        /// <summary>
        /// Sticks and price data struct.
        /// </summary>
        private class ChartData(List<OHLC> sticks, List<double> tradeVolumeData,
            BinancePrice price, DateTime exchangeStamp, DateTime timeFrameStamp)
        {
            /// <summary>
            /// Sticks.
            /// </summary>
            public List<OHLC> Sticks { get; } = sticks;

            /// <summary>
            /// Trade volume data for each stick.
            /// </summary>
            public List<double> TradeVolumeData { get; } = tradeVolumeData;

            /// <summary>
            /// Array of stick times in OLEA format.
            /// </summary>
            private readonly double[] _times = sticks.Select(x => x.DateTime.ToOADate()).ToArray();

            /// <summary>
            /// Price.
            /// </summary>
            public BinancePrice Price { get; } = price;

            /// <summary>
            /// Time stamp of the exchange data the current instance was built from.
            /// </summary>
            public DateTime ExchangeStamp { get; } = exchangeStamp;

            /// <summary>
            /// Time stamp of the time frame data the current instance was built from.
            /// </summary>
            public DateTime TimeFrameStamp { get; } = timeFrameStamp;

            /// <summary>
            /// Returns the time of the opening of the first candle-stick.
            /// </summary>
            public DateTime GetBeginTime()
            {
                if (Sticks == null || Sticks.Count == 0)
                    throw new InvalidOperationException("Invalid collection of candle-sticks.");
                
                var stickSpan = Sticks.First().TimeSpan;
                return Sticks.First().DateTime.Add(-0.5 * stickSpan);
            }

            /// <summary>
            /// Returns the time of the closing of the last candle-stick.
            /// </summary>
            public DateTime GetEndTime()
            {
                if (Sticks == null || Sticks.Count == 0)
                    throw new InvalidOperationException("Invalid collection of candle-sticks.");

                var stickSpan = Sticks.First().TimeSpan;
                return Sticks.Last().DateTime.Add(0.5 * stickSpan);
            }

            /// <summary>
            /// Returns "true" if the last candle-stick in the corresponding collection is green.
            /// </summary>
            /// <returns></returns>
            public bool IsPriceUp()
            {
                if (Sticks == null || Sticks.Count == 0)
                    throw new InvalidOperationException("Invalid collection of candle-sticks.");

                return IsGreen(Sticks.Last());
            }
            
            /// <summary>
            /// Returns index of a stick that corresponds to the given time-point
            /// in OLE Automation date equivalent format or "-1" if the given time
            /// point falls outside the time-frame of the chart.
            /// </summary>
            private int GetStickId(double timeOa)
            {
                if (GetBeginTime().ToOADate() > timeOa ||
                    GetEndTime().ToOADate() < timeOa)
                    return -1;
                
                var id = Array.BinarySearch(_times, timeOa);
                
                if (id >= 0)
                    return id;

                var idInverted = ~id;

                if (idInverted == 0)
                    return 0;

                if (idInverted == _times.Length)
                    return _times.Length - 1;

                return _times[idInverted] - timeOa < timeOa - _times[idInverted - 1] ? idInverted : idInverted - 1;
            }

            /// <summary>
            /// Returns index of a stick that corresponds to the given time-point
            /// in OLE Automation date equivalent format and the given price or "-1"
            /// if such a stick can't be found.
            /// </summary>
            public int GetStickId(double timeOa, double price)
            {
                var preliminaryId = GetStickId(timeOa);

                if (preliminaryId < 0)
                    return -1;
                
                var stick = Sticks[preliminaryId];

                return (stick.Low <= price && stick.High >= price) ? preliminaryId : -1;
            }

            /// <summary>
            /// Returns index of trade volume bar (which coincides with the index of the corresponding stick)
            /// that corresponds to the given time-point in OLE Automation date equivalent format and the given price or "-1"
            /// if such a stick can't be found.
            /// </summary>
            public int GetVolumeBarId(double timeOa, double price)
            {
                if (price <= 0)
                    return -1;
                
                var preliminaryId = GetStickId(timeOa);

                if (preliminaryId < 0)
                    return -1;

                var volume = TradeVolumeData[preliminaryId];

                return volume >= price ? preliminaryId : -1;
            }
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
        /// Retrieves the sticks-and-price data for the given time interval.
        /// </summary>
        private ChartData RetrieveSticksAndPrice()
        {
            var (timeFrame, exchange, client) =
                Dispatcher.Invoke(() => (_currentTimeFrame, _currentExchangeData, _client));

            if (timeFrame == null! || timeFrame.Discretization == default ||
                exchange == null! || exchange.ExchangeDescriptor is null or "" || client == null!)
                return null!;

            var sticks = client.GetCandleSticks(timeFrame.Begin, timeFrame.End,
                timeFrame.Discretization, exchange.ExchangeDescriptor).Result;

            var price = client.GetCurrentPrice(exchange.ExchangeDescriptor).Result;

            var plotSticks = sticks.Select(x => new OHLC()
            {
                Close = (double)x.ClosePrice,
                Open = (double)x.OpenPrice,
                High = (double)x.HighPrice,
                Low = (double)x.LowPrice,
                TimeSpan = timeFrame.Discretization.ToTimeSpan(),
                DateTime = x.OpenTime.Add(0.5 * timeFrame.Discretization.ToTimeSpan()).ToLocalTime()
            }).ToList();

            var volumeData = sticks.Select(x => (double)x.QuoteVolume).ToList();

            return new ChartData(plotSticks, volumeData, price, exchange.Stamp, timeFrame.Stamp);
        }

        /// <summary>
        /// Builds candle-sticks chart based on the given data.
        /// </summary>
        private CandlestickPlot BuildCandleSticks(ChartData chartData)
        {
            MainPlot.Plot.Clear();

            if (chartData.Sticks.Count == 0)
            {
                MainPlot.Refresh();
                return null;
            }

            var result = MainPlot.Plot.Add.Candlestick(chartData.Sticks);
            result.Axes.YAxis = MainPlot.Plot.Axes.Right;
            MainPlot.Plot.Axes.DateTimeTicksBottom();

            MainPlot.Plot.Axes.SetLimitsX(chartData.GetBeginTime().ToOADate(), chartData.GetEndTime().ToOADate());
            MainPlot.Refresh();

            return result;
        }

        /// <summary>
        /// Builds volume bar chart based on the given data.
        /// </summary>
        private BarPlot BuildVolumeChart(ChartData chartData)
        {
            VolPlot.Plot.Clear();

            if (chartData.Sticks.Count == 0)
            {
                VolPlot.Refresh();
                return null;
            }

            var sticks = chartData.Sticks;
            var result = VolPlot.Plot.Add.Bars(sticks.Select(x => x.DateTime.ToOADate()).ToList(), chartData.TradeVolumeData);
            result.Axes.YAxis = VolPlot.Plot.Axes.Right;

            var startTimeOa = chartData.GetBeginTime().ToOADate();
            var endTimeOa = chartData.GetEndTime().ToOADate();
            var barWidth = ((endTimeOa - startTimeOa) / result.Bars.Count()) * 0.8;

            var barId = 0;
            foreach (var bar in result.Bars)
            {
                bar.Size = barWidth;
                var stick = sticks[barId++];
                bar.FillColor = IsGreen(stick)
                    ? Color.FromColor(System.Drawing.Color.DarkCyan)
                    : Color.FromColor(System.Drawing.Color.Red);

                bar.BorderLineWidth = 0;
            }

            ScottPlot.TickGenerators.NumericAutomatic volumeTicksGenerator = new()
            {
                LabelFormatter = VolumeFormater
            };

            VolPlot.Plot.Axes.Right.TickGenerator = volumeTicksGenerator;

            ScottPlot.TickGenerators.NumericAutomatic voidTicksGenerator = new()
            {
                LabelFormatter = (c) => ""
            };

            VolPlot.Plot.Axes.Bottom.TickGenerator = voidTicksGenerator;
            VolPlot.Plot.Axes.AutoScale();
            VolPlot.Plot.Axes.SetLimitsX(startTimeOa, endTimeOa);
            VolPlot.Refresh();

            return result;
        }

        private CandlestickPlot _candlestickPlot;
        private BarPlot _volumePlot;
        private ChartData _chartData;

        /// <summary>
        /// Visualizes the given sticks-and-price data. Must be called in UI thread.
        /// </summary>
        private void VisualizeSticksAndPrice(ChartData chartData)
        {
            if (chartData == null || _currentTimeFrame == null ||
                _currentExchangeData == null ||
                chartData.ExchangeStamp != _currentExchangeData.Stamp ||
                chartData.TimeFrameStamp != _currentTimeFrame.Stamp)
                return;

            _chartData = chartData;
            _candlestickPlot = BuildCandleSticks(chartData);
            _volumePlot = BuildVolumeChart(chartData);

            var priceData = chartData.Price;
            if (priceData != null)
            {
                Price = $"{priceData.Price:0.#####}";
                PriceColor = chartData.IsPriceUp() ? "Green" : "Red";
                PriceTime = priceData.Timestamp?.ToLocalTime().ToString(CultureInfo.InvariantCulture) ??
                            DateTime.Now.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Ticks formater for the vertical axes of the volume chart.
        /// </summary>
        private static string VolumeFormater(double position)
        {
            if (position < 1e3)
                return ((int)position).ToString();

            if (position < 1e6)
                return $"{(int)(position / 1e3)}K";

            if (position < 1e9)
                return $"{(int)(position / 1e6)}M";

            if (position < 1e12)
                return $"{(int)(position / 1e9)}B";
            
            if (position < 1e15)
                return $"{(int)(position / 1e12)}T";

            return "$$$";
        }

        /// <summary>
        /// Method to update chart asynchronously
        /// </summary>
        private void UpdateChartInBackground()
        {
            Task.Run(() =>
            {
                var sticksAndPrice = RetrieveSticksAndPrice();
                Dispatcher.BeginInvoke(() => VisualizeSticksAndPrice(sticksAndPrice));
            });
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public ExchangeChartControl()
        {
            InitializeComponent();

            InitializePlots();

            AvailableTimeIntervals =
                new ObservableCollection<KlineInterval>(Enum.GetValues(typeof(KlineInterval)).Cast<KlineInterval>().
                    Where(x => x != KlineInterval.OneSecond));
            
            Connect();
            _updateTimer = new Timer(x => UpdateChartInBackground(),
                new AutoResetEvent(false), 1000, 1000);

            MainPlot.MouseMove += MainPlot_MouseMove;

            VolPlot.MouseMove += VolPlot_MouseMove;
        }

        /// <summary>
        /// Initializes plot controls.
        /// </summary>
        private void InitializePlots()
        {
            MainPlot.Interaction.Disable();
            MainPlot.Plot.Axes.Left.SetTicks([], []);

            VolPlot.Interaction.Disable();
            VolPlot.Plot.Axes.Left.SetTicks([], []);
            VolPlot.Plot.Axes.Bottom.SetTicks([], []);

            var padding = new PixelPadding(40, 70, 40, 10);
            MainPlot.Plot.Layout.Fixed(padding);
            VolPlot.Plot.Layout.Fixed(padding);
        }

        /// <summary>
        /// Mouse-move event handler for the main plot.
        /// </summary>
        private void MainPlot_MouseMove(object sender, MouseEventArgs e)
        {
            DisplayTipInfo(InfoTip, MainPlot, _candlestickPlot, _chartData,
                (t, p) => _chartData.GetStickId(t, p), e, showBelowPointer: true);
        }

        /// <summary>
        /// Mouse-move event handle for the volume plot.
        /// </summary>
        private void VolPlot_MouseMove(object sender, MouseEventArgs e)
        {
            DisplayTipInfo(InfoTip, VolPlot, _volumePlot, _chartData,
                (t, p) => _chartData.GetVolumeBarId(t, p),  e, showBelowPointer: false);
        }

        /// <summary>
        /// Visualizes tip information of the corresponding popup control.
        /// </summary>
        private void DisplayTipInfo(Popup popup, WpfPlot plot, IPlottable chart, ChartData data,
            Func<double, double, int> stickIdExtractor, MouseEventArgs e, bool showBelowPointer)
        {
            if (chart == null || data == null)
            {
                popup.IsOpen = false;
                return;
            }

            var pixel = plot.GetPlotPixelPosition(e);
            var dataPt = plot.Plot.GetCoordinates(pixel, xAxis: chart.Axes.XAxis, yAxis: chart.Axes.YAxis);
            var stickId = stickIdExtractor(dataPt.X, dataPt.Y);

            if (stickId < 0)
            {
                popup.IsOpen = false;
                return;
            }
           
            var stick = data.Sticks[stickId];
            var volume = data.TradeVolumeData[stickId];

            InfoTipString = $"{stick.DateTime.ToShortDateString()}/{stick.DateTime.ToShortTimeString()}\n" +
                           $"O: {stick.Open:0.#####} USDT\n" +
                           $"C: {stick.Close:0.#####} USDT\n" +
                           $"L: {stick.Low:0.#####} USDT\n" +
                           $"H: {stick.High:0.#####} USDT\n" +
                           $"V: {volume:0.##} USDT";

            var position = e.GetPosition(plot);
            popup.PlacementTarget = plot;
            popup.HorizontalOffset = position.X + 20;
            popup.VerticalOffset = position.Y + (showBelowPointer ? 20 : -100);

            if (!popup.IsOpen) popup.IsOpen = true;
        }

        /// <summary>
        /// "Connects" to Binance server.
        /// </summary>
        private void Connect()
        {
            Task.Run(() =>
            {
                var binance = new BAnalyzerCore.Binance();
                return binance;
            }).ContinueWith(async (t) =>
            {
                _client = t.Result;
                Symbols = new ObservableCollection<string>(await _client.GetSymbols());
                Ready?.Invoke(this);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public event Action<ExchangeChartControl> Ready = null!;

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
            
            _client?.Dispose();
            _client = null;
        }
    }
}
