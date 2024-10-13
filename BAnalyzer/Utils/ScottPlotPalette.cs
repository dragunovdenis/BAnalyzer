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

using System.Windows;
using System.Windows.Media;
using ScottPlot;
using SkiaSharp.Views.WPF;
using Color = ScottPlot.Color;

namespace BAnalyzer.Utils;

/// <summary>
/// The "white" palette.
/// </summary>
public static class ScottPlotPalette
{
    /// <summary>
    /// Returns color loaded from the resource dictionary by the given identifier.
    /// </summary>
    private static Color LoadResourceColor(string resourceName) =>
        Color.FromSKColor(((SolidColorBrush)Application.Current.TryFindResource(resourceName)).Color.ToSKColor());

    /// <summary>
    /// Foreground color.
    /// </summary>
    public static Color ForegroundColor { get; private set; }

    /// <summary>
    /// Line color.
    /// </summary>
    public static Color LineColor { get; private set; }

    /// <summary>
    /// Background color.
    /// </summary>
    public static Color BackgroundColor { get; private set; }

    /// <summary>
    /// Applies palette to the plot
    /// </summary>
    public static void Apply(Plot plot)
    {
        BackgroundColor = LoadResourceColor("BackgroundColor");
        LineColor = LoadResourceColor("PlotLineColor");
        ForegroundColor = LoadResourceColor("ForegroundColor");

        plot.FigureBackground.Color = BackgroundColor;
        plot.DataBackground.Color = BackgroundColor;
        plot.Axes.Color(ForegroundColor);
        plot.Grid.MajorLineColor = LineColor;
        plot.Legend.OutlineColor = ForegroundColor;
    }
}