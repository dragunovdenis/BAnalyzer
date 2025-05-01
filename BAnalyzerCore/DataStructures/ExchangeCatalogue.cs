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

namespace BAnalyzerCore.DataStructures;

/// <summary>
/// Functionality allowing to store and access data
/// according to its "symbol" and "time-interval" properties.
/// </summary>
internal class ExchangeCatalogue<TD>
{
    private readonly Dictionary<Tuple<string, KlineInterval>, TD> _data = new();

    /// <summary>
    /// Returns key composed of the given pair of property values.
    /// </summary>
    private static Tuple<string, KlineInterval> Key(string symbol, KlineInterval granularity)
        => new(symbol, granularity);

    /// <summary>
    /// Returns "true" if a data item with the given <param name="symbol"/>
    /// and <param name="granularity"/> values exists in the catalogue,
    /// in which case the data can be accessed via the output parameter
    /// <param name="item"/>.
    /// </summary>
    public bool TryGet(string symbol, KlineInterval granularity, out TD item) =>
        _data.TryGetValue(Key(symbol, granularity), out item);

    /// <summary>
    /// Assigns given data <param name="item"/> to a cell corresponding
    /// to the given <param name="symbol"/> and <param name="granularity"/>
    /// properties.
    /// </summary>
    public void Set(string symbol, KlineInterval granularity, TD item) =>
        _data[Key(symbol, granularity)] = item;
}