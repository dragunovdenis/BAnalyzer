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
using Binance.Net.Enums;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

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
    /// Contains all the symbols that are already present in the cache.
    /// </summary>
    public ObservableCollection<string> CachedSymbols { get; } = new();

    /// <summary>
    /// The cache.
    /// </summary>
    private readonly BinanceCache _cache = new();

    private bool _processing;

    /// <summary>
    /// Indicates that processing is ongoing.
    /// </summary>
    public bool Processing
    {
        get => _processing;
        set => SetField(ref _processing, value);
    }

    /// <summary>
    /// The corresponding dependency property.
    /// </summary>
    public static readonly DependencyProperty ClientProperty = DependencyProperty.
        Register(name: nameof(Client), propertyType: typeof(BAnalyzerCore.Binance), ownerType: typeof(CacheManagerControl),
            typeMetadata: new FrameworkPropertyMetadata(defaultValue: null, OnClientChange));

    /// <summary>
    /// Handles change of the Binance client object.
    /// </summary>
    private static void OnClientChange(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        if (obj is CacheManagerControl cacheManager)
        {
            cacheManager.AvailableSymbols.Clear();

            if (args.NewValue is BAnalyzerCore.Binance client)
            {
                var selectedSymbolIdx = 0;
                var symbols = client.GetSymbols();
                for (var sIdx = 0; sIdx < symbols.Count; sIdx++)
                {
                    var symbol = symbols[sIdx];
                    cacheManager.AvailableSymbols.Add(symbol);
                    if (symbol == "BTCUSDT")
                        selectedSymbolIdx = sIdx;
                }

                cacheManager.AvailableSymbolsBox.SelectedIndex = selectedSymbolIdx;
            }
        }
    }

    /// <summary>
    /// Binance client object.
    /// </summary>
    public BAnalyzerCore.Binance Client
    {
        get => (BAnalyzerCore.Binance)GetValue(ClientProperty);
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
        if (Client == null) throw new InvalidOperationException("Invalid client instance");

        var client = Client;
        Processing = true;

        string ReportProgress(string symbol, KlineInterval granularity, DateTime begin, DateTime end)
        {
            return $"{symbol} / {granularity.ToString()} / " +
                   $"[{begin.ToString(CultureInfo.InvariantCulture)} : {end.ToString(CultureInfo.InvariantCulture)}]";
        }

        int updateCounter = 0;

        try
        {
            await Task.Run(async () =>
            {
                for (var symbolId = PendingSymbols.Count - 1; symbolId >= 0; symbolId--)
                {
                    var symbol = PendingSymbols[symbolId];
                    await client.ReadOutData(symbol, _cache, (g, begin, end) =>
                    {
                        if (updateCounter++ % 3 == 0)
                            Dispatcher.BeginInvoke(() => ProgressInfo.Text = ReportProgress(symbol, g, begin, end));
                    });

                    await Dispatcher.BeginInvoke(() =>
                    {
                        PendingSymbols.Remove(symbol);
                        CachedSymbols.Add(symbol);
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
    /// Handles "click" event of the "save cache" button.
    /// </summary>
    private async void SaveCacheButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = new OpenFolderDialog();

            try
            {
                if (folder.ShowDialog(Application.Current.MainWindow) != true) return;

                ProgressInfo.Text = "Saving...";
                Processing = true;
                await Task.Run(() => _cache.Save(folder.FolderName));
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
}