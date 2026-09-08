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
using FluentAssertions;
using static BAnalyzerCore.Cache.ProgressReportDelegates;

namespace BAnalyzerCoreTest;

/// <summary>
/// Tests of <see cref="CandleToolExecutor"/>.
/// </summary>
[TestClass]
public class CandleToolExecutorTest
{
    private static readonly TimeGranularity Hourly = new("1h", 3600);
    private static readonly TimeGranularity Daily = new("1d", 86400);

    /// <summary>
    /// A client that returns the pre-defined "k-lines" and fails everything else.
    /// </summary>
    private sealed class StubClient(IList<KLine> data, bool success = true,
        IReadOnlyList<TimeGranularity> granularities = null) : IClientCached
    {
        public IReadOnlyList<TimeGranularity> Granularities { get; } = granularities ?? [Hourly, Daily];

        public string LastSymbol { get; private set; }
        public TimeGranularity LastGranularity { get; private set; }

        public Task<(IList<KLine> Data, bool Success)> GetKLinesAsync(string symbol, TimeGranularity granularity,
            DateTime timeBegin, DateTime timeEnd, bool ensureLatestData)
        {
            LastSymbol = symbol;
            LastGranularity = granularity;

            return Task.FromResult((data, success));
        }

        public Task<IList<string>> GetSymbolsAsync() => throw new NotSupportedException();
        public IList<string> GetSymbols() => throw new NotSupportedException();
        public Task<IOrderBook> GetOrderBookAsync(string symbol) => throw new NotSupportedException();
        public IPriceData GetCachedPrice(string symbol, int acceptableStalenessMs) => throw new NotSupportedException();
        public Task<IPriceData> GetPriceAsync(string symbol, int acceptableStalenessMs) => throw new NotSupportedException();
        public Task SaveCacheAsync(string folderPath, GeneralProgressReportingDelegate p) => throw new NotSupportedException();
        public Task LoadCacheAsync(string folderPath, GeneralProgressReportingDelegate p) => throw new NotSupportedException();
        public Task ReadOutData(string symbol, Cache storage, CachingProgressReport c) => throw new NotSupportedException();
        public void Dispose() { }
    }

    /// <summary>
    /// Returns the given number of consecutive hourly "k-lines".
    /// </summary>
    private static IList<KLine> CreateCandles(int count)
    {
        var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return Enumerable.Range(0, count).Select(i => new KLine
        {
            OpenTime = start.AddHours(i),
            CloseTime = start.AddHours(i + 1),
            OpenPrice = 100.0 + i,
            HighPrice = 110.0 + i,
            LowPrice = 90.0 + i,
            ClosePrice = 105.0 + i,
            Volume = 1000.0 + i
        }).ToList();
    }

    /// <summary>
    /// Builds a tool call with the given arguments.
    /// </summary>
    private static ToolCall Call(string arguments, string name = CandleToolExecutor.ToolName) =>
        new("call_1", name, JsonDocument.Parse(arguments).RootElement);

    [TestMethod]
    public void GetToolDefinition_RestrictsGranularityToTheSupportedValues()
    {
        var client = new StubClient(CreateCandles(1));
        var executor = new CandleToolExecutor(client);

        var definition = executor.GetToolDefinition();

        definition.Name.Should().Be(CandleToolExecutor.ToolName);

        var granularity = definition.Parameters.Single(x => x.Name == "granularity");

        granularity.AllowedValues.Should().Equal(["1h", "1d"],
            "the model must not be able to ask for an interval the exchange can't serve");
    }

    [TestMethod]
    public void GetToolDefinition_WithoutGranularities_ReturnsNull()
    {
        var executor = new CandleToolExecutor(new StubClient(CreateCandles(1), granularities: []));

        executor.GetToolDefinition().Should().BeNull();
    }

    [TestMethod]
    public async Task ExecuteAsync_ReturnsTheRequestedCandles()
    {
        var client = new StubClient(CreateCandles(10));
        var executor = new CandleToolExecutor(client);

        var result = await executor.ExecuteAsync(
            Call("""{"symbol":"btcusdt","granularity":"1d","count":10}"""), CancellationToken.None);

        result.Success.Should().BeTrue();

        client.LastSymbol.Should().Be("BTCUSDT", "the symbol must be normalized");
        client.LastGranularity.Name.Should().Be("1d");

        var rows = result.Content.Split(Environment.NewLine)
            .Where(x => x.StartsWith("2025-01-01", StringComparison.Ordinal)).ToArray();

        rows.Should().HaveCount(10);
    }

    [TestMethod]
    public async Task ExecuteAsync_ClampsTheCountAndSaysSo()
    {
        var executor = new CandleToolExecutor(new StubClient(CreateCandles(3)));

        var result = await executor.ExecuteAsync(
            Call("""{"symbol":"BTCUSDT","granularity":"1h","count":100000}"""), CancellationToken.None);

        result.Success.Should().BeTrue();

        // Silently returning less than was asked for would let the model draw
        // conclusions from a series it believes to be longer than it is.
        result.Content.Should().Contain(CandleToolExecutor.MaxCandlesPerCall.ToString());
        result.Content.Should().Contain("100000");
    }

    [TestMethod]
    public async Task ExecuteAsync_ReturnsOnlyTheMostRecentCandles()
    {
        var executor = new CandleToolExecutor(new StubClient(CreateCandles(10)));

        var result = await executor.ExecuteAsync(
            Call("""{"symbol":"BTCUSDT","granularity":"1h","count":3}"""), CancellationToken.None);

        var rows = result.Content.Split(Environment.NewLine)
            .Where(x => x.StartsWith("2025-01-01", StringComparison.Ordinal)).ToArray();

        rows.Should().HaveCount(3);
        rows[^1].Should().Contain("09:00");
    }

    [TestMethod]
    public async Task ExecuteAsync_AcceptsTheCountReportedAsAString()
    {
        var executor = new CandleToolExecutor(new StubClient(CreateCandles(5)));

        var result = await executor.ExecuteAsync(
            Call("""{"symbol":"BTCUSDT","granularity":"1h","count":"5"}"""), CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    [TestMethod]
    public async Task ExecuteAsync_UnsupportedGranularity_ReportsFailureWithTheAlternatives()
    {
        var executor = new CandleToolExecutor(new StubClient(CreateCandles(5)));

        var result = await executor.ExecuteAsync(
            Call("""{"symbol":"BTCUSDT","granularity":"3s","count":5}"""), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Content.Should().Contain("1h").And.Contain("1d");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutSymbol_ReportsFailure()
    {
        var executor = new CandleToolExecutor(new StubClient(CreateCandles(5)));

        var result = await executor.ExecuteAsync(
            Call("""{"granularity":"1h","count":5}"""), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Content.Should().Contain("symbol");
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownTool_ReportsFailure()
    {
        var executor = new CandleToolExecutor(new StubClient(CreateCandles(5)));

        var result = await executor.ExecuteAsync(
            Call("""{"symbol":"BTCUSDT"}""", "get_order_book"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Content.Should().Contain(CandleToolExecutor.ToolName);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownSymbol_ReportsFailureInsteadOfThrowing()
    {
        var executor = new CandleToolExecutor(new StubClient([], success: false));

        var result = await executor.ExecuteAsync(
            Call("""{"symbol":"NOSUCHPAIR","granularity":"1h","count":5}"""), CancellationToken.None);

        // A hallucinated pair must cost a round trip, not the conversation turn.
        result.Success.Should().BeFalse();
        result.Content.Should().Contain("NOSUCHPAIR");
    }

    [TestMethod]
    public async Task ExecuteAsync_DeclaresThatTheDataIsNotATradeTape()
    {
        var executor = new CandleToolExecutor(new StubClient(CreateCandles(2)));

        var result = await executor.ExecuteAsync(
            Call("""{"symbol":"BTCUSDT","granularity":"1h","count":2}"""), CancellationToken.None);

        result.Content.Should().Contain("not a raw trade tape");
    }
}
