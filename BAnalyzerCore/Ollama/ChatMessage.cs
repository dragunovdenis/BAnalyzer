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

namespace BAnalyzerCore.Ollama;

/// <summary>
/// Roles recognized by the Ollama chat API.
/// </summary>
public static class ChatRoles
{
    /// <summary>
    /// Role of a message that sets up the context for the model.
    /// </summary>
    public const string System = "system";

    /// <summary>
    /// Role of a message authored by the user.
    /// </summary>
    public const string User = "user";

    /// <summary>
    /// Role of a message authored by the model.
    /// </summary>
    public const string Assistant = "assistant";

    /// <summary>
    /// Role of a message carrying the result of a tool call back to the model.
    /// </summary>
    public const string Tool = "tool";
}

/// <summary>
/// A call of a tool requested by a model.
/// </summary>
/// <param name="Id">Identifier of the call, to be echoed back with the result.</param>
/// <param name="Name">Name of the tool to call.</param>
/// <param name="Arguments">Arguments of the call as a JSON object.</param>
public sealed record ToolCall(string Id, string Name, JsonElement Arguments);

/// <summary>
/// A single message of a conversation with a language model.
/// </summary>
/// <param name="Role">Author of the message, see <see cref="ChatRoles"/>.</param>
/// <param name="Content">Text of the message.</param>
/// <param name="ToolCalls">
/// Tools the model has decided to call. Set for the "assistant" messages that
/// request a tool call and "null" otherwise. Such a message must be put back
/// into the conversation before the corresponding <see cref="ChatRoles.Tool"/>
/// messages, otherwise the model can't match the results to its requests.
/// </param>
/// <param name="ToolName">
/// Name of the tool that produced the message. Set for the
/// <see cref="ChatRoles.Tool"/> messages only.
/// </param>
public record ChatMessage(string Role, string Content,
    IReadOnlyList<ToolCall> ToolCalls = null, string ToolName = null);
