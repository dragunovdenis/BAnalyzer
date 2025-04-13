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
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using BAnalyzer.Controllers;
using BAnalyzer.DataStructures;
using BAnalyzer.Utils;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WPF;

namespace BAnalyzer.Controls;

/// <summary>
/// Interaction logic for ExchangeChartControl.xaml
/// </summary>
public partial class ExchangeChartControl : INotifyPropertyChanged
{
    private bool _showVolumePlot = true;

    /// <summary>
    /// If "true" volume plot is visible.
    /// </summary>
    public bool ShowVolumePlot
    {
        get => _showVolumePlot;
        set => SetField(ref _showVolumePlot, value);
    }

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
    /// Notifies change of dependency property.
    /// </summary>
    private static void OnDependencyPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        if (obj is ExchangeChartControl chartControl)
            chartControl.OnPropertyChanged(args.Property.Name);
    }

    /// <summary>
    /// Dependency property.
    /// </summary>
    public static readonly DependencyProperty ShowFocusTimeMarkerProperty =
        DependencyProperty.Register(nameof(ShowFocusTimeMarker), typeof(bool),
            typeof(ExchangeChartControl), new PropertyMetadata(OnDependencyPropertyChanged));

    /// <summary>
    /// Determines whether the focus time marker should be visible.
    /// </summary>
    public bool ShowFocusTimeMarker
    {
        get => (bool)GetValue(ShowFocusTimeMarkerProperty);
        set => SetValue(ShowFocusTimeMarkerProperty, value);
    }

    /// <summary>
    /// Updates "in focus time" property with the given
    /// <param name="newValue"/> without broadcasting.
    /// </summary>
    public bool UpdateInFocusTimeNoBroadcast(double newValue)
    {
        if (SetField(ref _inFocusTime, newValue))
        {
            BuildMainChartTimeMarker();
            MainPlot.Refresh();
            BuildVolumeChartTimeMarker();
            VolPlot.Refresh();
            return true;
        }

        return false;
    }

    private double _inFocusTime;

    /// <summary>
    /// Point in time that is under the mouse pointer.
    /// </summary>
    public double InFocusTime
    {
        get => _inFocusTime;

        set
        {
            if (UpdateInFocusTimeNoBroadcast(value))
                _syncController?.BroadcastInFocusTime(this, InFocusTime);
        }
    }

    private VerticalLine _mainChartTimeMarker;
    private HorizontalLine _mainChartPriceMarker;
    private VerticalLine _volumeChartTimeMarker;
    
    private double _focusPrice = double.NaN;

    /// <summary>
    /// Current focus price.
    /// </summary>
    private double FocusPrice
    {
        set
        {
            if (SetField(ref _focusPrice, value))
            {
                BuildMainChartPriceMarker();
                MainPlot.Refresh();
            }
        }
    }

    /// <summary>
    /// Dependency property.
    /// </summary>
    public static readonly DependencyProperty TimeFrameEndProperty =
        DependencyProperty.Register(nameof(TimeFrameEnd), typeof(double), typeof(ExchangeChartControl),
            new PropertyMetadata(defaultValue: double.NaN, OnDependencyPropertyChanged));

    /// <summary>
    /// The end of displayed time frame in OLE Automation Date format.
    /// </summary>
    public double TimeFrameEnd
    {
        get => (double)GetValue(TimeFrameEndProperty);
        set
        {
            if (UpdateTimeFrameEndNoBroadcast(value))
                _syncController?.BroadcastFrameEnd(this, GetRegularizedTimeFrame());
        }
    }

    /// <summary>
    /// Updates value of the "end of time frame" parameter but does not "broadcast" the change.
    /// </summary>
    public bool UpdateTimeFrameEndNoBroadcast(double newValue)
    {
        if (!TimeFrameEnd.Equals(newValue))
        {
            SetValue(TimeFrameEndProperty, newValue);
            SetAxesLimits(regularizeTimeFrame: false);
            return true;
        }

        return false;
    }

    private IChartSynchronizationController _syncController;

    /// <summary>
    /// Registers to the given instance the given instance of synchronization controller.
    /// </summary>
    public void RegisterToSynchronizationController(IChartSynchronizationController syncController)
    {
        _syncController = syncController;
        _syncController?.Register(this);
    }

    /// <summary>
    /// Sets color to the given axis line.
    /// </summary>
    private static void SetColor(AxisLine line)
    {
        if (line == null) return;
        
        line.LineStyle.Color = ScottPlotPalette.ForegroundColor;
        line.LabelStyle.ForeColor = ScottPlotPalette.ForegroundColor;
        line.LabelStyle.BackgroundColor = new Color(0, 0, 0, 0);
    }

    /// <summary>
    /// Builds time marker on the main chart.
    /// </summary>
    private void BuildMainChartTimeMarker()
    {
        if (!ShowFocusTimeMarker)
        {
            MainPlot.Plot.PlottableList.Remove(_mainChartTimeMarker);
            _mainChartTimeMarker = null;
            return;
        }

        if (_mainChartTimeMarker != null)
        {
            _mainChartTimeMarker.X = InFocusTime;

            var dt = DateTime.FromOADate(InFocusTime);
            _mainChartTimeMarker.LabelText = $"{dt.ToShortDateString()}/{dt.ToShortTimeString()}";

            if (!MainPlot.Plot.PlottableList.Contains(_mainChartTimeMarker))
                MainPlot.Plot.PlottableList.Add(_mainChartTimeMarker);

            return;
        }

        _mainChartTimeMarker = MainPlot.Plot.Add.VerticalLine(InFocusTime);
        _mainChartTimeMarker.Axes.XAxis = MainPlot.Plot.Axes.Bottom;
        _mainChartTimeMarker.IsVisible = true;
        _mainChartTimeMarker.Axes.XAxis = MainPlot.Plot.Axes.Bottom;
        _mainChartTimeMarker.LabelOppositeAxis = true;
        _mainChartTimeMarker.LineWidth = 1;
        _mainChartTimeMarker.LabelOffsetY = 9;
        SetColor(_mainChartTimeMarker);
    }

    /// <summary>
    /// Builds price marker on the main chart.
    /// </summary>
    private void BuildMainChartPriceMarker()
    {
        if (double.IsNaN(_focusPrice))
        {
            MainPlot.Plot.PlottableList.Remove(_mainChartPriceMarker);
            _mainChartPriceMarker = null;
            return;
        }

        if (_mainChartPriceMarker != null)
        {
            _mainChartPriceMarker.Y = _focusPrice;
            _mainChartPriceMarker.LabelText = $"{DataFormatter.FloatToCompact(_focusPrice)} USDT";
            if (!MainPlot.Plot.PlottableList.Contains(_mainChartPriceMarker))
                MainPlot.Plot.PlottableList.Add(_mainChartPriceMarker);

            return;
        }

        _mainChartPriceMarker = MainPlot.Plot.Add.HorizontalLine(_focusPrice);
        _mainChartPriceMarker.Axes.YAxis = MainPlot.Plot.Axes.Right;
        _mainChartPriceMarker.LineWidth = 1;
        SetColor(_mainChartPriceMarker);
    }

    /// <summary>
    /// Builds focus markers on the main chart.
    /// </summary>
    private void ProcessMarkersOnMainChart()
    {
        BuildMainChartTimeMarker();
        BuildMainChartPriceMarker();
    }

    /// <summary>
    /// Builds focus marker on the volume chart.
    /// </summary>
    private void BuildVolumeChartTimeMarker()
    {
        if (_volumeChartTimeMarker != null)
        {
            _volumeChartTimeMarker.X = InFocusTime;
            if (!VolPlot.Plot.PlottableList.Contains(_volumeChartTimeMarker))
                VolPlot.Plot.PlottableList.Add(_volumeChartTimeMarker);
            
            return;
        }

        if (!ShowFocusTimeMarker)
            return;

        _volumeChartTimeMarker = VolPlot.Plot.Add.VerticalLine(InFocusTime);
        _volumeChartTimeMarker.LineWidth = 1;
        SetColor(_volumeChartTimeMarker);
    }

    private readonly Color _indicatorColor = Color.FromColor(System.Drawing.Color.Blue).WithAlpha(0.25);

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
        MainPlot.Plot.Axes.AutoScale();

        var timeline = chartData.Times;

        foreach (var changePt in chartData.PriceIndicatorPoints)
            MainPlot.Plot.Add.HorizontalSpan(timeline[Math.Max(changePt - 1, 0)],
                timeline[Math.Min(changePt + 1, timeline.Count - 1)], color: _indicatorColor);

        ProcessMarkersOnMainChart();
        
        return result;
    }

    /// <summary>
    /// Builds volume bar chart based on the given data.
    /// </summary>
    private BarPlot BuildVolumeChart(ChartData chartData)
    {
        if (!ShowVolumePlot)
            return null;

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

            bar.LineWidth = 0;
        }

        var timeline = chartData.Times;

        foreach (var changePt in chartData.VolumeIndicatorPoints)
            VolPlot.Plot.Add.HorizontalSpan(timeline[Math.Max(changePt - 1, 0)],
                timeline[Math.Min(changePt + 1, timeline.Count - 1)], color: _indicatorColor);

        ScottPlot.TickGenerators.NumericAutomatic volumeTicksGenerator = new()
        {
            LabelFormatter = DataFormatter.FloatToCompact
        };

        VolPlot.Plot.Axes.Right.TickGenerator = volumeTicksGenerator;

        ScottPlot.TickGenerators.NumericAutomatic voidTicksGenerator = new()
        {
            LabelFormatter = _ => ""
        };

        VolPlot.Plot.Axes.Bottom.TickGenerator = voidTicksGenerator;
        VolPlot.Plot.Axes.AutoScale();

        BuildVolumeChartTimeMarker();

        return result;
    }

    /// <summary>
    /// Returns value of the end of time frame clamped to its "conservative" limits.
    /// </summary>
    private double GetRegularizedTimeFrame()
    {
        if (_chartData == null)
            return TimeFrameEnd;

        var result = TimeFrameEnd;
        if (result < _chartData.MinStickTime + _chartData.TimeFrameDurationOad)
            result = _chartData.MinStickTime + _chartData.TimeFrameDurationOad;

        if (result > _chartData.MaxStickTime)
            result = double.NaN;

        return result;
    }

    /// <summary>
    /// Set axes limits for both plots.
    /// </summary>
    private void SetAxesLimits(bool regularizeTimeFrame)
    {
        if (_chartData == null)
            return;

        if (regularizeTimeFrame) // do not do adjustment of time-frame boundaries if we are "dragging"
            TimeFrameEnd = GetRegularizedTimeFrame();

        var localFrameEnd = double.IsNaN(TimeFrameEnd) ? _chartData.GetEndTime().ToOADate() : TimeFrameEnd;

        VolPlot.Plot.Axes.SetLimitsX(localFrameEnd - _chartData.TimeFrameDurationOad, localFrameEnd);
        VolPlot.Refresh();

        MainPlot.Plot.Axes.SetLimitsX(localFrameEnd - _chartData.TimeFrameDurationOad, localFrameEnd);
        MainPlot.Refresh();
    }

    /// <summary>
    /// Updates plots based on the current chart data.
    /// </summary>
    public void UpdatePlots(ChartData chartData)
    {
        _chartData = chartData;
        _candlestickPlot = BuildCandleSticks(_chartData);
        _volumePlot = BuildVolumeChart(_chartData);
        SetAxesLimits(regularizeTimeFrame: !PlotDragInProgress);
        ApplyColorPalettes();
    }
        
    /// <summary>
    /// Constructor.
    /// </summary>
    public ExchangeChartControl()
    {
        InitializeComponent();
        InitializePlots();
    }

    /// <summary>
    /// Applies current color palette to all the plots.
    /// </summary>
    private void ApplyColorPalettes()
    {
        SetColor(_mainChartPriceMarker);
        SetColor(_mainChartTimeMarker);
        SetColor(_volumeChartTimeMarker);
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
            name: nameof(DarkMode),
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

        if (_startDragPixel.HasValue)
        {
            var start = _startDragPixel.Value;
            var offset = new PixelOffset(start.X - pixel.X, 0);
            MainPlot.Plot.Axes.Pan(offset);
            VolPlot.Plot.Axes.Pan(offset);
            _startDragPixel = pixel;
            TimeFrameEnd = plot.Plot.Axes.GetLimits().Right;
        }

        if (plot == MainPlot)
            FocusPrice = dataPt.Y;

        InFocusTime = dataPt.X;

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
        MainPlot.UserInputProcessor.Disable();
        MainPlot.Plot.Axes.Left.SetTicks([], []);
        MainPlot.Plot.Axes.DateTimeTicksBottom();

        VolPlot.UserInputProcessor.Disable();
        VolPlot.Plot.Axes.Left.SetTicks([], []);
        VolPlot.Plot.Axes.Bottom.SetTicks([], []);

        var padding = new PixelPadding(40, 70, 40, 12);
        MainPlot.Plot.Layout.Fixed(padding);
        padding.Top = 0;
        padding.Bottom = 10;
        VolPlot.Plot.Layout.Fixed(padding);
    }

    private Pixel? _startDragPixel = null;

    /// <summary>
    /// "True" if the plot dragging is in progress.
    /// </summary>
    private bool PlotDragInProgress => _startDragPixel.HasValue;

    /// <summary>
    /// Mouse leave event handler of the main plot.
    /// </summary>
    private void MainPlot_OnMouseLeave(object sender, MouseEventArgs e)
    {
        FocusPrice = double.NaN;
        InfoTip.IsOpen = false;
        _startDragPixel = null;
    }

    /// <summary>
    /// Mouse leave event handler of the volume plot.
    /// </summary>
    private void VolumePlot_OnMouseLeave(object sender, MouseEventArgs e)
    {
        InfoTip.IsOpen = false;
        _startDragPixel = null;
    }

    /// <summary>
    /// Shared "mouse down" event handler for the two plots.
    /// </summary>
    private void Plots_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            _startDragPixel = MainPlot.GetPlotPixelPosition(e);
    }

    /// <summary>
    /// Shared "mouse up" event handler for the two plots.
    /// </summary>
    private void Plots_OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        _startDragPixel = null;
        SetAxesLimits(regularizeTimeFrame: true);
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