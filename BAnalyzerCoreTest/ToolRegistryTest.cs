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

using BAnalyzerCore.Ollama;
using FluentAssertions;

namespace BAnalyzerCoreTest;

/// <summary>
/// Tests of <see cref="ToolRegistry"/>.
/// </summary>
[TestClass]
public class ToolRegistryTest
{
    /// <summary>
    /// A tool that records the calls it was given and answers them as told.
    /// </summary>
    private sealed class StubTool(string name, Exception failure = null) : ITool
    {
        public int CallCount { get; private set; }

        public ToolDefinition Definition { get; set; } =
            new(name, $"Does {name}.", [new ToolParameter("symbol", "string", "Trading pair.")]);

        public ToolDefinition GetDefinition() => Definition;

        public Task<ToolCallResult> ExecuteAsync(ToolCall call, CancellationToken ct)
        {
            CallCount++;

            if (failure != null)
                throw failure;

            return Task.FromResult(new ToolCallResult($"Data of {name}", $"Summary of {name}", true));
        }
    }

    private static ToolCall Call(string name) =>
        StubToolClient.Call("""{"symbol":"BTCUSDT"}""", name);

    [TestMethod]
    public void GetToolDefinitions_SkipsTheToolsThatCantBeOffered()
    {
        // Arrange
        var unavailable = new StubTool("get_order_book") { Definition = null };
        var registry = new ToolRegistry(new StubTool("get_candles"), unavailable);

        // Act / Assert
        registry.GetToolDefinitions().Select(x => x.Name).Should().Equal(["get_candles"],
            "a tool that can't be served must not be advertised to the model");
    }

    [TestMethod]
    public async Task ExecuteAsync_RoutesTheCallToTheAddressedTool()
    {
        // Arrange
        var candles = new StubTool("get_candles");
        var book = new StubTool("get_order_book");
        var registry = new ToolRegistry(candles, book);
        
        // Act
        var result = await registry.ExecuteAsync(Call("get_order_book"), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Content.Should().Contain("get_order_book");

        book.CallCount.Should().Be(1);
        candles.CallCount.Should().Be(0);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnknownTool_ListsTheAvailableOnes()
    {
        // Arrange
        var candles = new StubTool("get_candles");
        var book = new StubTool("get_order_book");
        var registry = new ToolRegistry(candles, book);

        // Act
        var result = await registry.ExecuteAsync(Call("get_the_future"), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Content.Should().Contain("get_candles").And.Contain("get_order_book");
    }

    [TestMethod]
    public async Task ExecuteAsync_ThrowingTool_ReportsFailureInsteadOfThrowing()
    {
        // Arrange
        var registry = new ToolRegistry(new StubTool("get_candles", new InvalidOperationException("no network")));

        // Act
        var result = await registry.ExecuteAsync(Call("get_candles"), CancellationToken.None);
        
        // Assert
        result.Success.Should().BeFalse();
        result.Content.Should().Contain("no network");
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyCall_ReportsFailure()
    {
        // Arrange
        var registry = new ToolRegistry(new StubTool("get_candles"));
        
        // Act
        var result = await registry.ExecuteAsync(null, CancellationToken.None);
        
        // Assert
        result.Success.Should().BeFalse();
    }
}
