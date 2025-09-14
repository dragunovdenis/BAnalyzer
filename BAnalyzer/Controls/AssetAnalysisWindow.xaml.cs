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

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using BAnalyzer.Controllers;
using BAnalyzer.DataStructures;

namespace BAnalyzer.Controls;

/// <summary>
/// Interaction logic for AssetAnalysisWindow.xaml
/// </summary>
public partial class AssetAnalysisWindow
{
    /// <summary>
    /// State of the window.
    /// </summary>
    private record State(
        BAnalyzerCore.ExchangeClient Client,
        IList<string> ExchangeSymbols,
        ObservableCollection<AssetRecord> Assets,
        ExchangeSettings Settings,
        IChartSynchronizationController SyncController);

    private readonly State _sate;

    /// <summary>
    /// Constructor.
    /// </summary>
    public AssetAnalysisWindow(BAnalyzerCore.ExchangeClient client, IList<string> exchangeSymbols,
        ObservableCollection<AssetRecord> assets, ExchangeSettings settings,
        IChartSynchronizationController syncController)
    {
        _sate = new State(client, exchangeSymbols, assets, settings, syncController);

        InitializeComponent();
    }

    /// <summary>
    /// Activates the analysis control.
    /// </summary>
    private void ActivateAnalysisControl() => AnalysisControl.Activate(_sate.Client, _sate.ExchangeSymbols,
        _sate.Assets, _sate.Settings, _sate.SyncController);

    /// <summary>
    /// Deactivates the analysis control.
    /// </summary>
    private void DeactivateAnalysisControl() => AnalysisControl.Deactivate();

    /// <summary>
    /// "Minimize" button click event handler.
    /// </summary>
    private void Minimize_OnClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    /// <summary>
    /// "Maximize" button click event handler.
    /// </summary>
    private void Maximize_OnClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;

    /// <summary>
    /// "Close" button click event handler.
    /// </summary>
    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        AnalysisControl.Deactivate();
        Hide();
    }

    /// <summary>
    /// Handles "on-closed" event.
    /// </summary>
    private void AssetAnalysisWindow_OnClosed(object sender, EventArgs e) => DeactivateAnalysisControl();

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
    /// Handles "visibility-change" event of the window.
    /// </summary>
    private void AssetAnalysisWindow_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            ActivateAnalysisControl();
    }
}