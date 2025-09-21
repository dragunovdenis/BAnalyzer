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

using BAnalyzerCore;
using BAnalyzerCore.Clients;
using BAnalyzerCore.DataStructures;

namespace BAnalyzer.DataStructures;

/// <summary>
/// Enumerates all the supported crypto-exchanges.
/// </summary>
public enum ExchangeId
{
    None = 0,
    Binance = 1,
    ByBit = 2,
}

/// <summary>
/// General interface for a multi-exchange client.
/// </summary>
public interface IMultiExchange : IDisposable
{
    /// <summary>
    /// Returns instance of the client that corresponds to the given <paramref name="id"/>;
    /// </summary>
    IClientCached this[ExchangeId id] { get; }

    /// <summary>
    /// Collection of supported granularities.
    /// </summary>
    IReadOnlyList<TimeGranularity> Granularities { get; }

    /// <summary>
    /// Union of the exchange symbols of all the clients.
    /// </summary>
    IReadOnlyList<string> Symbols { get; }

    /// <summary>
    /// Returns IDs of all the supported exchanges.
    /// </summary>
    IReadOnlyList<ExchangeId> Exchanges { get; }
}

/// <summary>
/// Implementation of the corresponding interface.
/// </summary>
internal class MultiExchange : IMultiExchange
{
    private readonly Dictionary<ExchangeId, IClientCached> _clients = new()
    {
        { ExchangeId.Binance, new ExchangeClient<ClientBinance>() },
        { ExchangeId.ByBit, new ExchangeClient<ClientByBit>() }
    };

    /// <inheritdoc/>
    public IReadOnlyList<TimeGranularity> Granularities { get; }

    /// <inheritdoc/>
    public IReadOnlyList<string> Symbols { get; }

    /// <inheritdoc/>
    public IReadOnlyList<ExchangeId> Exchanges { get; }

    /// <inheritdoc/>
    public IClientCached this[ExchangeId id] => _clients.ContainsKey(id) ? _clients[id] : null;

    /// <summary>
    /// Constructor.
    /// </summary>
    public MultiExchange()
    {
        Granularities = GetGranularities();
        Symbols = GetSymbols();
        Exchanges = _clients.Keys.ToArray();
    }

    /// <summary>
    /// Returns intersection of the granularity sets of all the exchange-clients.
    /// </summary>
    /// <returns></returns>
    private IReadOnlyList<TimeGranularity> GetGranularities()
    {
        HashSet<TimeGranularity> commonGranularities = null;

        foreach (var (_, c) in _clients)
        {
            if (commonGranularities == null)
                commonGranularities = [.. c.Granularities];
            else
                commonGranularities.IntersectWith(c.Granularities);
        }

        return commonGranularities!.OrderBy(x => x.Seconds).ToArray();
    }

    /// <summary>
    /// Returns union of all the symbols supported by all the exchange-clients.
    /// </summary>
    private IReadOnlyList<string> GetSymbols()
    {
        return Task.Run(async () =>
        {
            var symbolsPerClient = await Task.WhenAll(_clients.
                Select(x => x.Value.GetSymbolsAsync()).ToList());

            HashSet<string> allSymbols = null;

            foreach (var s in symbolsPerClient)
            {
                if (allSymbols == null)
                    allSymbols = [..s];
                else
                    allSymbols.UnionWith(s);
            }

            return allSymbols!.Where(x => x.EndsWith("USDT")).ToArray();
        }).Result;
    }

    /// <summary>
    /// Disposes the instance.
    /// </summary>
    public void Dispose()
    {
        foreach (var (_, c) in _clients)
            c.Dispose();
    }
}