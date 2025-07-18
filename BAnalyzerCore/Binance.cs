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

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;
using BAnalyzerCore.Cache;
using BAnalyzerCore.DataStructures;
using BAnalyzerCore.Persistence.DataStructures;
using BAnalyzerCore.Utils;

namespace BAnalyzerCore;

/// <summary>
/// Wrapper of the Binance client.
/// </summary>
public class Binance : IDisposable
{
    private readonly BinanceRestClient _client = new();

    /// <summary>
    /// Constructor.
    /// </summary>
    public void Dispose() => _client.Dispose();

    /// <summary>
    /// Returns all the available coin pairs (in form of strings, called symbols)
    /// </summary>
    public async Task<IList<string>> GetSymbolsAsync()
    {
        var exchangeInfo = await _client.SpotApi.ExchangeData.GetExchangeInfoAsync();
        if (!exchangeInfo.Success)
            throw new Exception("Failed to get exchange info");

        return exchangeInfo.Data.Symbols.Select(x => x.Name).ToArray();
    }

    /// <summary>
    /// Returns all the available coin pairs (in form of strings, called symbols)
    /// </summary>
    public IList<string> GetSymbols() => Task.Run(async () => await GetSymbolsAsync()).Result;

    /// <summary>
    /// Maximal number of "k-lines" that Binance client can retrieve per time.
    /// </summary>
    private const int MaxKLinesPerTime = 1000;
    private static readonly DateTime MinTime = new(2016, 1, 1);

    /// <summary>
    /// Returns boundaries of the maximal possible time interval
    /// which is centered around the given time interval
    /// represented with its begin-end points and which can be
    /// retrieved via a single call from Binance server.
    /// </summary>
    private static TimeInterval GetDefaultInterval(DateTime timeBegin,
        DateTime timeEnd, DateTime localNow, KlineInterval granularity)
    {
        var kLineSpan = granularity.ToTimeSpan();

        var requestedSpan = timeEnd - timeBegin;
        var kLinesRequested = (int)(requestedSpan / kLineSpan) + 1;

        var kLinesSpare = MaxKLinesPerTime - kLinesRequested;

        if (kLinesSpare < 0)
            throw new InvalidOperationException("Too large time interval requested");

        var kLinesToAddToTheEnd = Math.Min((int)((localNow - timeEnd).Duration() / kLineSpan), kLinesSpare / 2);
        var kLinesToAddToTheBegin = kLinesSpare - kLinesToAddToTheEnd;

        if (kLinesRequested + kLinesToAddToTheEnd + kLinesToAddToTheBegin > MaxKLinesPerTime)
            throw new InvalidOperationException("Something went wrong!");

        var timeBeginExtended = timeBegin.Subtract(kLineSpan * kLinesToAddToTheBegin);
        var timeEndExtended = timeEnd.Add(kLineSpan * kLinesToAddToTheEnd);

        return new TimeInterval(timeBeginExtended.Max(MinTime), timeEndExtended);
    }

    /// <summary>
    /// Returns a time interval that needs to be retrieved from server
    /// given the <param name="timeBegin"/> and <param name="timeEnd"/> boundaries
    /// of the interval requested by the caller.The returned interval is
    /// optimized with respect to <param name="cacheGapInterval"/>
    /// which tells us about the boundaries of the data missing in the cache.
    /// </summary>
    private static TimeInterval GetTimeIntervalToRetrieveFromServer(DateTime timeBegin, DateTime timeEnd,
        KlineInterval granularity, TimeInterval cacheGapInterval, DateTime localNow)
    {
        if (cacheGapInterval.IsBothSideOpen())
            return GetDefaultInterval(timeBegin, timeEnd, localNow, granularity);

        var kLineSpan = granularity.ToTimeSpan();

        // Extend gap interval one k-line in each side, if possible.
        var gapIntervalLocal = new TimeInterval(
            cacheGapInterval.IsOpenToTheLeft() ? null : cacheGapInterval.Begin.Subtract(kLineSpan),
            cacheGapInterval.IsOpenToTheRight() ? null : cacheGapInterval.End.Add(kLineSpan));

        if (!gapIntervalLocal.IsOpen()) return gapIntervalLocal.Copy();

        var span = kLineSpan * (MaxKLinesPerTime - 1);

        return !gapIntervalLocal.IsOpenToTheLeft() ?
            new TimeInterval(gapIntervalLocal.Begin, gapIntervalLocal.Begin.Add(span).Min(localNow)) :
            new TimeInterval(gapIntervalLocal.End.Subtract(span), gapIntervalLocal.End);
    }

    private BinanceCache _cache = new();

    /// <summary>
    /// Returns "k-line" data.
    /// </summary>
    public async Task<(IList<KLine> Data, bool Success)> GetCandleSticksAsync(DateTime timeBegin, DateTime timeEnd,
        KlineInterval granularity, string symbol, bool ensureLatestData)
    {
        // Binance client usually has troubles retrieving
        // k-lines that are less than a few seconds "old".
        // To mitigate this issue we make our local
        // time to lag for 1 second.
        var localNow = DateTime.UtcNow.RoundToSeconds(1);
        timeEnd = timeEnd.Min(localNow);

        if (timeBegin >= timeEnd)
            return (new List<KLine>(), true);

        var cacheGrid = _cache.GetAssetViewThreadSafe(symbol).GetGridThreadSafe(granularity);

        var cachedResult = cacheGrid.RetrieveRobustThreadSafe(timeBegin, timeEnd, out var cacheGapInterval);

        if (cachedResult != null)
        {
            if (ensureLatestData && cachedResult.Last().CloseTime >= localNow)
            {
                var lastKline = cachedResult.Last();
                // We need to update the last "k-line"

                var binanceResult = await _client.SpotApi.ExchangeData.GetUiKlinesAsync(symbol, granularity,
                    startTime: lastKline.OpenTime, endTime: lastKline.CloseTime);

                if (!binanceResult.Success)
                    return (new List<KLine>(), false); 

                var latestKline = binanceResult.Data.ToArray();

                var updatedKline = new KLine(latestKline[^1]);
                cachedResult[^1] = updatedKline;

                cacheGrid.AppendThreadSafe([updatedKline]);
            }

            return (cachedResult, true);
        }

        var interval = GetTimeIntervalToRetrieveFromServer(timeBegin, timeEnd, granularity, cacheGapInterval, localNow);

        var kLines = await _client.SpotApi.ExchangeData.GetUiKlinesAsync(symbol, granularity,
            startTime: interval.Begin, endTime: interval.End, limit: 1500);

        if (!kLines.Success)
            return (new List<KLine>(), false);

        var kLinesArray = kLines.Data.Select(x => new KLine(x)).ToArray();

        if (kLinesArray.Length > 0)
        {
            cacheGrid.AppendThreadSafe(kLinesArray);

            if (kLinesArray[0].OpenTime > interval.Begin.Add(granularity.ToTimeSpan()))
                cacheGrid.SetZeroTimePointThreadSafe(kLinesArray[0].OpenTime);
        }
        else if (kLinesArray.Length == 0)
            cacheGrid.SetZeroTimePointThreadSafe(interval.End);

        if (kLinesArray.Length <= 1 && !cacheGapInterval.IsOpenToTheLeft())
        {
            // This is a glitch of Binance client (or server)
            // because, in fact, we have even an older data present
            // in the cache.
            // So, let's just return what we have.
            return (cacheGrid.RetrieveRobustThreadSafe(timeBegin, cacheGapInterval.Begin, out _), true);
        }

        return (cacheGrid.RetrieveRobustThreadSafe(timeBegin, timeEnd, out _), true);
    }

    /// <summary>
    /// Returns an order book for the given exchange symbol.
    /// </summary>
    public async Task<BinanceOrderBook> GetOrders(string symbol)
    {
        var result = await _client.SpotApi.ExchangeData.GetOrderBookAsync(symbol);
        return result.Data;
    }

    /// <summary>
    /// Cache of a spot-price data.
    /// </summary>
    private readonly ConcurrentDictionary<string, BinancePrice> _priceCache = new();

    /// <summary>
    /// Returns cached spot-price object for the given <paramref name="symbol"/>
    /// if it exists in the cache or null, otherwise.
    /// </summary>
    public BinancePrice GetCachedPrice(string symbol, int acceptableStalenessMs)
    {
        if (!_priceCache.TryGetValue(symbol, out var priceCached) ||
            priceCached == null || !priceCached.Timestamp.HasValue) return null;

        return DateTime.UtcNow.Subtract(priceCached.Timestamp.Value).
            Duration().TotalMilliseconds < acceptableStalenessMs ? priceCached : null;
    }

    /// <summary>
    /// Returns current price for the given symbol.
    /// </summary>
    public async Task<BinancePrice> GetCurrentPrice(string symbol, int acceptableStalenessMs = 500)
    {
        if (symbol is null or "")
            return null;

        var priceCached = GetCachedPrice(symbol, acceptableStalenessMs);

        if (priceCached != null)
            return priceCached;

        var price = await _client.SpotApi.ExchangeData.GetPriceAsync(symbol);

        return _priceCache[symbol] = price.Success ? price.Data with {Timestamp = DateTime.UtcNow} : null;
    }

    /// <summary>
    /// Saves the cache to the folder with the given <paramref name="folderPath"/>.
    /// </summary>
    public async Task SaveCacheAsync(string folderPath) =>
        await Task.Run(() => _cache.Save(folderPath));

    /// <summary>
    /// Loads cache from the data in the given <paramref name="folderPath"/>
    /// saved there previously by <see cref="SaveCacheAsync"/>.
    /// </summary>
    public async Task LoadCacheAsync(string folderPath) =>
        _cache = await Task.Run(() => BinanceCache.Load(folderPath));

    /// <summary>
    /// Delegate to report progress when caching data.
    /// </summary>
    public delegate void CachingProgressReport(KlineInterval granularity, DateTime beginTime, DateTime endTime, long cachedBytes);

    /// <summary>
    /// Reads out all the "k-line" data that corresponds to the given <paramref name="symbol"/>
    /// starting from "now" back to the "first placement" moment and puts the data into the
    /// given <paramref name="storage"/> provided by the caller.
    /// </summary>
    public async Task ReadOutData(string symbol, BinanceCache storage, CachingProgressReport progressReportCallback)
    {
        var excludedIntervals = new[]
        {
            KlineInterval.OneSecond,
        }.ToHashSet();

        foreach (var granularity in Enum.GetValues(typeof(KlineInterval)).Cast<KlineInterval>()
                     .Where(x => !excludedIntervals.Contains(x)))
            await ReadOutData(symbol, granularity, storage, _client, progressReportCallback);
    }

    /// <summary>
    /// Reads out all the "k-line" data that corresponds to the given <paramref name="symbol"/>
    /// and given <paramref name="granularity"/> starting from "now" back to the "first placement"
    /// moment and puts the data into the given <paramref name="storage"/> provided by the caller.
    /// </summary>
    public static async Task ReadOutData(string symbol, KlineInterval granularity, BinanceCache storage,
        BinanceRestClient client, CachingProgressReport progressReportCallback)
    {
        var end = DateTime.UtcNow;
        var granularitySpan = granularity.ToTimeSpan();

        var container = storage.GetAssetViewThreadSafe(symbol).GetGridThreadSafe(granularity);

        DateTime begin;
        var totalCachedSize = 0L;

        do
        {
            begin = end.Subtract(MaxKLinesPerTime * granularitySpan).Max(MinTime);
            var kLines = await client.SpotApi.ExchangeData.GetUiKlinesAsync(symbol, granularity,
                startTime: begin, endTime: end, limit: 1500);

            if (!kLines.Success)
                throw new Exception($"Failed to get exchange info: {kLines.Error}");

            var kLinesArray = kLines.Data.Select(x => new KLine(x)).ToArray();

            if (kLinesArray.Length == 0)
                break;

            totalCachedSize += kLinesArray.Length * KLine.SizeInBytes;

            container.AppendThreadSafe(kLinesArray);
            progressReportCallback?.Invoke(granularity, container.Begin, container.End, totalCachedSize);

            end = kLinesArray.First().OpenTime;

        } while (begin.Add(granularitySpan) >= end);
    }
}