//Copyright (c) 2026 Denys Dragunov, dragunovdenis@gmail.com
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

using System.Windows;
using System.Windows.Input;
using BAnalyzer.Controllers;
using BAnalyzer.DataStructures;

namespace BAnalyzer.Controls;

/// <summary>
/// Interaction logic for TradeSimWindow.xaml
/// </summary>
public partial class TradeSimWindow
{
    /// <summary>
    /// Constructor.
    /// </summary>
    public TradeSimWindow(IMultiExchange client, TradeSimSettings settings, TradeSimController controller)
    {
        InitializeComponent();
        SimControl.Initialize(client, settings, controller);
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
    /// "Close" button click event handler: the window is only hidden, so that it can be shown again.
    /// </summary>
    private void Close_OnClick(object sender, RoutedEventArgs e) => Hide();

    /// <summary>
    /// Handles "on-closed" event (e.g. caused by "Alt+F4" or by the closing of the main window).
    /// </summary>
    private void TradeSimWindow_OnClosed(object sender, EventArgs e) => SimControl.Deactivate();

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
    /// Activates the control when the window becomes visible and deactivates it when the window gets hidden,
    /// so that a hidden window does not poll the exchanges.
    /// </summary>
    private void TradeSimWindow_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            SimControl.Activate();
        else
            SimControl.Deactivate();
    }
}
