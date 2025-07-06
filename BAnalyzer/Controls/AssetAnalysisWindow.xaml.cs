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
    /// Constructor.
    /// </summary>
    public AssetAnalysisWindow(BAnalyzerCore.Binance client, IList<string> exchangeSymbols,
        ObservableCollection<AssetRecord> assets, ExchangeSettings settings,
        IChartSynchronizationController syncController)
    {
        InitializeComponent();
        AnalysisControl.Activate(client, exchangeSymbols, assets, settings, syncController);
    }

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
    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Handles "on-closed" event.
    /// </summary>
    private void AssetAnalysisWindow_OnClosed(object sender, EventArgs e) => AnalysisControl.Deactivate();

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
}