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

namespace BAnalyzer.Utils;

public static class DataFormatter
{
    /// <summary>
    /// Formats given number to an approximate compact from.
    /// </summary>
    public static string FloatToCompact(double value)
    {
        if (Math.Abs(value) < 1)
            return $"{value:0.#####}";

        if (Math.Abs(value) < 1e3)
            return $"{value:0.##}";

        if (Math.Abs(value) < 1e6)
            return $"{value / 1e3:0.#}K";

        if (Math.Abs(value) < 1e9)
            return $"{value / 1e6:0.#}M";

        if (Math.Abs(value) < 1e12)
            return $"{value / 1e9:0.#}B";

        if (Math.Abs(value) < 1e15)
            return $"{value / 1e12:0.#}T";

        if (double.IsNaN(value))
            return "NaN";

        throw new ArgumentException("Unexpected range of the input argument.");
    }
}