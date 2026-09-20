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
using System.Text.Json;

namespace BAnalyzerCore.Ollama;

/// <summary>
/// Outcome of a single tool call: the text to be sent back to the model
/// together with a short description of what has been done, to be shown to the user.
/// </summary>
/// <param name="Content">Text to be put into the conversation as a "tool" message.</param>
/// <param name="Summary">Human-readable description of the call.</param>
/// <param name="Success">Whether the requested data could be retrieved.</param>
public sealed record ToolCallResult(string Content, string Summary, bool Success)
{
    /// <summary>
    /// Builds a result reporting a failure to the model.
    /// </summary>
    /// <remarks>
    /// A failure is still an answer: the model is told what went wrong in the
    /// very place it expects the data, so that it can correct itself instead
    /// of being left waiting.
    /// </remarks>
    public static ToolCallResult Failure(string content, string summary) => new(content, summary, false);
}

/// <summary>
/// A single "tool" a model can be offered.
/// </summary>
/// <remarks>
/// Implementations are free to throw: the failure is turned into a
/// <see cref="ToolCallResult"/> by <see cref="ToolRegistry"/>, which also
/// makes sure that a tool is only ever given the calls addressed to it.
/// </remarks>
public interface ITool
{
    /// <summary>
    /// Returns the declaration of the tool, or "null" if it can't be offered
    /// at the moment (for example because the exchange does not report the
    /// values one of its parameters is restricted to).
    /// </summary>
    ToolDefinition GetDefinition();

    /// <summary>
    /// Serves the given <paramref name="call"/>.
    /// </summary>
    Task<ToolCallResult> ExecuteAsync(ToolCall call, CancellationToken ct);
}

/// <summary>
/// Serves the requests for the data that a model issues on its own.
/// </summary>
public interface IToolExecutor
{
    /// <summary>
    /// Returns the declarations of the tools to be offered to a model. Empty
    /// if there is nothing to offer.
    /// </summary>
    IReadOnlyList<ToolDefinition> GetToolDefinitions();

    /// <summary>
    /// Executes the given <paramref name="call"/> and returns the result to be
    /// reported back to the model.
    /// </summary>
    Task<ToolCallResult> ExecuteAsync(ToolCall call, CancellationToken ct);
}

/// <summary>
/// The set of tools a model is offered: collects their declarations and routes
/// each call to the tool it is addressed to.
/// </summary>
/// <remarks>
/// Everything that is the same for every tool lives here (the name lookup, the
/// guarantee that a call is always answered, the reporting of an unknown tool),
/// so that an individual <see cref="ITool"/> consists of nothing but its
/// declaration and the work it does.
/// </remarks>
public sealed class ToolRegistry : IToolExecutor
{
    private readonly IReadOnlyList<ITool> _tools;

    /// <summary>
    /// Constructor.
    /// </summary>
    public ToolRegistry(params ITool[] tools) =>
        _tools = tools?.Where(x => x != null).ToArray() ?? [];

    /// <inheritdoc/>
    public IReadOnlyList<ToolDefinition> GetToolDefinitions() =>
        _tools.Select(x => x.GetDefinition()).Where(x => x != null).ToArray();

    /// <inheritdoc/>
    /// <remarks>
    /// Never throws and never reports a failure by any means other than the
    /// returned record: a model must always get an answer it can react to,
    /// because an exception here would cost the user the entire conversation turn.
    /// </remarks>
    public async Task<ToolCallResult> ExecuteAsync(ToolCall call, CancellationToken ct)
    {
        if (call == null)
            return ToolCallResult.Failure("The tool call is empty.", "Invalid tool call");

        var tool = Resolve(call.Name);

        if (tool == null)
            return ToolCallResult.Failure(
                $"There is no tool named \"{call.Name}\". {DescribeAvailableTools()}",
                $"Unknown tool \"{call.Name}\"");

        try
        {
            return await tool.ExecuteAsync(call, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return ToolCallResult.Failure($"The data could not be retrieved: {e.Message}",
                "Market data request failed");
        }
    }

    /// <summary>
    /// Returns the tool with the given <paramref name="name"/> or "null" if
    /// there is none. Only the tools that are currently offered take part in
    /// the lookup: a model is not supposed to ask for anything else.
    /// </summary>
    private ITool Resolve(string name) => string.IsNullOrEmpty(name)
        ? null
        : _tools.FirstOrDefault(x => x.GetDefinition() is { } definition &&
                                     string.Equals(definition.Name, name.Trim(),
                                         StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Lists the tools the model can actually call, so that a mistaken name
    /// can be corrected on the next attempt.
    /// </summary>
    private string DescribeAvailableTools()
    {
        var names = GetToolDefinitions().Select(x => $"\"{x.Name}\"").ToArray();

        return names.Length == 0
            ? "There are no tools available."
            : $"The available tools are: {string.Join(", ", names)}.";
    }
}

/// <summary>
/// Utility methods to read the arguments a model has supplied with a tool call.
/// </summary>
public static class ToolArguments
{
    /// <summary>
    /// Returns the string value of the property with the given
    /// <paramref name="name"/> or "null" if there is no such property.
    /// </summary>
    public static string ReadString(JsonElement arguments, string name)
    {
        if (!TryGetProperty(arguments, name, out var property))
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
    public static int? ReadInt(JsonElement arguments, string name)
    {
        if (!TryGetProperty(arguments, name, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
            return number;

        if (property.ValueKind == JsonValueKind.String &&
            int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    /// <summary>
    /// Returns "true" if the arguments contain a property with the given
    /// <paramref name="name"/>.
    /// </summary>
    private static bool TryGetProperty(JsonElement arguments, string name, out JsonElement property)
    {
        if (arguments.ValueKind == JsonValueKind.Object)
            return arguments.TryGetProperty(name, out property);

        property = default;

        return false;
    }
}
