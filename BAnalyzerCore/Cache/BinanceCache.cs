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
using Binance.Net.Interfaces;

namespace BAnalyzerCore.Cache;

/// <summary>
/// Functionality allowing to cache "k-line" series.
/// </summary>
internal class BinanceCache
{
    private readonly Dictionary<string, AssetCache> _data = new();

    /// <summary>
    /// Returns cached data in the given interval or "null"
    /// if the data can't be retrieved.
    /// </summary>
    public IList<IBinanceKline> Retrieve(string symbol, KlineInterval granularity, DateTime timeBegin,
        DateTime timeEnd)
    {
        if (!_data.TryGetValue(symbol, out var cache))
            return null;

        return cache.Retrieve(granularity, timeBegin, timeEnd);
    }

    /// <summary>
    /// Appends new data to the cache.
    /// </summary>
    public void Append(string symbol, KlineInterval granularity, IReadOnlyList<IBinanceKline> kLines)
    {
        if (_data.TryGetValue(symbol, out var cache))
        {
            cache.Append(granularity, kLines);
        }
        else
        {
            var newCache = new AssetCache();
            newCache.Append(granularity, kLines);
            _data[symbol] = newCache;
        }
    }
}