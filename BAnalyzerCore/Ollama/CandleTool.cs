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
/// Serves the requests for the candlestick data that a model issues on its own.
/// </summary>
public sealed class CandleTool : ITool
{
    /// <summary>
    /// Name of the tool exposed to the models.
    /// </summary>
    public const string ToolName = "get_candles";

    /// <summary>
    /// The largest number of candles a single call can deliver. A model is
    /// free to ask for more, in which case the request is satisfied partially
    /// (and the model is told so).
    /// </summary>
    public const int MaxCandlesPerCall = 2000;

    /// <summary>
    /// The number of candles to deliver if the model does not specify it.
    /// </summary>
    private const int DefaultCandleCount = 24;

    private readonly IClientCached _client;

    /// <summary>
    /// Constructor.
    /// </summary>
    public CandleTool(IClientCached client) => _client = client;

    private const string SymbolParameterName = "symbol";
    private const string GranularityParameterName = "granularity";
    private const string CountParameterName = "count";

    /// <summary>
    /// Returns the declaration of the tool to be offered to a model, or "null"
    /// if the exchange does not report any granularity to choose from.
    /// </summary>
    /// <remarks>
    /// The set of the granularities is taken from the exchange client, so the
    /// model is offered exactly the intervals that can actually be served. It
    /// is therefore unable to ask for anything unsupported, which removes a
    /// whole class of failures instead of handling it.
    /// </remarks>
    public ToolDefinition GetDefinition()
    {
        var granularities = _client?.Granularities?
            .Where(x => x.IsValid).Select(x => x.Name).ToArray();

        if (granularities == null || granularities.Length == 0)
            return null;

        return new ToolDefinition(ToolName,
            "Returns historical OHLC candlestick data for a crypto trading pair from the exchange. " +
            "Call it whenever the conversation requires actual market data, choosing the granularity " +
            "and the number of candles that suit the question: for example 24 candles of \"1h\" for the " +
            "last day, 30 candles of \"1d\" for the last month, or 52 candles of \"1w\" for the last year. " +
            "The tool can be called for any trading pair, including the ones the user has not mentioned.",
            [
                new ToolParameter(SymbolParameterName, "string",
                    "Trading pair to retrieve the data for, for example \"BTCUSDT\" or \"ETHUSDT\"."),
                new ToolParameter(GranularityParameterName, "string",
                    "Duration of a single candle.", granularities),
                new ToolParameter(CountParameterName, "integer",
                    $"Number of the most recent candles to return, from 1 to {MaxCandlesPerCall}.")
            ]);
    }

    /// <summary>
    /// Executes the given <paramref name="call"/> and returns the result to be
    /// reported back to the model.
    /// </summary>
    public async Task<ToolCallResult> ExecuteAsync(ToolCall call, CancellationToken ct)
    {
        var symbol = ToolArguments.ReadString(call.Arguments, SymbolParameterName)?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(symbol))
            return ToolCallResult.Failure($"The {SymbolParameterName} argument is missing.",
                "Market data request without a symbol");

        var granularity = ResolveGranularity(ToolArguments.ReadString(call.Arguments, GranularityParameterName));

        if (!granularity.IsValid)
            return ToolCallResult.Failure(
                $"The {GranularityParameterName} argument is missing or not supported. Supported values are: " +
                $"{string.Join(", ", _client.Granularities.Where(x => x.IsValid).Select(x => x.Name))}.",
                "Market data request with an unsupported granularity");

        var requested = ToolArguments.ReadInt(call.Arguments, CountParameterName) ?? DefaultCandleCount;
        var count = Math.Clamp(requested, 1, MaxCandlesPerCall);

        var end = DateTime.UtcNow;
        var begin = end - granularity.Span * count;

        var (sticks, success) = await _client.GetKLinesAsync(symbol, granularity,
            begin, end, ensureLatestData: true).ConfigureAwait(false);

        if (!success || sticks == null)
            return ToolCallResult.Failure($"No data could be retrieved for \"{symbol}\". The trading pair may not exist " +
                                          "on the exchange; try a different one.", $"No data for {symbol}");

        var valid = sticks.Where(x => x != null && !x.IsInvalid())
            .OrderBy(x => x.OpenTime).TakeLast(count).ToArray();

        if (valid.Length == 0)
            return ToolCallResult.Failure($"No candlestick data is available for \"{symbol}\" at the \"{granularity.Name}\" " +
                                          "granularity.", $"No data for {symbol}");

        return new ToolCallResult(Render(symbol, granularity, valid, requested, count, end),
            $"{symbol}: {valid.Length} × {granularity.Name}", true);
    }

    /// <summary>
    /// Renders the given candles in the same tabular form the static market
    /// context uses, so that a model sees one format regardless of how the
    /// data has found its way into the conversation.
    /// </summary>
    private static string Render(string symbol, TimeGranularity granularity, IReadOnlyList<KLine> candles,
        int requested, int granted, DateTime end)
    {
        var c = CultureInfo.InvariantCulture;
        var builder = new StringBuilder();

        builder.AppendLine($"{symbol}: {candles.Count} {granularity.Name} candles up to {end:yyyy-MM-dd HH:mm} UTC.");

        if (requested > granted)
            builder.AppendLine($"Note: {requested} candles were requested, but a single call returns at most " +
                               $"{MaxCandlesPerCall}. Only the {granted} most recent ones are listed below.");

        builder.AppendLine("This is historical OHLC candlestick data from the exchange, not a raw trade tape: " +
                           "there are no individual trades, order book snapshots or tick data here.");
        builder.AppendLine("open_time_utc | open | high | low | close | volume");

        foreach (var stick in candles)
            builder.AppendLine($"{stick.OpenTime:yyyy-MM-dd HH:mm} | " +
                               $"{stick.OpenPrice.ToString("G6", c)} | " +
                               $"{stick.HighPrice.ToString("G6", c)} | " +
                               $"{stick.LowPrice.ToString("G6", c)} | " +
                               $"{stick.ClosePrice.ToString("G6", c)} | " +
                               $"{stick.Volume.ToString("G6", c)}");

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Returns the granularity with the given <paramref name="name"/> or an
    /// invalid one if the exchange does not support it.
    /// </summary>
    private TimeGranularity ResolveGranularity(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || _client.Granularities == null)
            return TimeGranularity.Invalid;

        return _client.Granularities.FirstOrDefault(x => x.IsValid &&
            string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase), TimeGranularity.Invalid);
    }
}
