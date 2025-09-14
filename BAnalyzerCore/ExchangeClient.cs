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

using BAnalyzerCore.Cache;
using BAnalyzerCore.Clients;
using BAnalyzerCore.DataStructures;
using BAnalyzerCore.Utils;
using System.Collections.Concurrent;
using static BAnalyzerCore.Cache.ProgressReportDelegates;

namespace BAnalyzerCore;

/// <summary>
/// Wrapper of an <see cref="IClient"/>.
/// </summary>
public class ExchangeClient : IDisposable
{
    private readonly IClient _client = new ClientBinance();
    //private readonly IClient _client = new ClientByBit();

    /// <summary>
    /// Constructor.
    /// </summary>
    public void Dispose() => _client.Dispose();

    /// <summary>
    /// Returns all the available coin pairs (in form of strings, called symbols)
    /// </summary>
    public async Task<IList<string>> GetSymbolsAsync() => await _client.GetSymbolsAsync();

    /// <summary>
    /// Returns all the available coin pairs (in form of strings, called symbols)
    /// </summary>
    public IList<string> GetSymbols() => Task.Run(async () => await GetSymbolsAsync()).Result;

    /// <summary>
    /// Returns collection of supported granularities ordered in
    /// ascending order with respect to the time-spans they represent.
    /// </summary>
    public IReadOnlyList<ITimeGranularity> Granularities => _client.Granularities;

    /// <summary>
    /// Returns boundaries of the maximal possible time interval
    /// which is centered around the given time interval
    /// represented with its begin-end points and which can be
    /// retrieved via a single call from Binance server.
    /// </summary>
    private TimeInterval GetDefaultInterval(DateTime timeBegin,
        DateTime timeEnd, DateTime localNow, ITimeGranularity granularity)
    {
        var kLineSpan = granularity.Span;

        var requestedSpan = timeEnd - timeBegin;
        var kLinesRequested = (int)(requestedSpan / kLineSpan) + 1;

        var kLinesSpare = _client.MaxKLinesPerTime - kLinesRequested;

        if (kLinesSpare < 0)
            throw new InvalidOperationException("Too large time interval requested");

        var kLinesToAddToTheEnd = Math.Min((int)((localNow - timeEnd).Duration() / kLineSpan), kLinesSpare / 2);
        var kLinesToAddToTheBegin = kLinesSpare - kLinesToAddToTheEnd;

        if (kLinesRequested + kLinesToAddToTheEnd + kLinesToAddToTheBegin > _client.MaxKLinesPerTime)
            throw new InvalidOperationException("Something went wrong!");

        var timeBeginExtended = timeBegin.Subtract(kLineSpan * kLinesToAddToTheBegin);
        var timeEndExtended = timeEnd.Add(kLineSpan * kLinesToAddToTheEnd);

        return new TimeInterval(timeBeginExtended.Max(_client.MinTime), timeEndExtended);
    }

    /// <summary>
    /// Returns a time interval that needs to be retrieved from server
    /// given the <param name="timeBegin"/> and <param name="timeEnd"/> boundaries
    /// of the interval requested by the caller.The returned interval is
    /// optimized with respect to <param name="cacheGapInterval"/>
    /// which tells us about the boundaries of the data missing in the cache.
    /// </summary>
    private TimeInterval GetTimeIntervalToRetrieveFromServer(DateTime timeBegin, DateTime timeEnd,
        ITimeGranularity granularity, TimeInterval cacheGapInterval, DateTime localNow)
    {
        if (cacheGapInterval.IsBothSideOpen())
            return GetDefaultInterval(timeBegin, timeEnd, localNow, granularity);

        var kLineSpan = granularity.Span;

        if (!cacheGapInterval.IsOpen()) return cacheGapInterval.Copy();

        var span = kLineSpan * (_client.MaxKLinesPerTime - 1);

        return !cacheGapInterval.IsOpenToTheLeft() ?
            new TimeInterval(cacheGapInterval.Begin, cacheGapInterval.Begin.Add(span).Min(localNow)) :
            new TimeInterval(cacheGapInterval.End.Subtract(span).Max(_client.MinTime), cacheGapInterval.End);
    }

    private BinanceCache _cache = new();

    /// <summary>
    /// Returns "k-line" data.
    /// </summary>
    public async Task<(IList<KLine> Data, bool Success)> GetCandleSticksAsync(DateTime timeBegin, DateTime timeEnd,
        ITimeGranularity granularity, string symbol, bool ensureLatestData)
    {
        // Binance client usually has troubles retrieving
        // k-lines that are less than a few seconds "old".
        // To mitigate this issue we make our local
        // time to lag for 1 second.
        var localNow = DateTime.UtcNow.RoundToSeconds(1);
        timeEnd = timeEnd.Min(localNow);
        timeBegin = timeBegin.Max(_client.MinTime);

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

                var binanceResult = await _client.GetKLinesAsync(symbol, granularity,
                    startTime: lastKline.OpenTime, endTime: lastKline.CloseTime, limit: null);

                if (!binanceResult.Success)
                    return (new List<KLine>(), false); 

                var updatedKline = binanceResult.KLines[^1];
                cachedResult[^1] = updatedKline;

                cacheGrid.AppendThreadSafe([updatedKline]);
            }

            return (cachedResult, true);
        }

        var interval = GetTimeIntervalToRetrieveFromServer(timeBegin, timeEnd, granularity, cacheGapInterval, localNow);

        var kLines = await _client.GetKLinesAsync(symbol, granularity,
            startTime: interval.Begin, endTime: interval.End, limit: 1500);

        if (!kLines.Success)
            return (new List<KLine>(), false);

        var kLinesArray = FillGapsGeneral(kLines.KLines, granularity, _client.MinTime);
        
        if (kLinesArray.Length > 0)
        {
            cacheGrid.AppendThreadSafe(kLinesArray);

            if (kLinesArray[0].OpenTime == interval.End)
                cacheGrid.SetZeroTimePointThreadSafe(kLinesArray[0].OpenTime);
        }

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
    public async Task<IOrderBook> GetOrders(string symbol) => await _client.GetOrderBookAsync(symbol);

    /// <summary>
    /// Cache of a spot-price data.
    /// </summary>
    private readonly ConcurrentDictionary<string, IPriceData> _priceCache = new();

    /// <summary>
    /// Returns cached spot-price object for the given <paramref name="symbol"/>
    /// if it exists in the cache or null, otherwise.
    /// </summary>
    public IPriceData GetCachedPrice(string symbol, int acceptableStalenessMs)
    {
        if (!_priceCache.TryGetValue(symbol, out var priceCached) ||
            priceCached == null) return null;

        return DateTime.UtcNow.Subtract(priceCached.TimeStamp).
            Duration().TotalMilliseconds < acceptableStalenessMs ? priceCached : null;
    }

    /// <summary>
    /// Returns current price for the given symbol.
    /// </summary>
    public async Task<IPriceData> GetCurrentPrice(string symbol, int acceptableStalenessMs = 500)
    {
        if (symbol is null or "")
            return null;

        var priceCached = GetCachedPrice(symbol, acceptableStalenessMs);

        if (priceCached != null)
            return priceCached;

        var price = await _client.GetPriceAsync(symbol);

        return _priceCache[symbol] = price;
    }

    /// <summary>
    /// Saves the cache to the folder with the given <paramref name="folderPath"/>.
    /// </summary>
    public async Task SaveCacheAsync(string folderPath, GeneralProgressReportingDelegate progressReporter = null) =>
        await Task.Run(() => _cache.Save(folderPath, progressReporter));

    /// <summary>
    /// Loads cache from the data in the given <paramref name="folderPath"/>
    /// saved there previously by <see cref="SaveCacheAsync"/>.
    /// </summary>
    public async Task LoadCacheAsync(string folderPath, GeneralProgressReportingDelegate progressReporter = null) =>
        _cache = await Task.Run(() => BinanceCache.Load(folderPath, progressReporter));

    /// <summary>
    /// Reads out all the "k-line" data that corresponds to the given <paramref name="symbol"/>
    /// starting from "now" back to the "first placement" moment and puts the data into the
    /// given <paramref name="storage"/> provided by the caller.
    /// </summary>
    public async Task ReadOutData(string symbol, BinanceCache storage, CachingProgressReport progressReportCallback)
    {
        foreach (var granularity in _client.Granularities)
            await ReadOutData(symbol, granularity, storage, _client, progressReportCallback);
    }

    /// <summary>
    /// Time-span representation of the time-gap between the close-time
    /// of previous k-line and open-time of the following k-line.
    /// </summary>
    private static readonly TimeSpan GapSpan = TimeSpan.FromSeconds(BinanceConstants.KLineTimeGapSec);

    /// <summary>
    /// Returns boundaries of chronologically contiguous blocks of k-lines
    /// in the given collection <paramref name="kLines"/>.
    /// </summary>
    private static IList<(int BeginIdInclusive, int EndIdExclusive)> FindBlockBoundaries(IList<KLine> kLines,
        Func<KLine, bool> validityPredicate)
    {
        var result = new List<(int, int)>();

        var currentItemId = 0;

        while (currentItemId < kLines.Count)
        {
            // skip invalid items if there are any
            while (currentItemId < kLines.Count && !validityPredicate(kLines[currentItemId]))
                currentItemId++;

            var blockStart = currentItemId;

            var blockItemCount = 0;

            while (currentItemId < kLines.Count && validityPredicate(kLines[currentItemId]) &&
                   (blockItemCount == 0 || kLines[currentItemId - 1].CloseTime.Add(GapSpan) ==
                       kLines[currentItemId].OpenTime))
            {
                blockItemCount++;
                currentItemId++;
            }

            if (blockItemCount > 0)
                result.Add((blockStart, blockStart + blockItemCount));
        }

        return result;
    }

    /// <summary>
    /// Returns "true" if the given <paramref name="kLine"/>,
    /// corresponding to a "one-month" granularity, is valid.
    /// </summary>
    private static bool IsValidMonth(KLine kLine)
    {
        if (kLine.OpenTime.Month != kLine.CloseTime.Month) return false;

        var daysInMonth = DateTime.DaysInMonth(kLine.OpenTime.Year, kLine.OpenTime.Month);
        var expectedMonthSpan = TimeSpan.FromDays(daysInMonth);

        return kLine.CloseTime.Subtract(kLine.OpenTime).Add(GapSpan) == expectedMonthSpan;
    }

    /// <summary>
    /// Fills chronological gaps in the given collection <paramref name="kLines"/>
    /// caused by invalid and/or missing k-lines of "one-month" granularity.
    /// </summary>
    private static KLine[] FillMonthGaps(IList<KLine> kLines, DateTime minTime)
    {
        var validSubBlocks = FindBlockBoundaries(kLines, IsValidMonth);

        if (validSubBlocks.Count == 0)
            return [];

        var initialOpenTime = kLines[validSubBlocks.First().BeginIdInclusive].OpenTime;

        var finalCloseTime = kLines[validSubBlocks.Last().EndIdExclusive - 1].CloseTime;

        var resultSize = (finalCloseTime.Year - initialOpenTime.Year) * 12 + finalCloseTime.Month - initialOpenTime.Month + 1;

        int OpenTimeToIndex(DateTime openTime) => (openTime.Year - initialOpenTime.Year) * 12 + openTime.Month - initialOpenTime.Month;

        var result = new KLine[resultSize];

        var currentKlineIndex = 0;
        DateTime prevSubBlockCloseTime = minTime;

        foreach (var subBlock in validSubBlocks)
        {
            var subBlockBeginId = OpenTimeToIndex(kLines[subBlock.BeginIdInclusive].OpenTime);

            while (currentKlineIndex < subBlockBeginId)
            {
                var opeTime = prevSubBlockCloseTime + GapSpan;
                prevSubBlockCloseTime += TimeSpan.FromDays(DateTime.DaysInMonth(opeTime.Year, opeTime.Month));
                result[currentKlineIndex++] = KLine.Invalid(opeTime, prevSubBlockCloseTime);
            }

            for (var originalId = subBlock.BeginIdInclusive; originalId < subBlock.EndIdExclusive; originalId++)
                result[currentKlineIndex++] = kLines[originalId];

            prevSubBlockCloseTime = kLines[subBlock.EndIdExclusive - 1].CloseTime;
        }

        return result;
    }

    /// <summary>
    /// Fills chronological gaps in the given collection <paramref name="kLines"/>
    /// caused by invalid and/or missing k-lines of the given <paramref name="granularity"/>.
    /// Can't handle a "one-month" granularity.
    /// </summary>
    private static KLine[] FillGaps(IList<KLine> kLines, ITimeGranularity granularity, DateTime minTime)
    {
        if (granularity.IsMonth)
            throw new InvalidOperationException("Can't handle \"one-month\" granularity");

        var span = granularity.Span;
        bool IsValid(KLine kLine) => kLine.CloseTime.Subtract(kLine.OpenTime).Add(GapSpan) == span;

        var validSubBlocks = FindBlockBoundaries(kLines, IsValid);

        if (validSubBlocks.Count == 0)
            return [];

        var baseOpenTime = kLines[validSubBlocks.First().BeginIdInclusive].OpenTime;

        var resultSize = (int)Math.Round((kLines[validSubBlocks.Last().EndIdExclusive - 1].CloseTime - baseOpenTime).Add(GapSpan) / span);

        int OpenTimeToIndex(DateTime openTime) => (int)Math.Round((openTime - baseOpenTime) / span);

        var result = new KLine[resultSize];

        var currentKlineIndex = 0;
        DateTime prevSubBlockCloseTime = minTime;

        foreach (var subBlock in validSubBlocks)
        {
            var subBlockBeginId = OpenTimeToIndex(kLines[subBlock.BeginIdInclusive].OpenTime);

            while (currentKlineIndex < subBlockBeginId)
            {
                var opeTime = prevSubBlockCloseTime + GapSpan;
                prevSubBlockCloseTime += span;
                result[currentKlineIndex++] = KLine.Invalid(opeTime, prevSubBlockCloseTime);
            }

            for (var originalId = subBlock.BeginIdInclusive; originalId < subBlock.EndIdExclusive; originalId++)
                result[currentKlineIndex++] = kLines[originalId];

            prevSubBlockCloseTime = kLines[subBlock.EndIdExclusive - 1].CloseTime;
        }

        return result;
    }

    /// <summary>
    /// Fills chronological gaps in the given collection <paramref name="kLines"/>
    /// caused by invalid and/or missing k-lines of the given <paramref name="granularity"/>.
    /// </summary>
    private static KLine[] FillGapsGeneral(IList<KLine> kLines, ITimeGranularity granularity, DateTime minTime) =>
        granularity.IsMonth ? FillMonthGaps(kLines, minTime) : FillGaps(kLines, granularity, minTime);

    /// <summary>
    /// Reads out all the "k-line" data that corresponds to the given <paramref name="symbol"/>
    /// and given <paramref name="granularity"/> starting from "now" back to the "first placement"
    /// moment and puts the data into the given <paramref name="storage"/> provided by the caller.
    /// </summary>
    public static async Task ReadOutData(string symbol, ITimeGranularity granularity, BinanceCache storage,
        IClient client, CachingProgressReport progressReportCallback)
    {
        var end = DateTime.UtcNow;
        var granularitySpan = granularity.Span;

        var container = storage.GetAssetViewThreadSafe(symbol).GetGridThreadSafe(granularity);

        var totalCachedSize = 0L;

        do
        {
            var begin = end.Subtract(client.MaxKLinesPerTime * granularitySpan).Max(client.MinTime);
            var kLines = await client.GetKLinesAsync(symbol, granularity,
                startTime: begin, endTime: end, limit: 1500);

            if (!kLines.Success)
                throw new Exception("Failed to get exchange info");

            if (kLines.KLines.Count == 0)
                break;

            var kLinesArray = FillGapsGeneral(kLines.KLines, granularity, client.MinTime);

            totalCachedSize += kLinesArray.Length * KLine.SizeInBytes;

            container.AppendThreadSafe(kLinesArray);

            progressReportCallback?.Invoke(granularity, container.Begin, container.End, totalCachedSize);

            if (end == kLinesArray[0].OpenTime)
                break;

            end = kLinesArray[0].OpenTime;

        } while (true);

        if (!container.CheckChronologicalIntegrity())
            throw new InvalidOperationException("Block-grid has failed the chronological integrity test");
    }
}