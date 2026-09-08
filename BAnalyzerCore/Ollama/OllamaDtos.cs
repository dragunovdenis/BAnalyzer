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
using System.Text.Json.Serialization;

namespace BAnalyzerCore.Ollama;

/// <summary>
/// Response of the Ollama "/api/tags" end-point.
/// </summary>
internal sealed class OllamaTagsResponse
{
    /// <summary>
    /// Collection of the locally available models.
    /// </summary>
    [JsonPropertyName("models")]
    public List<OllamaModelInfo> Models { get; set; }
}

/// <summary>
/// Request body of the Ollama "/api/show" end-point.
/// </summary>
internal sealed class OllamaShowRequest
{
    /// <summary>
    /// Name of the model to describe.
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; }
}

/// <summary>
/// Response of the Ollama "/api/show" end-point.
/// </summary>
internal sealed class OllamaShowResponse
{
    /// <summary>
    /// Capabilities declared by the model, for example "completion", "tools",
    /// "thinking" or "vision". Older builds of the service do not report the
    /// field at all, in which case the collection is "null".
    /// </summary>
    [JsonPropertyName("capabilities")]
    public List<string> Capabilities { get; set; }

    /// <summary>
    /// Low-level, family-specific model parameters, for example
    /// "llama.context_length". The keys are prefixed with the model family
    /// and hence can't be known upfront, which is why the values are kept
    /// as raw JSON elements rather than being mapped onto dedicated properties.
    /// </summary>
    [JsonPropertyName("model_info")]
    public Dictionary<string, JsonElement> ModelInfo { get; set; }
}

/// <summary>
/// Description of a single locally available model.
/// </summary>
internal sealed class OllamaModelInfo
{
    /// <summary>
    /// Name of the model (the identifier accepted by the "/api/chat" end-point).
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Size of the model on disk, in bytes.
    /// </summary>
    [JsonPropertyName("size")]
    public long? Size { get; set; }

    /// <summary>
    /// Family-specific details of the model, such as its parameter count
    /// and quantization level.
    /// </summary>
    [JsonPropertyName("details")]
    public OllamaModelDetails Details { get; set; }
}

/// <summary>
/// Family-specific details of a locally available model, as reported by the
/// "/api/tags" end-point.
/// </summary>
internal sealed class OllamaModelDetails
{
    /// <summary>
    /// Family the model belongs to, for example "llama" or "qwen3".
    /// </summary>
    [JsonPropertyName("family")]
    public string Family { get; set; }

    /// <summary>
    /// Human-readable size of the model, for example "8B" or "27B".
    /// </summary>
    [JsonPropertyName("parameter_size")]
    public string ParameterSize { get; set; }

    /// <summary>
    /// Quantization level of the model, for example "Q4_K_M".
    /// </summary>
    [JsonPropertyName("quantization_level")]
    public string QuantizationLevel { get; set; }
}

/// <summary>
/// Description of a function the model has decided to call.
/// </summary>
internal sealed class OllamaToolCallFunctionDto
{
    /// <summary>
    /// Name of the function to call.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Arguments of the call. The service reports them as a JSON <b>object</b>
    /// (not as a string containing JSON), hence there is no second parsing step.
    /// </summary>
    [JsonPropertyName("arguments")]
    public JsonElement Arguments { get; set; }
}

/// <summary>
/// A single call of a tool requested by the model.
/// </summary>
internal sealed class OllamaToolCallDto
{
    /// <summary>
    /// Identifier of the call, to be echoed back with the result.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// The function to call together with its arguments.
    /// </summary>
    [JsonPropertyName("function")]
    public OllamaToolCallFunctionDto Function { get; set; }
}

/// <summary>
/// A single message as it is represented in the Ollama chat protocol.
/// </summary>
internal sealed class OllamaChatMessageDto
{
    /// <summary>
    /// Author of the message ("system", "user", "assistant" or "tool").
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; }

    /// <summary>
    /// Text of the message.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; }

    /// <summary>
    /// The "reasoning" the model produces before the actual answer. Reported by
    /// the models that support it (and only by them), separately from
    /// <see cref="Content"/>, which is why it does not have to be parsed out of
    /// the answer text.
    /// </summary>
    [JsonPropertyName("thinking")]
    public string Thinking { get; set; }

    /// <summary>
    /// Tools the model has decided to call. Present only for the models that
    /// support tool calling and only when they choose to use one.
    /// </summary>
    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<OllamaToolCallDto> ToolCalls { get; set; }

    /// <summary>
    /// Name of the tool that produced the message. Applicable to the
    /// messages of the "tool" role only.
    /// </summary>
    [JsonPropertyName("tool_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string ToolName { get; set; }
}

/// <summary>
/// Declaration of the parameters a tool accepts (a JSON-schema object).
/// </summary>
internal sealed class OllamaToolParametersDto
{
    /// <summary>
    /// Type of the parameters container, always "object".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    /// <summary>
    /// The accepted parameters keyed by their names.
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, OllamaToolPropertyDto> Properties { get; set; }

    /// <summary>
    /// Names of the parameters that must be supplied.
    /// </summary>
    [JsonPropertyName("required")]
    public List<string> Required { get; set; }
}

/// <summary>
/// Declaration of a single parameter of a tool.
/// </summary>
internal sealed class OllamaToolPropertyDto
{
    /// <summary>
    /// JSON-schema type of the parameter ("string", "integer" etc.).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }

    /// <summary>
    /// Human-readable description of the parameter, used by the model to
    /// decide how to fill it in.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; }

    /// <summary>
    /// The values the parameter is allowed to take. When it is set, the model
    /// can't produce anything outside the collection, which is the
    /// cheapest way to keep the requests valid by construction.
    /// </summary>
    [JsonPropertyName("enum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string> Enum { get; set; }
}

/// <summary>
/// Declaration of a function that the model is allowed to call.
/// </summary>
internal sealed class OllamaToolFunctionDto
{
    /// <summary>
    /// Name of the function.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Description of what the function does, used by the model to decide
    /// whether the function is relevant to the request at hand.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; }

    /// <summary>
    /// Parameters the function accepts.
    /// </summary>
    [JsonPropertyName("parameters")]
    public OllamaToolParametersDto Parameters { get; set; }
}

/// <summary>
/// Declaration of a single tool offered to the model.
/// </summary>
internal sealed class OllamaToolDto
{
    /// <summary>
    /// Type of the tool, always "function".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    /// <summary>
    /// The function the tool exposes.
    /// </summary>
    [JsonPropertyName("function")]
    public OllamaToolFunctionDto Function { get; set; }
}

/// <summary>
/// Request body of the Ollama "/api/chat" end-point.
/// </summary>
internal sealed class OllamaChatRequest
{
    /// <summary>
    /// Name of the model to run the conversation with.
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; }

    /// <summary>
    /// The conversation so far.
    /// </summary>
    [JsonPropertyName("messages")]
    public List<OllamaChatMessageDto> Messages { get; set; }

    /// <summary>
    /// Set to "false" to receive the entire answer as a single response.
    /// </summary>
    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    /// <summary>
    /// Tools the model is allowed to call. Must be left "null" for the models
    /// that do not declare the corresponding capability.
    /// </summary>
    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<OllamaToolDto> Tools { get; set; }
}

/// <summary>
/// Response of the Ollama "/api/chat" end-point (non-streaming mode).
/// </summary>
internal sealed class OllamaChatResponse
{
    /// <summary>
    /// The message produced by the model.
    /// </summary>
    [JsonPropertyName("message")]
    public OllamaChatMessageDto Message { get; set; }

    /// <summary>
    /// Indicates that the generation is finished.
    /// </summary>
    [JsonPropertyName("done")]
    public bool Done { get; set; }
}
