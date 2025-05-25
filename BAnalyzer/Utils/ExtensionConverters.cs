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

using BAnalyzerCore.DataStructures;
using Binance.Net.Interfaces;
using ScottPlot;

namespace BAnalyzer.Utils;

/// <summary>
/// Extension methods that convert data.
/// </summary>
internal static class ExtensionConverters
{
    /// <summary>
    /// Converter.
    /// </summary>
    public static OHLC[] ToScottPlotCandleSticks(this IList<KLine> candleStickData) =>
        candleStickData.Select(x => x.ToScottPlotCandleStick()).ToArray();

    /// <summary>
    /// Converts the "binance" candle-stick into a "scott-plot" candle-stick.
    /// </summary>
    public static OHLC ToScottPlotCandleStick(this KLine candleStick)
    {
        return new OHLC()
        {
            Close = candleStick.ClosePrice,
            Open = candleStick.OpenPrice,
            High = candleStick.HighPrice,
            Low = candleStick.LowPrice,
            TimeSpan = candleStick.CloseTime - candleStick.OpenTime,
            DateTime = candleStick.OpenTime.Add(0.5 * (candleStick.CloseTime - candleStick.OpenTime)).ToLocalTime()
        };
    }
}