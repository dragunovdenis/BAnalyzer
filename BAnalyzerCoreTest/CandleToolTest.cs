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

using BAnalyzerCore.DataStructures;
using BAnalyzerCore.Ollama;
using FluentAssertions;

namespace BAnalyzerCoreTest;

/// <summary>
/// Tests of <see cref="CandleTool"/>.
/// </summary>
[TestClass]
public class CandleToolTest
{
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

    [TestMethod]
    public void GetDefinition_RestrictsGranularityToTheSupportedValues()
    {
        // Arrange
        var client = new StubToolClient(CreateCandles(1));
        var tool = new CandleTool(client);

        // Act
        var definition = tool.GetDefinition();

        // Assert
        definition.Name.Should().Be(CandleTool.ToolName);
        var granularity = definition.Parameters.Single(x => x.Name == "granularity");

        granularity.AllowedValues.Should().Equal(["1h", "1d"],
            "the model must not be able to ask for an interval the exchange can't serve");
    }

    [TestMethod]
    public void GetDefinition_WithoutGranularities_ReturnsNull()
    {
        // Arrange
        var tool = new CandleTool(new StubToolClient(CreateCandles(1), granularities: []));

        // Act
        var definition = tool.GetDefinition();

        // Assert
        definition.Should().BeNull();
    }

    [TestMethod]
    public async Task ExecuteAsync_ReturnsTheRequestedCandles()
    {
        // Arrange
        var client = new StubToolClient(CreateCandles(10));
        var tool = new CandleTool(client);

        // Act
        var result = await tool.ExecuteAsync(
            StubToolClient.Call("""{"symbol":"btcusdt","granularity":"1d","count":10}""",
                CandleTool.ToolName), CancellationToken.None);

        // Assert
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
        // Arrange
        var tool = new CandleTool(new StubToolClient(CreateCandles(3)));

        // Act
        var result = await tool.ExecuteAsync(
            StubToolClient.Call("""{"symbol":"BTCUSDT","granularity":"1h","count":100000}""",
                CandleTool.ToolName), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        // Silently returning less than was asked for would let the model draw
        // conclusions from a series it believes to be longer than it is.
        result.Content.Should().Contain(CandleTool.MaxCandlesPerCall.ToString());
        result.Content.Should().Contain("100000");
    }

    [TestMethod]
    public async Task ExecuteAsync_ReturnsOnlyTheMostRecentCandles()
    {
        // Arrange
        var tool = new CandleTool(new StubToolClient(CreateCandles(10)));

        // Act
        var result = await tool.ExecuteAsync(
            StubToolClient.Call("""{"symbol":"BTCUSDT","granularity":"1h","count":3}""",
                CandleTool.ToolName), CancellationToken.None);

        // Assert
        var rows = result.Content.Split(Environment.NewLine)
            .Where(x => x.StartsWith("2025-01-01", StringComparison.Ordinal)).ToArray();

        rows.Should().HaveCount(3);
        rows[^1].Should().Contain("09:00");
    }

    [TestMethod]
    public async Task ExecuteAsync_AcceptsTheCountReportedAsAString()
    {
        // Arrange
        var tool = new CandleTool(new StubToolClient(CreateCandles(5)));

        // Act
        var result = await tool.ExecuteAsync(
            StubToolClient.Call("""{"symbol":"BTCUSDT","granularity":"1h","count":"5"}""",
                CandleTool.ToolName), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
    }

    [TestMethod]
    public async Task ExecuteAsync_UnsupportedGranularity_ReportsFailureWithTheAlternatives()
    {
        // Arrange
        var tool = new CandleTool(new StubToolClient(CreateCandles(5)));

        // Act
        var result = await tool.ExecuteAsync(
            StubToolClient.Call("""{"symbol":"BTCUSDT","granularity":"3s","count":5}""",
                CandleTool.ToolName), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Content.Should().Contain("1h").And.Contain("1d");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutSymbol_ReportsFailure()
    {
        // Arrange
        var tool = new CandleTool(new StubToolClient(CreateCandles(5)));

        // Act
        var result = await tool.ExecuteAsync(
            StubToolClient.Call("""{"granularity":"1h","count":5}""",
                CandleTool.ToolName), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Content.Should().Contain("symbol");
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownSymbol_ReportsFailureInsteadOfThrowing()
    {
        // Arrange
        var registry = new ToolRegistry(new CandleTool(new StubToolClient([], success: false)));

        // Act
        var result = await registry.ExecuteAsync(
            StubToolClient.Call("""{"symbol":"NOSUCHPAIR","granularity":"1h","count":5}""",
                CandleTool.ToolName), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Content.Should().Contain("NOSUCHPAIR");
    }

    [TestMethod]
    public async Task ExecuteAsync_DeclaresThatTheDataIsNotATradeTape()
    {
        // Arrange  
        var tool = new CandleTool(new StubToolClient(CreateCandles(2)));

        // Act
        var result = await tool.ExecuteAsync(
            StubToolClient.Call("""{"symbol":"BTCUSDT","granularity":"1h","count":2}""",
                CandleTool.ToolName), CancellationToken.None);

        // Assert
        result.Content.Should().Contain("not a raw trade tape");
    }
}
