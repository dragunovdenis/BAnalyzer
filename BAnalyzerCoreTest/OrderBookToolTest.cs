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
/// Tests of <see cref="OrderBookTool"/>.
/// </summary>
[TestClass]
public class OrderBookToolTest
{
    /// <summary>
    /// An order book entry built by hand.
    /// </summary>
    private sealed record Entry(double Price, double Quantity) : IOrderBookEntry;

    /// <summary>
    /// An order book built by hand.
    /// </summary>
    private sealed record Book(string Symbol, IOrderBookEntry[] Bids, IOrderBookEntry[] Asks) : IOrderBook;

    /// <summary>
    /// Returns an order book with the given number of levels per side.
    /// </summary>
    private static IOrderBook CreateBook(int levels = 5)
    {
        var bids = Enumerable.Range(0, levels).Select(i => new Entry(100.0 - i, 2.0)).ToArray<IOrderBookEntry>();
        var asks = Enumerable.Range(0, levels).Select(i => new Entry(101.0 + i, 1.0)).ToArray<IOrderBookEntry>();

        return new Book("BTCUSDT", bids, asks);
    }

    private static ToolCall Call(string arguments) => StubToolClient.Call(arguments, OrderBookTool.ToolName);

    [TestMethod]
    public void GetDefinition_DeclaresTheSymbolAndTheLevels()
    {
        // Act
        var definition = new OrderBookTool(new StubToolClient([], orderBook: CreateBook())).GetDefinition();

        // Assert
        definition.Name.Should().Be(OrderBookTool.ToolName);
        definition.Parameters.Select(x => x.Name).Should().Equal(["symbol", "order_items_count"]);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListsOnlyTheRequestedNumberOfLevels()
    {
        // Arrange
        var tool = new OrderBookTool(new StubToolClient([], orderBook: CreateBook(10)));

        // Act
        var result = await tool.ExecuteAsync(Call("""{"symbol":"BTCUSDT","order_items_count":2}"""), CancellationToken.None);

        // Assert
        result.Content.Should().Contain("Top 2 bids").And.Contain("Top 2 asks");

        result.Content.Split(Environment.NewLine)
            .Count(x => x.Contains(" | ") && char.IsDigit(x[0])).Should().Be(2 * 2);
    }

    [TestMethod]
    public async Task ExecuteAsync_ClampsTheNumberOfLevels()
    {
        // Arrange
        var tool = new OrderBookTool(new StubToolClient([], orderBook: CreateBook(100)));

        // Act
        var result = await tool.ExecuteAsync(Call("""{"symbol":"BTCUSDT","order_items_count":100000}"""),
            CancellationToken.None);

        // Assert
        result.Content.Should().Contain($"Top {OrderBookTool.MaxLevelsPerSide} bids");
        result.Content.Should().Contain($"Top {OrderBookTool.MaxLevelsPerSide} asks");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutSymbol_ReportsFailure()
    {
        // Arrange
        var tool = new OrderBookTool(new StubToolClient([], orderBook: CreateBook()));

        // Act
        var result = await tool.ExecuteAsync(Call("""{"order_items_count":5}"""), CancellationToken.None);
        
        // Assert   
        result.Success.Should().BeFalse();
        result.Content.Should().Contain("symbol");
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownSymbol_ReportsFailureInsteadOfThrowing()
    {
        // Arrange
        var tool = new OrderBookTool(new StubToolClient([]));

        // Act
        var result = await tool.ExecuteAsync(Call("""{"symbol":"NOSUCHPAIR","order_items_count":10}"""), CancellationToken.None);

        // Assert   
        result.Success.Should().BeFalse();
        result.Content.Should().Contain("NOSUCHPAIR");
    }
}
