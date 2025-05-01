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
/// Binance data cached for a certain asset
/// </summary>
internal class AssetCache
{
    /// <summary>
    /// Cached data.
    /// </summary>
    private readonly Dictionary<KlineInterval, BlockGrid> _data = new();

    /// <summary>
    /// Returns cached data in the given interval or "null"
    /// if the data can't be retrieved.
    /// </summary>
    public IList<IBinanceKline> Retrieve(KlineInterval granularity, DateTime timeBegin, DateTime timeEnd)
    {
        if (!_data.TryGetValue(granularity, out var grid))
            return null;

        return grid.Retrieve(timeBegin, timeEnd);
    }

    /// <summary>
    /// Appends new data to the cache.
    /// </summary>
    public void Append(KlineInterval granularity, IReadOnlyList<IBinanceKline> kLines)
    {
        if (_data.TryGetValue(granularity, out var grid))
        {
            grid.Append(granularity, kLines);
        }
        else
        {
            var newGrid = new BlockGrid();
            newGrid.Append(granularity, kLines);
            _data[granularity] = newGrid;
        }
    }
}