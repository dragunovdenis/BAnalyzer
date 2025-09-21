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

using BAnalyzerCore.Cache;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BAnalyzer.DataStructures;
using BAnalyzerCore.DataStructures;

namespace BAnalyzer.Controls;

/// <summary>
/// Interaction logic for CacheManagerControl.xaml
/// </summary>
public partial class CacheManagerControl :INotifyPropertyChanged
{
    /// <summary>
    /// Contains all the available symbols provided by the Binance client.
    /// </summary>
    public ObservableCollection<string> AvailableSymbols { get; } = new();

    /// <summary>
    /// Contains all the symbols that needs to be cached.
    /// </summary>
    public ObservableCollection<string> PendingSymbols { get; } = new();

    /// <summary>
    /// Essential information about cache interval of a certain exchange symbol.
    /// </summary>
    public record CachedTimeIntervalInfo(TimeGranularity Granularity, DateTime Begin, DateTime End, double SizeBytes);

    /// <summary>
    /// Essential information about a cached symbol.
    /// </summary>
    public record CachedSymbolInfo(string Symbol, ObservableCollection<CachedTimeIntervalInfo> Items);

    /// <summary>
    /// Contains all the symbols that are already present in the cache.
    /// </summary>
    public ObservableCollection<CachedSymbolInfo> CachedSymbols { get; } = new();

    /// <summary>
    /// The cache.
    /// </summary>
    private BinanceCache _cache = new();

    /// <summary>
    /// Visualizes cached data in UI.
    /// </summary>
    private void VisualizeCachedData()
    {
        CachedSymbols.Clear();
        foreach (var symbol in _cache.CachedSymbols)
        {
            var asset = _cache.GetAssetViewThreadSafe(symbol);

            var granularityItems = new List<CachedTimeIntervalInfo>();

            foreach (var granularity in asset.Granularities)
            {
                var grid = asset.GetGridThreadSafe(granularity);
                granularityItems.Add(new CachedTimeIntervalInfo(granularity, grid.Begin, grid.End, grid.SizeInBytes));
            }

            CachedSymbols.Add(new CachedSymbolInfo(symbol,
                new ObservableCollection<CachedTimeIntervalInfo>(granularityItems.OrderBy(x => x.Granularity.Seconds))));
        }
    }

    private bool _processing;

    /// <summary>
    /// Indicates that processing is ongoing.
    /// </summary>
    public bool Processing
    {
        get => _processing;
        set => SetField(ref _processing, value);
    }

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
    /// The corresponding dependency property.
    /// </summary>
    public static readonly DependencyProperty ClientProperty = DependencyProperty.
        Register(name: nameof(Client), propertyType: typeof(IMultiExchange), ownerType: typeof(CacheManagerControl),
            typeMetadata: new FrameworkPropertyMetadata(defaultValue: null, OnClientChange));

    /// <summary>
    /// Handles change of the Binance client object.
    /// </summary>
    private static void OnClientChange(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        if (obj is CacheManagerControl cacheManager)
        {
            cacheManager.AvailableSymbols.Clear();

            if (args.NewValue is IMultiExchange client)
            {
                var selectedSymbolIdx = 0;
                var symbols = client.Symbols;
                for (var sIdx = 0; sIdx < symbols.Count; sIdx++)
                {
                    var symbol = symbols[sIdx];
                    cacheManager.AvailableSymbols.Add(symbol);
                    if (symbol == "BTCUSDT")
                        selectedSymbolIdx = sIdx;
                }

                cacheManager.Exchanges = new ObservableCollection<ExchangeId>(client.Exchanges);
                cacheManager.AvailableSymbolsBox.SelectedIndex = selectedSymbolIdx;
            }
        }
    }

    /// <summary>
    /// Binance client object.
    /// </summary>
    public IMultiExchange Client
    {
        get => (IMultiExchange)GetValue(ClientProperty);
        set => SetValue(ClientProperty, value);
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    public CacheManagerControl() => InitializeComponent();

    /// <summary>
    /// Adds the selected symbol to the list of "pending" symbols.
    /// </summary>
    private void AddSelectedSymbolToPendingList()
    {
        var selectedSymbolIdx = AvailableSymbolsBox.SelectedIndex;

        if (selectedSymbolIdx == -1)
            return;

        var symbol = AvailableSymbols[selectedSymbolIdx];

        if (PendingSymbols.Contains(symbol)) return;

        PendingSymbols.Add(symbol);
    }

    /// <summary>
    /// Handles the "click" event of the button that adds symbols to the "pending" list.
    /// </summary>
    private void AddPendingSymbolButton_OnClick(object sender, RoutedEventArgs e) => AddSelectedSymbolToPendingList();

    /// <summary>
    /// Handles "key-down" event of the combobox of available symbols.
    /// </summary>
    private void AvailableSymbolsBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter) AddSelectedSymbolToPendingList();
    }

    /// <summary>
    /// Handles the "click" event of the "download" button.
    /// </summary>
    private async void DownloadButton_OnClick(object sender, RoutedEventArgs e)
    {
        var client = Client;
        Processing = true;

        string ReportProgress(string symbol, TimeGranularity granularity, DateTime begin, DateTime end, double dataRateKbSec, TimeSpan elapsedTime)
        {
            return $"{symbol} / {granularity.ToString()} / " +
                   $"[{begin.ToString(CultureInfo.InvariantCulture)} : {end.ToString(CultureInfo.InvariantCulture)}] / {dataRateKbSec} Kb/Sec / " +
                   $@"Elapsed Time : {elapsedTime:hh\:mm\:ss}";
        }

        var exchangeId = (ExchangeId)AvailableExchangesBox.SelectedValue;

        int updateCounter = 0;

        try
        {
            await Task.Run(async () =>
            {
                for (var symbolId = PendingSymbols.Count - 1; symbolId >= 0; symbolId--)
                {
                    var sw = new Stopwatch();
                    sw.Restart();
                    long timePrevMs = 0;
                    long bytesLoadedPrev = 0;

                    var symbol = PendingSymbols[symbolId];
                    await client[exchangeId].ReadOutData(symbol, _cache,
                        (g, begin, end, bytesLoaded) =>
                    {
                        var kBytesLoadedDiff = (bytesLoaded - bytesLoadedPrev) / 1024.0;
                        bytesLoadedPrev = bytesLoaded;

                        var currentTime = sw.ElapsedMilliseconds;
                        var elapsedTimeSec = (currentTime - timePrevMs) / 1000.0;
                        timePrevMs = currentTime;

                        var dataRateKbPerSec = Math.Round(kBytesLoadedDiff / elapsedTimeSec, 2);

                        if (updateCounter++ % 3 == 0)
                            Dispatcher.BeginInvoke(() => ProgressInfo.Text =
                                ReportProgress(symbol, g, begin, end, dataRateKbPerSec, sw.Elapsed));
                    });

                    await Dispatcher.BeginInvoke(() =>
                    {
                        PendingSymbols.Remove(symbol);
                        VisualizeCachedData();
                    });
                }
            });
        }
        finally
        {
            Processing = false;
            ProgressInfo.Text = "";
        }
    }

    /// <summary>
    /// Handles "click" event of the "load cache" button.
    /// </summary>
    private async void LoadCacheButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select folder with cache data."
            };

            try
            {
                if (dialog.ShowDialog(Application.Current.MainWindow) != true) return;

                ProgressInfo.Text = "Loading...";
                Processing = true;
                var reportCounter = 0;

                _cache = await Task.Run(() => BinanceCache.Load(dialog.FolderName,
                    (symbol, blockCount, byteCount) =>
                    {
                        if (reportCounter++ % 9 == 0)
                            Dispatcher.BeginInvoke( () => ProgressInfo.Text =
                                $"Loading... {symbol} : {blockCount} blocks / {byteCount / 1024} Kb");
                    }));
                VisualizeCachedData();
            }
            finally
            {
                Processing = false;
            }
        }
        catch (Exception) { /*ignored*/ }
    }

    /// <summary>
    /// Handles "click" event of the "save cache" button.
    /// </summary>
    private async void SaveCacheButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select folder to save cache data."
            };

            try
            {
                if (dialog.ShowDialog(Application.Current.MainWindow) != true) return;

                ProgressInfo.Text = "Saving...";
                Processing = true;
                var reportCounter = 0;

                await Task.Run(() => _cache.Save(dialog.FolderName,
                    (symbol, blockCount, byteCount) =>
                    {
                        if (reportCounter++ % 9 == 0)
                            Dispatcher.BeginInvoke(() => ProgressInfo.Text =
                                $"Saving... {symbol} : {blockCount} blocks / {byteCount / 1024} Kb");
                    }));
            }
            finally
            {
                Processing = false;
            }
        }
        catch (Exception) { /*ignored*/ }
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
    /// Handles mouse wheel preview event of the scroll viewer control
    /// so that we can scroll while mouse pointer is over the content
    /// (and not only over the scroll viewer bar).
    /// </summary>
    private void ScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var scv = (ScrollViewer)sender;
        scv.ScrollToVerticalOffset(scv.VerticalOffset - e.Delta / 7.0);
        e.Handled = true;
    }
}