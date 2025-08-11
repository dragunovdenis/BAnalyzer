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

using Binance.Net.Enums;
using BAnalyzerCore;

namespace BAnalyzer.DataStructures;

/// <summary>
/// Representation of a time frame.
/// </summary>
internal class TimeFrame
{
    /// <summary>
    /// Duration of the timeframe.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Begin point.
    /// </summary>
    public DateTime Begin { get; init; }

    /// <summary>
    /// End point.
    /// </summary>
    public DateTime End { get; init; }

    /// <summary>
    /// Discretization of measurements (in time).
    /// </summary>
    public KlineInterval Discretization { get; init; }

    /// <summary>
    /// Constructor.
    /// </summary>
    public TimeFrame(KlineInterval discretization, int sticksPerChart, double timeFrameEndOad)
    {
        Discretization = discretization;
        Duration = Discretization.ToTimeSpan().Multiply(sticksPerChart);
        End = timeFrameEndOad > DateTime.UtcNow.ToOADate() ?
            DateTime.UtcNow : DateTime.FromOADate(timeFrameEndOad);
        Begin = End.Subtract(Duration);
    }
}