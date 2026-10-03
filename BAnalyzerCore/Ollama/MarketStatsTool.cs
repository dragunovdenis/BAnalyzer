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
/// Serves the requests for the rolling 24-hour statistics that a model issues on its own.
/// </summary>
public sealed class MarketStatsTool : ITool
{
    /// <summary>
    /// Name of the tool exposed to the models.
    /// </summary>
    public const string ToolName = "get_market_stats";

    private readonly IClientCached _client;

    /// <summary>
    /// Constructor.
    /// </summary>
    public MarketStatsTool(IClientCached client) => _client = client;

    private const string SymbolParameterName = "symbol";

    /// <inheritdoc/>
    public ToolDefinition GetDefinition() => new(ToolName,
        "Returns the rolling 24-hour market statistics for a crypto trading pair from the exchange: " +
        "the latest price, the price 24 hours ago, the highest and the lowest price of the period, " +
        "the relative price change, the traded volume as well as the best (highest) \"bid\" and the " +
        "best (lowest) \"ask\" currently available. Call it to find out how the asset has been doing " +
        "over the last day and how actively it is being traded, for example to judge whether a price " +
        "move is large or ordinary, where the current price sits within the range of the day, how " +
        "liquid the pair is or how wide the gap between the buyers and the sellers is. It summarizes " +
        "the last 24 hours in a single snapshot. Use " + $"\"{CandleTool.ToolName}\" to get the " +
        "detailed historical price/volume data over an arbitrary period of time and " +
        $"\"{OrderBookTool.ToolName}\" to see the whole depth of the resting orders rather than only " +
        "the best ones. The statistics changes continuously, so the values returned by a previous " +
        "call may be outdated.",
        [
            new ToolParameter(SymbolParameterName, "string",
                "Trading pair to retrieve the statistics for, for example \"BTCUSDT\" or \"ETHUSDT\".")
        ]);

    /// <inheritdoc/>
    public async Task<ToolCallResult> ExecuteAsync(ToolCall call, CancellationToken ct)
    {
        var symbol = ToolArguments.ReadString(call.Arguments, SymbolParameterName)?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(symbol))
            return ToolCallResult.Failure($"The {SymbolParameterName} argument is missing.",
                "Market statistics request without a symbol");

        var stats = await _client.GetMarketStatsAsync(symbol).ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();

        if (stats == null)
            return ToolCallResult.Failure(
                $"No market statistics could be retrieved for \"{symbol}\". The trading pair may not exist " +
                "on the exchange; try a different one.", $"No market statistics for {symbol}");

        return new ToolCallResult(Render(stats), $"{symbol}: 24h market statistics", true);
    }

    /// <summary>
    /// Renders the given statistics as a compact block of labelled values.
    /// </summary>
    /// <remarks>
    /// The values an exchange does not report are left out altogether rather
    /// than shown as zeros, so that the model does not mistake the absence of
    /// the data for a measurement.
    /// </remarks>
    private static string Render(IMarketStats stats)
    {
        var c = CultureInfo.InvariantCulture;
        var builder = new StringBuilder();

        string Format(double value) => value.ToString("G6", c);

        builder.AppendLine($"{stats.Symbol}: rolling 24-hour statistics as of {stats.TimeStamp:yyyy-MM-dd HH:mm:ss} UTC.");
        builder.AppendLine($"Last price: {Format(stats.LastPrice)}");
        builder.AppendLine($"Price 24h ago: {Format(stats.OpenPrice24H)}");
        builder.AppendLine($"24h high: {Format(stats.HighPrice24H)}");
        builder.AppendLine($"24h low: {Format(stats.LowPrice24H)}");
        builder.AppendLine($"24h price change, %: {stats.PriceChangePercentage24H.ToString("F2", c)}%");
        builder.AppendLine($"24h volume (base asset): {Format(stats.BaseVolume24H)}");
        builder.AppendLine($"24h volume (quote asset): {Format(stats.QuoteVolume24H)}");
        builder.AppendLine($"Best bid (price | quantity): {Format(stats.BestBidPrice)} | {Format(stats.BestBidQuantity)}");
        builder.AppendLine($"Best ask (price | quantity): {Format(stats.BestAskPrice)} | {Format(stats.BestAskQuantity)}");

        if (stats.WeightedAveragePrice24H.HasValue)
            builder.AppendLine($"24h volume-weighted average price: {Format(stats.WeightedAveragePrice24H.Value)}");

        if (stats.TradeCount24H.HasValue)
            builder.AppendLine($"24h number of trades: {stats.TradeCount24H.Value.ToString(c)}");

        return builder.ToString().TrimEnd();
    }
}
