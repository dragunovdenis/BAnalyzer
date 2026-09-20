//Copyright (c) 2026 Denys Dragunov, dragunovdenis@gmail.com
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

using System.Text.Json;
using BAnalyzerCore.Cache;
using BAnalyzerCore.Clients;
using BAnalyzerCore.DataStructures;
using BAnalyzerCore.Ollama;
using static BAnalyzerCore.Cache.ProgressReportDelegates;

namespace BAnalyzerCoreTest;

/// <summary>
/// An exchange client that returns the pre-defined data and fails everything else.
/// </summary>
/// <remarks>
/// Shared by the tests of the individual tools, so that each of them only has
/// to care about the data it is actually interested in.
/// </remarks>
internal sealed class StubToolClient(IList<KLine> data, bool success = true,
    IReadOnlyList<TimeGranularity> granularities = null, IOrderBook orderBook = null) : IClientCached
{
    private static readonly TimeGranularity Hourly = new("1h", 3600);
    private static readonly TimeGranularity Daily = new("1d", 86400);

    public IReadOnlyList<TimeGranularity> Granularities { get; } = granularities ?? [Hourly, Daily];

    public string LastSymbol { get; private set; }
    public TimeGranularity LastGranularity { get; private set; }
    public string LastOrderBookSymbol { get; private set; }

    public Task<(IList<KLine> Data, bool Success)> GetKLinesAsync(string symbol, TimeGranularity granularity,
        DateTime timeBegin, DateTime timeEnd, bool ensureLatestData)
    {
        LastSymbol = symbol;
        LastGranularity = granularity;

        return Task.FromResult((data, success));
    }

    public Task<IOrderBook> GetOrderBookAsync(string symbol, int maxOrderItems)
    {
        LastOrderBookSymbol = symbol;

        return Task.FromResult(orderBook);
    }

    /// <summary>
    /// Builds a tool call with the given arguments.
    /// </summary>
    public static ToolCall Call(string arguments, string name) =>
        new("call_1", name, JsonDocument.Parse(arguments).RootElement);

    public Task<IList<string>> GetSymbolsAsync() => throw new NotSupportedException();
    public IList<string> GetSymbols() => throw new NotSupportedException();
    public IPriceData GetCachedPrice(string symbol, int acceptableStalenessMs) => throw new NotSupportedException();
    public Task<IPriceData> GetPriceAsync(string symbol, int acceptableStalenessMs) => throw new NotSupportedException();
    public Task SaveCacheAsync(string folderPath, GeneralProgressReportingDelegate p) => throw new NotSupportedException();
    public Task LoadCacheAsync(string folderPath, GeneralProgressReportingDelegate p) => throw new NotSupportedException();
    public Task ReadOutData(string symbol, Cache storage, CachingProgressReport c) => throw new NotSupportedException();
    public void Dispose() { }
}
