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

namespace BAnalyzerCore.DataStructures;

/// <summary>
/// A continuous interval of time defined by "begin" and "end" points.
/// </summary>
public class TimeInterval(DateTime begin, DateTime end)
{
    /// <summary>
    /// Returns a copy of the current interval.
    /// </summary>
    public TimeInterval Copy() => new(Begin, End);

    /// <summary>
    /// Left boundary of the interval.
    /// </summary>
    public DateTime Begin { get; } = begin;

    /// <summary>
    /// Right boundary of the interval.
    /// </summary>
    public DateTime End { get; } = end;

    /// <summary>
    /// Returns "true" if the interval is empty.
    /// </summary>
    public bool IsEmpty() => Begin >= End;

    /// <summary>
    /// Returns "true" if the given <param name="time"/>
    /// belongs to the interior of interval.
    /// </summary>
    public bool IsStrictlyInside(DateTime time) => Begin < time && time < End;

    /// <summary>
    /// Returns an empty interval.
    /// </summary>
    public static TimeInterval Empty => new(DateTime.MaxValue, DateTime.MinValue);

    /// <summary>
    /// Returns the result of subtraction of the given
    /// <param name="intervalToSubtract"/> from the current interval.
    /// The output collection always contains at least one item, in
    /// some cases (if the given interval to subtract is contained
    /// by the current one) it can contain 2 items.
    /// </summary>
    public IList<TimeInterval> Subtract(TimeInterval intervalToSubtract)
    {
        if (intervalToSubtract.Begin <= Begin && intervalToSubtract.End >= End)
            return [Empty];

        if (intervalToSubtract.End <= Begin ||  intervalToSubtract.Begin >= End)
            return [new TimeInterval(Begin, End)];

        var result = new List<TimeInterval>();

        if (IsStrictlyInside(intervalToSubtract.Begin))
            result.Add(new TimeInterval(Begin, intervalToSubtract.Begin));

        if (IsStrictlyInside(intervalToSubtract.End))
            result.Add(new TimeInterval(intervalToSubtract.End, End));

        return result;
    }
}