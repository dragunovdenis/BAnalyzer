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

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BAnalyzer.Controllers;
using BAnalyzer.DataStructures;
using Binance.Net.Enums;
using Microsoft.Win32;

namespace BAnalyzer.Controls;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    private readonly IApplicationSettings _settings;

    /// <summary>
    /// Application settings.
    /// </summary>
    public IApplicationSettings Settings
    {
        get => _settings;

        init
        {
            _settings = value;
            _settings.PropertyChanged += Settings_PropertyChanged;
        }
    }

    /// <summary>
    /// Property-changed even handler of the settings instance.
    /// </summary>
    private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(_settings.ControlSynchronization))
        {
            if (_settings.ControlSynchronization)
                SynchronizeSettings(_exchangeControls.First().Settings);

            _chartSyncController.SynchronizationEnabled = _settings.ControlSynchronization;
        } else if (e.PropertyName is nameof(_settings.DarkMode))
        {
            ApplyTheme();
        }
    }

    private AssetAnalysisWindow _assetAnalysisWindow;

    private AssetAnalysisWindow AssetAnalysisWindowInstance => _assetAnalysisWindow ?? ActivateAnalysisWindow();

    /// <summary>
    /// Activates the "asset analysis" window.
    /// </summary>
    private AssetAnalysisWindow ActivateAnalysisWindow()
    {
        if (_assetAnalysisWindow != null)
            return _assetAnalysisWindow;

        var exchangeSymbols = GetExchangeSymbols();
        var settings = Settings.ExchangeSettings;

        var settingsId = "AssetAnalysisWindow";
        if (!settings.ContainsKey(settingsId))
        {
            settings[settingsId] = SetupExchangeSettings("");
            settings[settingsId].PropertyChanged += ExchangeSettings_PropertyChanged;
        }

        if (Settings.ControlSynchronization) SynchronizeSettings(null);

        return _assetAnalysisWindow = new AssetAnalysisWindow(BinanceClientController.Client,
            exchangeSymbols, Settings.Assets, settings[settingsId], _chartSyncController)
        {
            Owner = Application.Current.MainWindow
        };
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    public MainWindow()
    {
        Settings = ApplicationController.Instance.ApplicationSettings;
        _chartSyncController.SynchronizationEnabled = Settings.ControlSynchronization;

        InitializeComponent();

        _exchangeControls = SetUpExchangeControls();

        foreach (var s in Settings.ExchangeSettings)
            s.Value.PropertyChanged += ExchangeSettings_PropertyChanged;

        ApplyTheme();

        Closing += MainWindow_Closing;
    }

    /// <summary>
    /// Closing event.
    /// </summary>
    private void MainWindow_Closing(object sender, CancelEventArgs e) => ApplicationController.Instance.SaveSettings();

    /// <summary>
    /// Property changed handler of all exchange controls.
    /// </summary>
    private void ExchangeSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is not ExchangeSettings source || !Settings.ControlSynchronization)
            return;

        SynchronizeSettings(source);
    }

    /// <summary>
    /// Synchronizes settings of all the exchange controls with the given <param name="source"/>
    /// </summary>
    private void SynchronizeSettings(ExchangeSettings source)
    {
        var sourceLocal = source ?? Settings.ExchangeSettings.Values.FirstOrDefault();

        if (sourceLocal == null)
            return;

        foreach (var ec in Settings.ExchangeSettings)
            ec.Value.Assign(sourceLocal, excludeExchangeDescriptor: true);
    }

    private readonly IReadOnlyList<string> _defaultExchangeStockNames =
        ["BTCUSDT", "ETHUSDT", "SOLUSDT", "RVNUSDT"];
        
    private readonly IList<CryptoExchangeControl> _exchangeControls;

    /// <summary>
    /// Creates (almost) default exchange settings.
    /// </summary>
    private static ExchangeSettings SetupExchangeSettings(string exchangeDescriptor) => new()
        {
            ExchangeDescriptor = exchangeDescriptor,
            CurrentAnalysisIndicator = AnalysisIndicatorType.None,
            MainAnalysisWindow = 10,
            StickRange = 75,
            TimeDiscretization = KlineInterval.FifteenMinutes,
        };

    /// <summary>
    /// Returns collection of exchange symbols.
    /// </summary>
    private static string[] GetExchangeSymbols() => BinanceClientController.
        ExchangeSymbols.Where(x => x.EndsWith("USDT")).ToArray();

    private readonly ChartSynchronizationController _chartSyncController = new();

    /// <summary>
    /// Sets up the exchange controls and returns them.
    /// </summary>
    private IList<CryptoExchangeControl> SetUpExchangeControls()
    {
        if (_defaultExchangeStockNames.Count != 4)
            throw new InvalidOperationException("Unexpected number of stock names");
            
        var result = new List<CryptoExchangeControl>();

        var exchangeSymbols = GetExchangeSymbols();

        var settings = ApplicationController.Instance.ApplicationSettings.ExchangeSettings;

        for (var rowId = 0; rowId < 2; rowId++)
        for (var colId = 0; colId < 2; colId++)
        {
            var settingsId = $"ExchangeControl_{rowId}:{colId}";

            if (!settings.ContainsKey(settingsId)) 
                settings.Add(settingsId, SetupExchangeSettings(_defaultExchangeStockNames[result.Count]));

            var exSettings = settings[settingsId];

            var exchangeControl = new CryptoExchangeControl(BinanceClientController.Client,
                exchangeSymbols, exSettings, _chartSyncController)
            {
                BorderThickness = new Thickness(2, 2, colId == 1 ? 2 : 0, rowId == 1 ? 2 : 0),
            };

            Grid.SetColumn(exchangeControl, colId);
            Grid.SetRow(exchangeControl, rowId);
            ChartGrid.Children.Add(exchangeControl);
            result.Add(exchangeControl);
        }

        return result;
    }

    /// <summary>
    /// Applies current theme to the whole application.
    /// </summary>
    private void ApplyTheme()
    {
        AppThemeController.ApplyTheme(new Uri(Settings.DarkMode ? "../Themes/Dark.xaml" : "../Themes/Light.xaml",
            UriKind.Relative));

        foreach (var ec in _exchangeControls)
            ec.DarkMode = Settings.DarkMode;
    }
    
    /// <summary>
    /// Closing event handler.
    /// </summary>
    private void MainWindow_OnClosing(object sender, CancelEventArgs e)
    {
        foreach (var ec in _exchangeControls)
            ec.Dispose();

        _exchangeControls.Clear();
        _assetAnalysisWindow?.Close();
    }

    /// <summary>
    /// Minimize button click event handler.
    /// </summary>
    private void Minimize_OnClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    /// <summary>
    /// Maximize button click event handler.
    /// </summary>
    private void Maximize_OnClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;

    /// <summary>
    /// Close button click event handler.
    /// </summary>
    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Header left mouse button click event handler.
    /// </summary>
    private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
        else
            DragMove();
    }

    /// <summary>
    /// On click event handler of the corresponding menu item.
    /// </summary>
    private void ShowAssetAnalysisMenuItem_OnClick(object sender, RoutedEventArgs e) => AssetAnalysisWindowInstance.Show();

    /// <summary>
    /// Handles click of the "save cache" menu item.
    /// </summary>
    private async void SaveCacheMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = new OpenFolderDialog();

            if (folder.ShowDialog(this) == true)
                await BinanceClientController.Client.SaveCacheAsync(folder.FolderName);
        }
        catch (Exception) { /*ignored*/ }
    }

    /// <summary>
    /// Handles click of the "load cache" menu item.
    /// </summary>
    private async void LoadCacheMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = new OpenFolderDialog();

            if (folder.ShowDialog(this) == true)
                await BinanceClientController.Client.LoadCacheAsync(folder.FolderName);
        }
        catch (Exception) { /*ignored*/ }
    }

    /// <summary>
    /// Handles click of the "about" menu item.
    /// </summary>
    private void AboutMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        aboutWindow.ShowDialog();
    }
}