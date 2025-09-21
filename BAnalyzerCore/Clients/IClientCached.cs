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

using BAnalyzerCore.Cache;
using BAnalyzerCore.DataStructures;
using static BAnalyzerCore.Cache.ProgressReportDelegates;

namespace BAnalyzerCore.Clients;

/// <summary>
/// Interface for an exchange client backed up with ф caching functionality.
/// </summary>
public interface IClientCached : IDisposable
{
    /// <summary>
    /// Returns all the available coin pairs (in form of strings, called symbols)
    /// </summary>
    Task<IList<string>> GetSymbolsAsync();

    /// <summary>
    /// Returns all the available coin pairs (in form of strings, called symbols), sync version.
    /// </summary>
    IList<string> GetSymbols();

    /// <summary>
    /// Returns collection of supported granularities ordered in
    /// ascending order with respect to the time-spans they represent.
    /// </summary>
    IReadOnlyList<TimeGranularity> Granularities { get; }

    /// <summary>
    /// Returns "k-line" data.
    /// </summary>
    Task<(IList<KLine> Data, bool Success)> GetKLinesAsync(string symbol, TimeGranularity granularity,
        DateTime timeBegin, DateTime timeEnd, bool ensureLatestData);

    /// <summary>
    /// Returns an order book for the given exchange symbol.
    /// </summary>
    Task<IOrderBook> GetOrderBookAsync(string symbol);

    /// <summary>
    /// Returns cached spot-price object for the given <paramref name="symbol"/>
    /// if it exists in the cache or null, otherwise.
    /// </summary>
    IPriceData GetCachedPrice(string symbol, int acceptableStalenessMs);

    /// <summary>
    /// Returns current price for the given symbol. The price might be retrieved
    /// from cache if it is not alder than <paramref name="acceptableStalenessMs"/> milliseconds.
    /// </summary>
    Task<IPriceData> GetPriceAsync(string symbol, int acceptableStalenessMs);

    /// <summary>
    /// Saves the cache to the folder with the given <paramref name="folderPath"/>.
    /// </summary>
    Task SaveCacheAsync(string folderPath, GeneralProgressReportingDelegate progressReporter);

    /// <summary>
    /// Loads cache from the data in the given <paramref name="folderPath"/>
    /// saved there previously by <see cref="SaveCacheAsync"/>.
    /// </summary>
    Task LoadCacheAsync(string folderPath, GeneralProgressReportingDelegate progressReporter);

    /// <summary>
    /// Reads out all the "k-line" data that corresponds to the given <paramref name="symbol"/>
    /// starting from "now" back to the "first placement" moment and puts the data into the
    /// given <paramref name="storage"/> provided by the caller.
    /// </summary>
    Task ReadOutData(string symbol, BinanceCache storage, CachingProgressReport progressReportCallback);
}