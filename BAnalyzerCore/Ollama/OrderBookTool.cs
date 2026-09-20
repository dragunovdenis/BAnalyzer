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

using System.Globalization;
using System.Text;
using BAnalyzerCore.Clients;
using BAnalyzerCore.DataStructures;

namespace BAnalyzerCore.Ollama;

/// <summary>
/// Serves the requests for the current order book that a model issues on its own.
/// </summary>
public sealed class OrderBookTool : ITool
{
    /// <summary>
    /// Name of the tool exposed to the models.
    /// </summary>
    public const string ToolName = "get_order_book";

    /// <summary>
    /// The largest number of the top levels a single call lists per side.
    /// </summary>
    public const int MaxLevelsPerSide = 50;

    private readonly IClientCached _client;

    /// <summary>
    /// Constructor.
    /// </summary>
    public OrderBookTool(IClientCached client) => _client = client;

    private const string OrderItemsCountParamName = "order_items_count";
    private const string SymbolParameterName = "symbol";

    /// <inheritdoc/>
    public ToolDefinition GetDefinition() => new(ToolName,
        "Returns the current order book (live \"bid\" and \"ask\" order book items) for a crypto " +
        "trading pair from the exchange. That is, the data reflecting the readiness of the market players to buy " +
        "(\"bid\") or sell (\"ask\") given crypto asset for a certain price. Call it to get an overview of " +
        "the current trading activity for the given crypto asset (symbol) in question, including but not limited " +
        "to its liquidity, buying/selling pressure etc. It is a snapshot of the resting orders right now and says " +
        "nothing about the past: use " + $"\"{CandleTool.ToolName}\" to get the historical price/volume data " +
        "for the given symbol. Call the tool frequently as the order book changes rapidly and the order book " +
        "returned by the previous call may be outdated.",
        [
            new ToolParameter(SymbolParameterName, "string",
                "Trading pair to retrieve the order book for, for example \"BTCUSDT\" or \"ETHUSDT\"."),
            new ToolParameter(OrderItemsCountParamName, "integer",
                $"Number of the top \"asks\" and \"bids\" order book items, from 1 to {MaxLevelsPerSide}.")
        ]);

    /// <inheritdoc/>
    public async Task<ToolCallResult> ExecuteAsync(ToolCall call, CancellationToken ct)
    {
        var symbol = ToolArguments.ReadString(call.Arguments, SymbolParameterName)?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(symbol))
            return ToolCallResult.Failure($"The {SymbolParameterName} argument is missing.",
                "Order book request without a symbol");

        var levelsRequested = ToolArguments.ReadInt(call.Arguments, OrderItemsCountParamName);

        if (levelsRequested == null)
            return ToolCallResult.Failure($"The {OrderItemsCountParamName} argument is missing.",
                "Order book request without a specified number of order book items");

        var levels = Math.Clamp(levelsRequested.Value, 1, MaxLevelsPerSide);

        var book = await _client.GetOrderBookAsync(symbol, levels).ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();

        var bids = book?.Bids ?? Array.Empty<IOrderBookEntry>();
        var asks = book?.Asks ?? Array.Empty<IOrderBookEntry>();

        if (bids.Length == 0 || asks.Length == 0)
            return ToolCallResult.Failure(
                $"No order book could be retrieved for \"{symbol}\". The trading pair may not exist " +
                "on the exchange; try a different one.", $"No order book for {symbol}");

        return new ToolCallResult(Render(symbol, bids, asks, levels),
            $"{symbol}: order book, {Math.Min(levels, Math.Min(bids.Length, asks.Length))} levels per side",
            true);
    }

    /// <summary>
    /// Renders the given order book as a short summary followed by the top
    /// <paramref name="levels"/> of each side.
    /// </summary>
    private static string Render(string symbol, IOrderBookEntry[] bids, IOrderBookEntry[] asks, int levels)
    {
        var c = CultureInfo.InvariantCulture;
        var builder = new StringBuilder();

        builder.AppendLine($"{symbol}: order book snapshot as of {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.");
        builder.AppendLine("This is the current state of the resting orders. It shows what the market is willing to do right now.");

        builder.AppendLine($"Top {Math.Min(levels, bids.Length)} bids (price | quantity):");
        AppendSide(builder, bids, levels, c);

        builder.AppendLine($"Top {Math.Min(levels, asks.Length)} asks (price | quantity):");
        AppendSide(builder, asks, levels, c);

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Appends the top <paramref name="levels"/> of the given side.
    /// </summary>
    private static void AppendSide(StringBuilder builder, IOrderBookEntry[] side, int levels, IFormatProvider c)
    {
        foreach (var entry in side.Take(levels))
            builder.AppendLine($"{entry.Price.ToString("G6", c)} | {entry.Quantity.ToString("G6", c)}");
    }
}
