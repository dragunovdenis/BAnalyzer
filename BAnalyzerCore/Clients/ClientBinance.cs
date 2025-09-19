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
using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects.Models;

namespace BAnalyzerCore.Clients;

/// <summary>
/// Implementation of <see cref="IClient"/> for <see cref="BinanceRestClient"/>.
/// </summary>
internal class ClientBinance : IClient
{
    private readonly BinanceRestClient _client = new();

    /// <inheritdoc/>
    public async Task<IList<string>> GetSymbolsAsync()
    {
        var exchangeInfo = await _client.SpotApi.ExchangeData.GetExchangeInfoAsync();
        if (!exchangeInfo.Success)
            throw new Exception("Failed to get exchange info");

        return exchangeInfo.Data.Symbols.Select(x => x.Name).ToArray();
    }

    /// <summary>
    /// Enumerates all the interval identifiers.
    /// </summary>
    private static IEnumerable<KlineInterval> GetAllIntervals() =>
        Enum.GetValues(typeof(KlineInterval)).Cast<KlineInterval>();

    /// <inheritdoc/>
    public IReadOnlyList<TimeGranularity> Granularities { get; } = GetAllIntervals().Where(x => (int)x >= 60).
        Select(x => new TimeGranularity(x.ToString(), (int)x)).ToArray();

    private readonly IDictionary<int, KlineInterval> _lookup = GetAllIntervals().ToDictionary(x => (int)x, x => x);

    /// <inheritdoc/>
    public async Task<(IList<KLine> KLines, bool Success)> GetKLinesAsync(string symbol, TimeGranularity granularity,
        DateTime? startTime, DateTime? endTime, int? limit)
    {
        var binanceResult = await _client.SpotApi.ExchangeData.GetKlinesAsync(symbol, _lookup[granularity.Seconds],
            startTime: startTime, endTime: endTime, limit: limit);

        if (!binanceResult.Success)
            return (new List<KLine>(), false);

        return (binanceResult.Data.Select(x => new KLine(x)).ToArray(), true);
    }

    /// <inheritdoc/>
    public async Task<IPriceData> GetPriceAsync(string symbol)
    {
        var price = await _client.SpotApi.ExchangeData.GetPriceAsync(symbol);

        return price.Success ? new PriceData((double)price.Data.Price, price.Data.Symbol, DateTime.UtcNow) : null;
    }

    /// <inheritdoc/>
    public async Task<IOrderBook> GetOrderBookAsync(string symbol)
    {
        var result = await _client.SpotApi.ExchangeData.GetOrderBookAsync(symbol);

        if (!result.Success)
            return null;

        IOrderBookEntry Convert(BinanceOrderBookEntry x) => new OrderBookEntry((double)x.Price, (double)x.Quantity);

        return new OrderBook(result.Data.Symbol, result.Data.Bids.Select(Convert).ToArray(), result.Data.Asks.Select(Convert).ToArray());
    }

    /// <inheritdoc/>
    public DateTime MinTime { get; } = new(2016, 1, 1);

    /// <inheritdoc/>
    public int MaxKLinesPerTime => 1000;

    /// <inheritdoc/>
    public void Dispose() => _client?.Dispose();
}