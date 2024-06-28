﻿//Copyright (c) 2024 Denys Dragunov, dragunovdenis@gmail.com
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
using System.Windows.Data;
using System.Windows.Input;
using BAnalyzerCore;

namespace BAnalyzer
{
    /// <summary>
    /// Interaction logic for ExchangeChartControl.xaml
    /// </summary>
    public partial class ExchangeChartControl : INotifyPropertyChanged
    {
        private BAnalyzerCore.Binance _client = null!;

        private ObservableCollection<string> _symbols = null!;

        private readonly Timer _updateTimer;
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

        private string _priceTime = null!;

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
        /// Sticks and price data struct.
        /// </summary>
        private class ChartData(List<OHLC> sticks, BinancePrice price, DateTime exchangeStamp, DateTime timeFrameStamp)
        {
            /// <summary>
            /// Sticks.
            /// </summary>
            public List<OHLC> Sticks { get; } = sticks;

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
            /// Discretization of measurements.
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
            var (timeFrame, exchange) =
                Dispatcher.Invoke(() => (_currentTimeFrame, _currentExchangeData));

            if (timeFrame == null! || timeFrame.Discretization == default ||
                exchange == null! || exchange.ExchangeDescriptor is null or "" || _client == null!)
                return null!;

            var sticks = _client.GetCandleSticks(timeFrame.Begin, timeFrame.End,
                timeFrame.Discretization, exchange.ExchangeDescriptor).Result;

            var price = _client.GetCurrentPrice(exchange.ExchangeDescriptor).Result;

            var plotSticks = sticks.Select(x => new OHLC()
            {
                Close = (double)x.ClosePrice,
                Open = (double)x.OpenPrice,
                High = (double)x.HighPrice,
                Low = (double)x.LowPrice,
                TimeSpan = timeFrame.Discretization.ToTimeSpan(),
                DateTime = x.OpenTime.ToLocalTime()
            }).ToList();

            return new ChartData(plotSticks, price, exchange.Stamp, timeFrame.Stamp);
        }

        /// <summary>
        /// Visualizes the given sticks-and-price data. Must be called in UI thread.
        /// </summary>
        private void VisualizeSticksAndPrice(ChartData chartData)
        {
            if (chartData == null! || _currentTimeFrame == null! ||
                _currentExchangeData == null! ||
                chartData.ExchangeStamp != _currentExchangeData.Stamp ||
                chartData.TimeFrameStamp != _currentTimeFrame.Stamp)
                return;

            Plot.Plot.Clear();
            var sticks = chartData.Sticks;
            if (sticks.Count > 0)
            {
                var signal = Plot.Plot.Add.Candlestick(sticks);
                signal.Axes.YAxis = Plot.Plot.Axes.Right;
                Plot.Plot.Axes.DateTimeTicksBottom();
                var startTimeOa = sticks.First().DateTime.ToOADate();
                var endTimeOa = sticks.Last().DateTime.Add(sticks.Last().TimeSpan).ToOADate();

                Plot.Plot.Axes.SetLimitsX(startTimeOa, endTimeOa);
            }

            var priceData = chartData.Price;
            if (priceData != null!)
            {
                Price = $"{priceData.Price.ToString(CultureInfo.InvariantCulture)} USDT";
                PriceTime = priceData.Timestamp?.ToLocalTime().ToString(CultureInfo.InvariantCulture) ??
                            string.Empty;
            }

            Plot.Refresh();
        }

        private void UpdateChartInBackground()
        {
            Task.Run(() =>
            {
                var sticksAndPrice = RetrieveSticksAndPrice();
                Dispatcher.BeginInvoke(() => VisualizeSticksAndPrice(sticksAndPrice));
            });
        }

        public ExchangeChartControl()
        {
            InitializeComponent();
            Plot.Interaction.Disable();
            Plot.Plot.Axes.Left.SetTicks(Array.Empty<double>(), Array.Empty<string>()); ;
            AvailableTimeIntervals =
                new ObservableCollection<KlineInterval>(Enum.GetValues(typeof(KlineInterval)).Cast<KlineInterval>().
                    Where(x => x != KlineInterval.OneSecond));
            Connect();
            _updateTimer = new Timer(x => UpdateChartInBackground(),
                new AutoResetEvent(false), 1000, 1000);
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

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Property changed handler.
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Field setter.
        /// </summary>
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
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

    }
}
