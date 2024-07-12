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
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using BAnalyzer.DataStructures;
using BAnalyzer.Utils;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WPF;

namespace BAnalyzer.Controls;

/// <summary>
/// Interaction logic for ExchangeChartControl.xaml
/// </summary>
public partial class ExchangeChartControl : UserControl, INotifyPropertyChanged
{
    private string _infoTipString;

    /// <summary>
    /// Field bound to the info-popup.
    /// </summary>
    public string InfoTipString
    {
        get => _infoTipString;
        private set => SetField(ref _infoTipString, value);
    }

    private CandlestickPlot _candlestickPlot;
    private BarPlot _volumePlot;
        
    private ChartData _chartData;

    /// <summary>
    /// Builds candle-sticks chart based on the given data.
    /// </summary>
    private CandlestickPlot BuildCandleSticks(ChartData chartData)
    {
        MainPlot.Plot.Clear();

        if (chartData == null || chartData.Sticks.Count == 0)
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

        if (chartData == null || chartData.Sticks.Count == 0)
        {
            VolPlot.Refresh();
            return null;
        }

        var sticks = chartData.Sticks;
        var result = VolPlot.Plot.Add.Bars(sticks.Select(x => x.DateTime.ToOADate()).ToList(),
            chartData.TradeVolumeData);
        result.Axes.YAxis = VolPlot.Plot.Axes.Right;

        var startTimeOa = chartData.GetBeginTime().ToOADate();
        var endTimeOa = chartData.GetEndTime().ToOADate();
        var barWidth = ((endTimeOa - startTimeOa) / result.Bars.Count()) * 0.8;

        var barId = 0;
        foreach (var bar in result.Bars)
        {
            bar.Size = barWidth;
            var stick = sticks[barId++];
            bar.FillColor = stick.IsGreen()
                ? Color.FromColor(System.Drawing.Color.DarkCyan)
                : Color.FromColor(System.Drawing.Color.Red);

            bar.BorderLineWidth = 0;
        }

        ScottPlot.TickGenerators.NumericAutomatic volumeTicksGenerator = new()
        {
            LabelFormatter = DataFormatter.FloatToCompact
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

    /// <summary>
    /// Updates plots based on the current chart data.
    /// </summary>
    public void UpdatePlots(ChartData chartData)
    {
        _chartData = chartData;
        _candlestickPlot = BuildCandleSticks(_chartData);
        _volumePlot = BuildVolumeChart(_chartData);
        ApplyColorPalettes();
    }
        
    /// <summary>
    /// Constructor.
    /// </summary>
    public ExchangeChartControl()
    {
        InitializeComponent();
        InitializePlots();

        MainPlot.MouseMove += MainPlot_MouseMove;
        VolPlot.MouseMove += VolPlot_MouseMove;
    }

    /// <summary>
    /// Applies current color palette to all the plots.
    /// </summary>
    private void ApplyColorPalettes()
    {
        ScottPlotPalette.Apply(MainPlot.Plot);
        MainPlot.Refresh();
        ScottPlotPalette.Apply(VolPlot.Plot);
        VolPlot.Refresh();
    }

    /// <summary>
    /// The corresponding dependency property.
    /// </summary>
    public static readonly DependencyProperty DarkModeProperty =
        DependencyProperty.Register(
            name: "DarkMode",
            propertyType: typeof(bool),
            ownerType: typeof(ExchangeChartControl),
            typeMetadata: new FrameworkPropertyMetadata(defaultValue: true, DarkModeValueChanged));

    private static void DarkModeValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is ExchangeChartControl control)
        {
            if (eventArgs.NewValue != eventArgs.OldValue)
                control.ApplyColorPalettes();
        }
    }

    /// <summary>
    /// Switches between white/black color modes.
    /// </summary>
    public bool DarkMode
    {
        get => (bool)GetValue(DarkModeProperty);
        set => SetValue(DarkModeProperty, value);
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
            (t, p) => _chartData.GetVolumeBarId(t, p), e, showBelowPointer: false);
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
                        $"V: {DataFormatter.FloatToCompact(volume)} USDT";

        var position = e.GetPosition(plot);
        popup.PlacementTarget = plot;
        popup.HorizontalOffset = position.X + 20;
        popup.VerticalOffset = position.Y + (showBelowPointer ? 20 : -100);

        if (!popup.IsOpen) popup.IsOpen = true;
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