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
/// Tests of <see cref="MarketStatsTool"/>.
/// </summary>
[TestClass]
public class MarketStatsToolTest
{
    /// <summary>
    /// Returns the statistics of a pair that has gained 2.5 percent.
    /// </summary>
    private static IMarketStats CreateStats(double? weightedAveragePrice = 99.5, long? tradeCount = 1234) =>
        new MarketStats("BTCUSDT", LastPrice: 102.5, OpenPrice24H: 100.0, HighPrice24H: 105.0,
            LowPrice24H: 98.0, PriceChangePercentage24H: 2.5, BaseVolume24H: 1500.0,
            QuoteVolume24H: 150000.0, BestBidPrice: 102.4, BestBidQuantity: 3.0,
            BestAskPrice: 102.6, BestAskQuantity: 4.0, weightedAveragePrice, tradeCount,
            new DateTime(2026, 2, 11, 9, 31, 4, DateTimeKind.Utc));

    private static ToolCall Call(string arguments) => StubToolClient.Call(arguments, MarketStatsTool.ToolName);

    [TestMethod]
    public void GetDefinition_DeclaresTheSymbol()
    {
        // Act
        var definition = new MarketStatsTool(new StubToolClient([], marketStats: CreateStats())).GetDefinition();

        // Assert
        definition.Name.Should().Be(MarketStatsTool.ToolName);
        definition.Parameters.Select(x => x.Name).Should().Equal(["symbol"]);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReportsAllTheStatistics()
    {
        // Arrange
        var tool = new MarketStatsTool(new StubToolClient([], marketStats: CreateStats()));

        // Act
        var result = await tool.ExecuteAsync(Call("""{"symbol":"BTCUSDT"}"""), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Content.Should().Contain("BTCUSDT").
            And.Contain("102.5").
            And.Contain("105").
            And.Contain("98").
            And.Contain("1500").
            And.Contain("150000").
            And.Contain("102.4").
            And.Contain("102.6");
    }

    [TestMethod]
    public async Task ExecuteAsync_RendersTheChangeAsAPercentage()
    {
        // Arrange
        var tool = new MarketStatsTool(new StubToolClient([], marketStats: CreateStats()));

        // Act
        var result = await tool.ExecuteAsync(Call("""{"symbol":"BTCUSDT"}"""), CancellationToken.None);

        // Assert: the fraction of 0.025 must reach the model as "2.50%".
        result.Content.Should().Contain("2.50%");
    }

    [TestMethod]
    public async Task ExecuteAsync_OmitsTheValuesTheExchangeDoesNotReport()
    {
        // Arrange
        var tool = new MarketStatsTool(new StubToolClient([],
            marketStats: CreateStats(weightedAveragePrice: null, tradeCount: null)));

        // Act
        var result = await tool.ExecuteAsync(Call("""{"symbol":"BTCUSDT"}"""), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Content.Should().NotContain("weighted average").And.NotContain("number of trades");
    }

    [TestMethod]
    public async Task ExecuteAsync_ReportsTheValuesTheExchangeDoesProvide()
    {
        // Arrange
        var tool = new MarketStatsTool(new StubToolClient([], marketStats: CreateStats(tradeCount: 4321)));

        // Act
        var result = await tool.ExecuteAsync(Call("""{"symbol":"BTCUSDT"}"""), CancellationToken.None);

        // Assert
        result.Content.Should().Contain("24h number of trades").And.Contain("4321");
    }

    [TestMethod]
    public async Task ExecuteAsync_PassesTheSymbolUpperCasedToTheClient()
    {
        // Arrange
        var client = new StubToolClient([], marketStats: CreateStats());
        var tool = new MarketStatsTool(client);

        // Act
        await tool.ExecuteAsync(Call("""{"symbol":" btcusdt "}"""), CancellationToken.None);

        // Assert
        client.LastMarketStatsSymbol.Should().Be("BTCUSDT");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutSymbol_ReportsFailure()
    {
        // Arrange
        var tool = new MarketStatsTool(new StubToolClient([], marketStats: CreateStats()));

        // Act
        var result = await tool.ExecuteAsync(Call("""{}"""), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Content.Should().Contain("symbol");
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownSymbol_ReportsFailureInsteadOfThrowing()
    {
        // Arrange
        var tool = new MarketStatsTool(new StubToolClient([]));

        // Act
        var result = await tool.ExecuteAsync(Call("""{"symbol":"NOSUCHPAIR"}"""), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Content.Should().Contain("NOSUCHPAIR");
    }
}
