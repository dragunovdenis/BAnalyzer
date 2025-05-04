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

namespace BAnalyzerCore.Utils;

/// <summary>
/// Collection of methods that help dealing with date-time data.
/// </summary>
internal static class DateTimeUtils
{
    /// <summary>
    /// Returns index of the first element in the given <param name="collection"/>
    /// for which the time value returned by <param name="timeExtractor"/>
    /// is not less than the given <param name="time"/>.
    /// </summary>
    public static int LowerBoundTime<T>(this IReadOnlyList<T> collection, Func<T, DateTime> timeExtractor, DateTime time)
    {
        var count = collection.Count;
        var first = 0;

        while (count > 0)
        {
            var step = count / 2;
            var tempId = first + step;

            if (timeExtractor(collection[tempId]) < time)
            {
                first = ++tempId;
                count -= step + 1;
            }
            else
                count = step;
        }

        return first;
    }

    /// <summary>
    /// Returns index of the first element in the given <param name="collection"/>
    /// for which the time value returned by <param name="timeExtractor"/>
    /// is greater than the given <param name="time"/>.
    /// </summary>
    public static int UpperBoundTime<T>(this IReadOnlyList<T> collection, Func<T, DateTime> timeExtractor, DateTime time)
    {
        var count = collection.Count;
        var first = 0;

        while (count > 0)
        {
            var step = count / 2;
            var tempId = first + step;

            if (timeExtractor(collection[tempId]) <= time)
            {
                first = ++tempId;
                count -= step + 1;
            }
            else
                count = step;
        }

        return first;
    }

    /// <summary>
    /// Returns maximum of the given two time points.
    /// </summary>
    public static DateTime Max(this DateTime t0, DateTime t1) => new(Math.Max(t0.Ticks, t1.Ticks));

    /// <summary>
    /// Returns minimum of the given two time points.
    /// </summary>
    public static DateTime Min(this DateTime t0, DateTime t1) => new(Math.Min(t0.Ticks, t1.Ticks));

    /// <summary>
    /// Returns maximum of the given two time spans.
    /// </summary>
    public static TimeSpan Max(this TimeSpan s0, TimeSpan s1) => new(Math.Max(s0.Ticks, s1.Ticks));

    /// <summary>
    /// Returns minimum of the given two time spans.
    /// </summary>
    public static TimeSpan Min(this TimeSpan s0, TimeSpan s1) => new(Math.Min(s0.Ticks, s1.Ticks));
}