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

using Binance.Net.Clients;
using Binance.Net.Interfaces;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;
using BAnalyzerCore.Cache;
using BAnalyzerCore.DataStructures;
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
    /// that covers the given time interval represented with its
    /// begin-end points and which can be retrieved via a single
    /// call from Binance server.
    /// </summary>
    private static TimeInterval GetExtendedInterval(DateTime timeBegin,
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
    /// Returns an adjustment of the given <param name="interval"/>
    /// according to the given <param name="gapIndicator"/>.
    /// </summary>
    private static TimeInterval AdjustTimeInterval(TimeInterval interval, TimeInterval gapIndicator, DateTime localNow)
    {
        if (gapIndicator.Begin == DateTime.MinValue && gapIndicator.End == DateTime.MaxValue)
            return interval.Copy();

        if (gapIndicator.Begin != DateTime.MinValue && gapIndicator.End != DateTime.MaxValue)
            return gapIndicator.Copy();

        var span = interval.End - interval.Begin;

        return gapIndicator.Begin != DateTime.MinValue ?
            new TimeInterval(gapIndicator.Begin, gapIndicator.Begin.Add(span).Min(localNow)) :
            new TimeInterval(gapIndicator.End.Subtract(span), gapIndicator.End);
    }

    private readonly BinanceCache _cache = new();

    /// <summary>
    /// Returns "k-line" data.
    /// </summary>
    public async Task<IList<IBinanceKline>> GetCandleSticksAsync(DateTime timeBegin, DateTime timeEnd,
        KlineInterval granularity, string symbol, bool ensureLatestData)
    {
        // Binance client usually has troubles retrieving
        // k-lines that are less than a few seconds "old".
        // To overcome this obstacle we make our local
        // time to lag for 3 seconds. Experiments show that
        // a 1-second lag is not enough, 2-seconds lag is OK,
        // but to be on the safe side we go for the 3-seconds lag.
        DateTime localNow = DateTime.UtcNow.RoundToSeconds(3);
        timeEnd = timeEnd.Min(localNow);

        if (timeBegin >= timeEnd)
            return new List<IBinanceKline>();

        var assetCacheView = _cache.GetAssetViewThreadSafe(symbol, granularity);

        IList<IBinanceKline> RetrieveDataFromCache(out TimeInterval gapIndicator)
        {
            lock (assetCacheView) return assetCacheView.Retrieve(timeBegin, timeEnd, out gapIndicator);
        }

        var cachedResult = RetrieveDataFromCache(out var cacheGapIndicator);

        if (cachedResult != null)
        {
            if (ensureLatestData && cachedResult.Last().CloseTime >= localNow)
            {
                var lastKline = cachedResult.Last();
                // We need to update the last "k-line"
                var latestKline = (await _client.SpotApi.ExchangeData.GetUiKlinesAsync(symbol, granularity,
                    startTime: lastKline.OpenTime, endTime: lastKline.CloseTime)).Data.ToArray();

                var updatedKline = latestKline[^1];
                cachedResult[^1] = updatedKline;

                lock (assetCacheView)
                    assetCacheView.Append([updatedKline]);
            }

            return cachedResult;
        }

        var extendedInterval = GetExtendedInterval(timeBegin, timeEnd, localNow, granularity);
        var adjustedInterval = AdjustTimeInterval(extendedInterval, cacheGapIndicator, localNow);

        var kLines = await _client.SpotApi.ExchangeData.GetUiKlinesAsync(symbol, granularity,
            startTime: adjustedInterval.Begin, endTime: adjustedInterval.End, limit: 1500);

        if (!kLines.Success)
            throw new Exception("Failed to get exchange info");

        var kLinesArray = kLines.Data.ToArray();

        lock (assetCacheView)
        {
            if (kLinesArray.Length > 0)
            {
                assetCacheView.Append(kLinesArray);

                if (kLinesArray[0].OpenTime > adjustedInterval.Begin.Add(granularity.ToTimeSpan()))
                    assetCacheView.SetZeroTimePoint(kLinesArray[0].OpenTime);
            }
            else if (kLinesArray.Length == 0)
                assetCacheView.SetZeroTimePoint(timeEnd);

            return assetCacheView.Retrieve(timeBegin, timeEnd, out _);
        }
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
    /// Returns current price for the given symbol.
    /// </summary>
    public async Task<BinancePrice> GetCurrentPrice(string symbol)
    {
        if (symbol is null or "")
            return null;

        var price = await _client.SpotApi.ExchangeData.GetPriceAsync(symbol);

        return price.Success ? price.Data : null!;
    }
}