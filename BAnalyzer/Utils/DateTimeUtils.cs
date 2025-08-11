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

namespace BAnalyzer.Utils;

/// <summary>
/// Collection of methods for date-time operations.
/// </summary>
internal static class DateTimeUtils
{
    /// <summary>
    /// Converts OAD format local time to OAD format UTC time.
    /// </summary>
    public static double LocalToUtcOad(double localOad)
    {
        if (double.IsPositiveInfinity(localOad))
            return double.PositiveInfinity;

        var time = DateTime.FromOADate(localOad);

        if (TimeZoneInfo.Local.IsInvalidTime(time))
            // this is, probably, the daylight saving adjustment, so go straight to the next hour
            time = new DateTime(year: time.Year, month: time.Month, day: time.Day,
                hour: time.Hour + 1, minute: 0, second: 0);

        return TimeZoneInfo.ConvertTimeToUtc(time, TimeZoneInfo.Local).ToOADate();
    }
}