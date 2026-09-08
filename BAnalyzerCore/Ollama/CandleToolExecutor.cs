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
using System.Text.Json;
using BAnalyzerCore.Clients;
using BAnalyzerCore.DataStructures;

namespace BAnalyzerCore.Ollama;

/// <summary>
/// Outcome of a single tool call: the text to be sent back to the model
/// together with a short description of what has been done, to be shown to the user.
/// </summary>
/// <param name="Content">Text to be put into the conversation as a "tool" message.</param>
/// <param name="Summary">Human-readable description of the call.</param>
/// <param name="Success">Whether the requested data could be retrieved.</param>
public sealed record ToolCallResult(string Content, string Summary, bool Success);

/// <summary>
/// Serves the requests for the data that a model issues on its own.
/// </summary>
public interface IToolExecutor
{
    /// <summary>
    /// Returns the declaration of the tool to be offered to a model, or "null"
    /// if there is nothing to offer.
    /// </summary>
    ToolDefinition GetToolDefinition();

    /// <summary>
    /// Executes the given <paramref name="call"/> and returns the result to be
    /// reported back to the model.
    /// </summary>
    Task<ToolCallResult> ExecuteAsync(ToolCall call, CancellationToken ct);
}

/// <summary>
/// Executes the requests for the candlestick data that a model issues on its own.
/// </summary>
/// <remarks>
/// Unlike <see cref="MarketContextBuilder"/>, which supplies a fixed amount of
/// data whether it is needed or not, this class lets the model decide what
/// exactly it wants to look at. The two are alternatives: the builder serves
/// the models that can't call tools.
/// </remarks>
public sealed class CandleToolExecutor : IToolExecutor
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
    public CandleToolExecutor(IClientCached client) => _client = client;

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
    public ToolDefinition GetToolDefinition()
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
                new ToolParameter("symbol", "string",
                    "Trading pair to retrieve the data for, for example \"BTCUSDT\" or \"ETHUSDT\"."),
                new ToolParameter("granularity", "string",
                    "Duration of a single candle.", granularities),
                new ToolParameter("count", "integer",
                    $"Number of the most recent candles to return, from 1 to {MaxCandlesPerCall}.")
            ]);
    }

    /// <summary>
    /// Executes the given <paramref name="call"/> and returns the result to be
    /// reported back to the model.
    /// </summary>
    /// <remarks>
    /// Never throws and never reports a failure by any means other than the
    /// returned record: a model must always get an answer it can react to,
    /// because an exception here would cost the user the entire conversation turn.
    /// </remarks>
    public async Task<ToolCallResult> ExecuteAsync(ToolCall call, CancellationToken ct)
    {
        if (call == null)
            return Failure("The tool call is empty.", "Invalid tool call");

        if (!string.Equals(call.Name, ToolName, StringComparison.OrdinalIgnoreCase))
            return Failure($"There is no tool named \"{call.Name}\". The only available tool is \"{ToolName}\".",
                $"Unknown tool \"{call.Name}\"");

        try
        {
            return await ExecuteImplAsync(call, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return Failure($"The data could not be retrieved: {e.Message}", "Market data request failed");
        }
    }

    /// <summary>
    /// Does the actual work of <see cref="ExecuteAsync"/>.
    /// </summary>
    private async Task<ToolCallResult> ExecuteImplAsync(ToolCall call, CancellationToken ct)
    {
        var symbol = ReadString(call.Arguments, "symbol")?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(symbol))
            return Failure("The \"symbol\" argument is missing.", "Market data request without a symbol");

        var granularity = ResolveGranularity(ReadString(call.Arguments, "granularity"));

        if (!granularity.IsValid)
            return Failure("The \"granularity\" argument is missing or not supported. Supported values are: " +
                           $"{string.Join(", ", _client.Granularities.Where(x => x.IsValid).Select(x => x.Name))}.",
                "Market data request with an unsupported granularity");

        var requested = ReadInt(call.Arguments, "count") ?? DefaultCandleCount;
        var count = Math.Clamp(requested, 1, MaxCandlesPerCall);

        var end = DateTime.UtcNow;
        var begin = end - granularity.Span * count;

        var (sticks, success) = await _client.GetKLinesAsync(symbol, granularity,
            begin, end, ensureLatestData: true).ConfigureAwait(false);

        if (!success || sticks == null)
            return Failure($"No data could be retrieved for \"{symbol}\". The trading pair may not exist " +
                           "on the exchange; try a different one.", $"No data for {symbol}");

        var valid = sticks.Where(x => x != null && !x.IsInvalid())
            .OrderBy(x => x.OpenTime).TakeLast(count).ToArray();

        if (valid.Length == 0)
            return Failure($"No candlestick data is available for \"{symbol}\" at the \"{granularity.Name}\" " +
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

    /// <summary>
    /// Returns the string value of the property with the given
    /// <paramref name="name"/> or "null" if there is no such property.
    /// </summary>
    private static string ReadString(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object ||
            !arguments.TryGetProperty(name, out var property))
            return null;

        return property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    }

    /// <summary>
    /// Returns the integer value of the property with the given
    /// <paramref name="name"/> or "null" if there is no such property.
    /// </summary>
    /// <remarks>
    /// Models are known to report numbers as strings, hence
    /// the extra parsing attempt.
    /// </remarks>
    private static int? ReadInt(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object ||
            !arguments.TryGetProperty(name, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
            return number;

        if (property.ValueKind == JsonValueKind.String &&
            int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    /// <summary>
    /// Builds a result reporting a failure to the model.
    /// </summary>
    private static ToolCallResult Failure(string content, string summary) =>
        new(content, summary, false);
}
