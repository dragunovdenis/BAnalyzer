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

    /// <summary>
    /// Returns boundaries of the maximal possible time interval
    /// that covers the given time interval represented with its
    /// begin-end points and which can be retrieved via a single
    /// call from Binance server.
    /// </summary>
    private static (DateTime TimeBeginExtended, DateTime TimeEndExtended) GetExtendedInterval(DateTime timeBegin,
        DateTime timeEnd, KlineInterval granularity)
    {
        var kLineSpan = granularity.ToTimeSpan();

        var requestedSpan = timeEnd - timeBegin;
        var kLinesRequested = (int)(requestedSpan / kLineSpan) + 1;

        var kLinesSpare = MaxKLinesPerTime - kLinesRequested;

        if (kLinesSpare < 0)
            throw new InvalidOperationException("Too large time interval requested");

        var kLinesToAddToTheEnd = Math.Min((int)((DateTime.UtcNow - timeEnd).Duration() / kLineSpan), kLinesSpare / 2);
        var kLinesToAddToTheBegin = kLinesSpare - kLinesToAddToTheEnd;

        if (kLinesRequested + kLinesToAddToTheEnd + kLinesToAddToTheBegin > MaxKLinesPerTime)
            throw new InvalidOperationException("Something went wrong!");

        var timeBeginExtended = timeBegin.Subtract(kLineSpan * kLinesToAddToTheBegin);
        var timeEndExtended = timeEnd.Add(kLineSpan * kLinesToAddToTheEnd);

        return (timeBeginExtended, timeEndExtended);
    }

    private readonly BinanceCache _cache = new();

    /// <summary>
    /// Returns "k-line" data.
    /// </summary>
    public async Task<IList<IBinanceKline>> GetCandleSticksAsync(DateTime timeBegin, DateTime timeEnd,
        KlineInterval granularity, string symbol, bool ensureLatestData)
    {
        if (timeBegin >= timeEnd)
            return new List<IBinanceKline>();

        timeEnd = timeEnd.Min(DateTime.UtcNow);

        var assetCacheView = _cache.GetAssetViewThreadSafe(symbol, granularity);

        IList<IBinanceKline> RetrieveDataFromCache()
        {
            lock (assetCacheView) return assetCacheView.Retrieve(timeBegin, timeEnd);
        }

        var cachedResult = RetrieveDataFromCache();

        if (cachedResult != null)
        {
            if (ensureLatestData && cachedResult.Last().CloseTime >= DateTime.UtcNow)
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

        var (extendedBegin, extendedEnd) = GetExtendedInterval(timeBegin, timeEnd, granularity);
        var kLines = await _client.SpotApi.ExchangeData.GetUiKlinesAsync(symbol, granularity,
            startTime: extendedBegin, endTime: extendedEnd, limit: 1500);

        if (!kLines.Success)
            throw new Exception("Failed to get exchange info");

        var result = kLines.Data.ToArray();

        lock (assetCacheView)
        {
            if (result.Length > 0)
            {
                assetCacheView.Append(result);

                if (result[0].OpenTime > timeBegin)
                    assetCacheView.SetZeroTimePoint(result[0].OpenTime);
            }
            else if (result.Length == 0)
                assetCacheView.SetZeroTimePoint(timeEnd);

            return assetCacheView.Retrieve(timeBegin, timeEnd);
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