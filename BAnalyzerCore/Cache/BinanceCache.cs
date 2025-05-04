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
using Binance.Net.Enums;
using Binance.Net.Interfaces;

namespace BAnalyzerCore.Cache;

/// <summary>
/// Functionality allowing to cache "k-line" series.
/// </summary>
internal class BinanceCache
{
    private readonly ExchangeCatalogue<AssetTimeView> _data = new();

    /// <summary>
    /// Returns an existing asset-view object associated with the given pair of keys
    /// or a newly created one (if such an object does not exist yet).
    /// </summary>
    private AssetTimeView GetOrCreate(string symbol, KlineInterval granularity)
    {
        if (_data.TryGet(symbol, granularity, out var view))
            return view;

        var newView = new AssetTimeView(granularity);
        _data.Set(symbol, granularity, newView);
        return newView;
    }

    /// <summary>
    /// Returns instance of asset-view associated with the given "symbol-granularity" pair.
    /// </summary>
    public AssetTimeView GetAssetViewThreadSafe(string symbol, KlineInterval granularity)
    {
        lock (this) return GetOrCreate(symbol, granularity);
    }
}