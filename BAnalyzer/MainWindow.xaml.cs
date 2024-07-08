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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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

            _exchangeControls = SetUpExchangeControls();

            foreach (var ec in _exchangeControls)
                ec.PropertyChanged += Exchange_PropertyChanged;
        }

        /// <summary>
        /// Property changed handler of all exchange controls.
        /// </summary>
        private void Exchange_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            CryptoExchangeControl exchange;
            if (e.PropertyName == nameof(exchange.CurrentTimeInterval) && SyncIntervals)
            {
                var newInterval = (sender as CryptoExchangeControl)!.CurrentTimeInterval;

                foreach (var ec in _exchangeControls)
                    ec.CurrentTimeInterval = newInterval;
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

        private readonly IReadOnlyList<string> _defaultExchangeStockNames =
            new[] { "BTCUSDT", "ETHUSDT", "SOLUSDT", "RVNUSDT" };
        
        private readonly IList<CryptoExchangeControl> _exchangeControls;
        
        /// <summary>
        /// Sets up the exchange controls and returns them.
        /// </summary>
        private IList<CryptoExchangeControl> SetUpExchangeControls()
        {
            if (_defaultExchangeStockNames.Count != 4)
                throw new InvalidOperationException("Unexpected number of stock names");
            
            var result = new List<CryptoExchangeControl>();
            
            for (var rowId = 1; rowId < 3; rowId++)
            for (var colId = 0; colId < 2; colId++)
            {
                var exchangeControl = new CryptoExchangeControl()
                {
                    AllowDrop = true,
                    SelectedSymbol = _defaultExchangeStockNames[result.Count],
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1, 1, colId == 1 ? 1 : 0, rowId == 2 ? 1 : 0),
                };
                
                exchangeControl.Ready += Exchange_OnReady;
                exchangeControl.MouseDown += Exchange_OnMouseDown;
                exchangeControl.DragEnter += Exchange_OnDragEnter;

                Grid.SetColumn(exchangeControl, colId);
                Grid.SetRow(exchangeControl, rowId);
                MainGrid.Children.Add(exchangeControl);
                result.Add(exchangeControl);
            }

            return result;
        }

        /// <summary>
        /// "Ready" event handler.
        /// </summary>
        private void Exchange_OnReady(CryptoExchangeControl sender)
        {
            if (sender != null! && _exchangeControls.Contains(sender))
            {
                sender.SelectedSymbol = _defaultExchangeStockNames[_exchangeControls.IndexOf(sender)];
                sender.CurrentTimeInterval = KlineInterval.FifteenMinutes;
            }
        }

        /// <summary>
        /// Sets given interval descriptor to all the exchange controls.
        /// </summary>
        private void SynchronizeIntervals(KlineInterval interval)
        {
            foreach (var ec in _exchangeControls)
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
                    SynchronizeIntervals(_exchangeControls.First().CurrentTimeInterval);
            }
        }

       
        /// <summary>
        /// Mouse down event handler.
        /// </summary>
        private void Exchange_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is CryptoExchangeControl exchange)
                DragDrop.DoDragDrop(exchange, exchange, DragDropEffects.Move);
        }

        /// <summary>
        /// Drop enter event handler.
        /// </summary>
        private void Exchange_OnDragEnter(object sender, DragEventArgs e)
        {
            var exchangeMoving = (CryptoExchangeControl)e.Data.GetData(typeof(CryptoExchangeControl));
            
            if (!(sender is CryptoExchangeControl exchangeWaiting) || exchangeMoving == null ||
                exchangeWaiting == exchangeMoving)
                return;

            var colIdMoving = Grid.GetColumn(exchangeMoving);
            var rowIdMoving = Grid.GetRow(exchangeMoving);

            var colIdWaiting = Grid.GetColumn(exchangeWaiting);
            var rowIdWaiting = Grid.GetRow(exchangeWaiting);

            Grid.SetColumn(exchangeMoving, colIdWaiting);
            Grid.SetRow(exchangeMoving, rowIdWaiting);

            Grid.SetColumn(exchangeWaiting, colIdMoving);
            Grid.SetRow(exchangeWaiting, rowIdMoving);

            (exchangeMoving.BorderThickness, exchangeWaiting.BorderThickness) =
                (exchangeWaiting.BorderThickness, exchangeMoving.BorderThickness);
        }

        /// <summary>
        /// Closing event handler.
        /// </summary>
        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            foreach (var ec in _exchangeControls)
                ec.Dispose();

            _exchangeControls.Clear();
        }
    }
}