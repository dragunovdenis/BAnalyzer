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
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BAnalyzer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            foreach (var ec in GetExchangeControls())
                ec.PropertyChanged += Exchange_PropertyChanged;
        }

        /// <summary>
        /// Property changed handler of all exchange controls.
        /// </summary>
        private void Exchange_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Exchange0.CurrentTimeInterval) && SyncIntervals)
            {
                var newInterval = (sender as ExchangeChartControl)!.CurrentTimeInterval;

                foreach (var ec in GetExchangeControls())
                    ec.CurrentTimeInterval = newInterval;
            }
        }

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
        /// Returns collection of all the exchange controls.
        /// </summary>
        private IEnumerable<ExchangeChartControl> GetExchangeControls() =>
            new[] { Exchange0, Exchange1, Exchange2, Exchange3 };

        /// <summary>
        /// "Ready" event handler.
        /// </summary>
        private void Exchange_OnReady(ExchangeChartControl sender)
        {
            if  (sender == Exchange0)
                Exchange0.SelectedSymbol = "BTCUSDT";

            if (sender == Exchange1)
                Exchange1.SelectedSymbol = "ETHUSDT";

            if (sender == Exchange2)
                Exchange2.SelectedSymbol = "SOLUSDT";

            if (sender == Exchange3)
                Exchange3.SelectedSymbol = "RVNUSDT";
        }


        /// <summary>
        /// Sets given interval descriptor to all the exchange controls.
        /// </summary>
        private void SynchronizeIntervals(KlineInterval interval)
        {
            foreach (var ec in GetExchangeControls())
                ec.CurrentTimeInterval = interval;
        }

        private bool _syncIntervals = true;

        /// <summary>
        /// Flag determining whether time intervals of all the exchange controls should be synchronized.
        /// </summary>
        public bool SyncIntervals
        {
            get => _syncIntervals;
            set
            {
                if (SetField(ref _syncIntervals, value))
                    SynchronizeIntervals(Exchange0.CurrentTimeInterval);
            }
        }
    }
}