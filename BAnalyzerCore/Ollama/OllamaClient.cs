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

using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace BAnalyzerCore.Ollama;

/// <summary>
/// Implementation of the corresponding interface on top of the Ollama REST API.
/// </summary>
public sealed class OllamaClient : IOllamaClient
{
    /// <summary>
    /// Address the Ollama service listens at by default.
    /// </summary>
    public const string DefaultBaseAddress = "http://localhost:11434";

    /// <summary>
    /// Name of the environment variable that (optionally) redefines the address
    /// the Ollama service listens at. It is respected by the "ollama" command
    /// line tool, so honoring it here is essential to stay in sync with it.
    /// </summary>
    private const string HostEnvironmentVariable = "OLLAMA_HOST";

    /// <summary>
    /// Port the Ollama service listens at by default.
    /// </summary>
    private const int DefaultPort = 11434;

    /// <summary>
    /// Returns the address of the Ollama service, taking the
    /// <see cref="HostEnvironmentVariable"/> into account.
    /// </summary>
    private static Uri ResolveBaseAddress()
    {
        var networkHost = Environment.GetEnvironmentVariable(HostEnvironmentVariable);
        return ParseHost(networkHost);
    }

    /// <summary>
    /// Converts the given value of the "host" environment variable into an address.
    /// Falls back to <see cref="DefaultBaseAddress"/> if the value can't be parsed.
    /// </summary>
    /// <remarks>
    /// The variable can be given in a number of formats, for example
    /// "example-pc", "example-pc:11434", ":11434" or "http://example-pc:11434".
    /// </remarks>
    internal static Uri ParseHost(string host)
    {
        var fallback = new Uri(DefaultBaseAddress);

        if (string.IsNullOrEmpty(host))
            return fallback;

        host = host.Trim();

        // Supply the scheme, if it is missing, to make the string parsable as an address.
        if (!host.Contains("://", StringComparison.Ordinal))
            host = "http://" + host.TrimStart('/');

        if (!Uri.TryCreate(host, UriKind.Absolute, out var result))
            return fallback;

        var builder = new UriBuilder(result);

        // An omitted host (as in ":11434") ends up being an empty string.
        if (string.IsNullOrEmpty(builder.Host))
            builder.Host = fallback.Host;

        // "0.0.0.0" (or its IPv6 equivalent "::") is a "listen on all interfaces"
        // wildcard address. It is what the "ollama" server itself falls back to
        // (and thus what "OLLAMA_HOST" may end up containing), but it is not a
        // valid address to connect to, so it needs to be substituted with the
        // loopback host used to actually reach the service.
        if (IsWildcardHost(builder.Host))
            builder.Host = fallback.Host;

        // "UriBuilder" substitutes the default port of the scheme (80 for "http")
        // when the port is not given explicitly, which is not what we want here.
        if (result.IsDefaultPort && !HasExplicitPort(host))
            builder.Port = DefaultPort;

        return builder.Uri;
    }

    /// <summary>
    /// Returns "true" if the given host string represents a "listen on all
    /// interfaces" wildcard address (such as "0.0.0.0" or "::"), which is not
    /// a valid address to connect to.
    /// </summary>
    private static bool IsWildcardHost(string host)
    {
        return host is "0.0.0.0" or "::" or "[::]";
    }

    /// <summary>
    /// Returns "true" if the given address string contains an explicitly defined port.
    /// </summary>
    /// <remarks>
    /// The question is asked of the string rather than of the parsed address,
    /// because the parser silently substitutes the default port of the scheme
    /// and thus erases the very distinction we are after.
    /// </remarks>
    private static bool HasExplicitPort(string address)
    {
        // Everything between the scheme and the path is the authority, and the
        // port, if given, is what follows its last colon. The host of an IPv6
        // address contains colons of its own, but they are enclosed in brackets.
        var authority = address[(address.IndexOf("://", StringComparison.Ordinal) + 3)..];
        var pathStart = authority.IndexOf('/');

        if (pathStart >= 0) authority = authority[..pathStart];

        return authority.LastIndexOf(':') > authority.LastIndexOf(']');
    }

    /// <summary>
    /// Time budget of a "list models" request.
    /// </summary>
    public static readonly TimeSpan ModelsTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Time budget of a "describe model" request. The end-point reports the
    /// metadata only, so it neither loads the model nor generates anything.
    /// </summary>
    public static readonly TimeSpan ShowTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Cached capabilities of the models that were asked about so far. The capabilities
    /// of a model can't change while the service is running, so asking once is enough.
    /// </summary>
    private readonly ConcurrentDictionary<string, ModelCaps?> _modelCaps = new();   

    /// <summary>
    /// Suffix of the "model_info" key (see <see cref="OllamaShowResponse.ModelInfo"/>)
    /// that carries the context window of a model. The key is prefixed with
    /// the model family, which is why a suffix match is used instead.
    /// </summary>
    private const string ContextLengthKeySuffix = ".context_length";

    /// <summary>
    /// Time budget of a single conversation turn. Generous, because a "cold"
    /// model of a considerable size can take minutes to load.
    /// </summary>
    public static readonly TimeSpan ChatTimeout = TimeSpan.FromMinutes(2);

    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    /// <summary>
    /// Constructor.
    /// </summary>
    public OllamaClient() : this(new HttpClient(), true) { }

    /// <summary>
    /// Constructor allowing to substitute the message handler and to pin the
    /// address explicitly (used in tests).
    /// </summary>
    /// <remarks>
    /// The address is a mandatory parameter here so that a test can never end
    /// up depending on the <see cref="HostEnvironmentVariable"/> variable of
    /// the machine it happens to run on.
    /// </remarks>
    internal OllamaClient(Uri baseAddress, HttpMessageHandler handler)
        : this(new HttpClient(handler) { BaseAddress = baseAddress }, true) { }

    private OllamaClient(HttpClient client, bool ownsClient)
    {
        _client = client;
        _ownsClient = ownsClient;

        _client.BaseAddress ??= ResolveBaseAddress();

        // The timeout is controlled per-request via a cancellation token source
        // (HttpClient.Timeout is a per-instance setting and hence too coarse here).
        _client.Timeout = Timeout.InfiniteTimeSpan;
    }

    /// <inheritdoc/>
    public async Task<ModelsResult> TryGetModelsAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(ModelsTimeout);

        try
        {
            using var response = await _client.GetAsync("/api/tags", cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return new ModelsResult(false, [], DescribeStatus(response));

            var payload = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(cts.Token).ConfigureAwait(false);

            if (payload?.Models == null)
                throw new InvalidOperationException("Failed to get models.");

            var namedModels = payload.Models
                .Where(x => !string.IsNullOrWhiteSpace(x?.Name))
                .ToArray();

            // The capabilities are fetched in parallel and cached, so that the
            // later questions about the same models cost no extra requests.
            var caps = await Task.WhenAll(namedModels.Select(m => GetModelCapsAsync(m.Name, cts.Token)))
                .ConfigureAwait(false);

            var modelsInfos = namedModels.Select((x, i) => new ModelInfo(x.Name, x.Size,
                    x.Details?.ParameterSize, x.Details?.QuantizationLevel,
                    caps[i]?.ContextWindow ?? -1, caps[i]))
                .ToList();

            return new ModelsResult(true, modelsInfos, null);
        }
        catch (Exception e)
        {
            return new ModelsResult(false, [], DescribeFailure(e, ct,
                $"Could not reach the Ollama service at {_client.BaseAddress}"));
        }
    }

    /// <summary>
    /// Returns "true" if the model with the given name declares the "tools" capability.
    /// </summary>
    public async Task<bool> SupportsToolsAsync(string model, CancellationToken ct)
    {
        var caps = await GetModelCapsAsync(model, ct).ConfigureAwait(false);
        return caps?.Tools ?? false;
    }

    /// <summary>
    /// Returns "true" if the response declares the given capability.
    /// </summary>
    private bool CheckCapability(OllamaShowResponse response, string capabilityId)
    {
        return response?.Capabilities?
            .Any(x => string.Equals(x, capabilityId, StringComparison.OrdinalIgnoreCase)) ?? false;
    }

    /// <inheritdoc/>
    public async Task<ModelCaps?> GetModelCapsAsync(string model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model))
            return null;

        if (_modelCaps.TryGetValue(model, out var known))
            return known;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(ShowTimeout);

        ModelCaps? caps;

        try
        {
            using var response = await _client.PostAsJsonAsync("/api/show",
                new OllamaShowRequest { Model = model }, cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content
                .ReadFromJsonAsync<OllamaShowResponse>(cts.Token).ConfigureAwait(false);

            const string toolsCapability = "tools";
            var tools = CheckCapability(payload, toolsCapability);

            const string thinkingCapability = "thinking";
            var thinking = CheckCapability(payload, thinkingCapability);

            const string completionCapability = "completion";
            var completion = CheckCapability(payload, completionCapability);

            const string visionCapability = "vision";
            var vision = CheckCapability(payload, visionCapability);    

            const string audioCapability = "audio";
            var audio = CheckCapability(payload, audioCapability);

            // The key is prefixed with the model family (e.g. "llama.context_length"),
            // so it can't be looked up directly and a suffix match is used instead.
            var entry = payload?.ModelInfo?
                .FirstOrDefault(x => x.Key.EndsWith(ContextLengthKeySuffix, StringComparison.Ordinal));

            var contextWindow = entry?.Value.ValueKind == JsonValueKind.Number &&
                                entry.Value.Value.TryGetInt32(out var value) ? value : -1;

            caps = new ModelCaps(contextWindow, thinking, completion, vision, tools, audio);

        }
        catch (Exception)
        {
            // Whatever went wrong, the caller can still supply the context
            // itself, so a failure to determine the capability is not worth
            // reporting - and must not be cached either.
            return null;
        }

        return _modelCaps[model] = caps;
    }

    /// <summary>
    /// Builds a request body for the "/api/chat" end-point.
    /// </summary>
    private static OllamaChatRequest BuildRequest(string model, IReadOnlyList<ChatMessage> history,
        IReadOnlyList<ToolDefinition> tools, bool stream, ChatRequestOptions options) => new()
    {
        Model = model,
        Stream = stream,
        Tools = BuildTools(tools),
        Think = ToWireValue(options?.Thinking),
        Messages = history.Select(x => new OllamaChatMessageDto
        {
            Role = x.Role,
            Content = x.Content,
            ToolName = x.ToolName,
            ToolCalls = x.ToolCalls?.Select(c => new OllamaToolCallDto
            {
                Id = c.Id,
                Function = new OllamaToolCallFunctionDto { Name = c.Name, Arguments = c.Arguments }
            }).ToList()
        }).ToList()
    };

    /// <summary>
    /// Converts the given tool declarations to their wire representation.
    /// Returns "null" if there are no tools to declare, so that the field is
    /// omitted from the request altogether.
    /// </summary>
    private static List<OllamaToolDto> BuildTools(IReadOnlyList<ToolDefinition> tools)
    {
        if (tools == null || tools.Count == 0)
            return null;

        return tools.Select(tool => new OllamaToolDto
        {
            Function = new OllamaToolFunctionDto
            {
                Name = tool.Name,
                Description = tool.Description,
                Parameters = new OllamaToolParametersDto
                {
                    Properties = tool.Parameters?.ToDictionary(p => p.Name, p => new OllamaToolPropertyDto
                    {
                        Type = p.Type,
                        Description = p.Description,
                        Enum = p.AllowedValues?.ToList()
                    }) ?? new Dictionary<string, OllamaToolPropertyDto>(),
                    Required = tool.Parameters?.Where(p => p.Required).Select(p => p.Name).ToList() ?? []
                }
            }
        }).ToList();
    }

    /// <summary>
    /// Converts the given thinking level to the value of the "think" field of a
    /// chat request ("null" means that the field is omitted).
    /// </summary>
    internal static object ToWireValue(ThinkingLevel? level) => level switch
    {
        ThinkingLevel.Low => "low",
        ThinkingLevel.Medium => "medium",
        ThinkingLevel.High => "high",
        _ => null
    };

    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatChunk> ChatStreamAsync(string model, IReadOnlyList<ChatMessage> history,
        IReadOnlyList<ToolDefinition> tools, ChatRequestOptions options,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("No model selected.", nameof(model));

        if (history == null || history.Count == 0)
            throw new ArgumentException("Nothing to send.", nameof(history));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // In the streaming mode the budget is applied to the *pauses* between the
        // portions of the answer rather than to the answer as a whole: a lengthy
        // answer is a normal thing, whereas a lengthy silence means a stall.
        cts.CancelAfter(ChatTimeout);

        var body = BuildRequest(model, history, tools, stream: true, options);
        using var response = await PostChatAsync(body, cts.Token).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadErrorAsync(response, cts.Token).ConfigureAwait(false);
            throw new HttpRequestException(DescribeStatus(response, error));
        }

        await foreach (var chunk in ReadOutResponse(response, ct))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Posts the given chat request and returns the response as soon as its
    /// headers have been received. Some thinking-capable model families only
    /// understand a boolean "think", so if the service rejects a thinking
    /// <i>level</i>, the request is repeated with thinking simply switched on.
    /// </summary>
    private async Task<HttpResponseMessage> PostChatAsync(OllamaChatRequest body, CancellationToken ct)
    {
        var response = await SendChatAsync(body, ct).ConfigureAwait(false);

        if (response.IsSuccessStatusCode || body.Think is not string)
            return response;

        try
        {
            var error = await ReadErrorAsync(response, ct).ConfigureAwait(false);

            if (error == null || !error.Contains("think", StringComparison.OrdinalIgnoreCase))
                return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }

        response.Dispose();
        body.Think = true;

        return await SendChatAsync(body, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Posts the given request to the "/api/chat" end-point and returns the
    /// response as soon as its headers have been received (without that the
    /// entire body gets buffered, which defeats the purpose of streaming).
    /// </summary>
    private async Task<HttpResponseMessage> SendChatAsync(OllamaChatRequest body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(body)
        };

        return await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the error message contained in the body of the given (failed)
    /// response, or "null" if there is none.
    /// </summary>
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(text))
                return null;

            try
            {
                // Native end-points report {"error":"..."}, the OpenAI-compatible
                // ones {"error":{"message":"..."}}.
                using var json = JsonDocument.Parse(text);

                if (json.RootElement.ValueKind == JsonValueKind.Object &&
                    json.RootElement.TryGetProperty("error", out var error))
                {
                    if (error.ValueKind == JsonValueKind.String)
                        return error.GetString();

                    if (error.ValueKind == JsonValueKind.Object &&
                        error.TryGetProperty("message", out var message) &&
                        message.ValueKind == JsonValueKind.String)
                        return message.GetString();
                }

                return text;
            }
            catch (JsonException)
            {
                return text;
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<RunningModelsResult> GetRunningModelsAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(ModelsTimeout);

        try
        {
            using var response = await _client.GetAsync("/api/ps", cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return new RunningModelsResult(false, [], DescribeStatus(response));

            var payload = await response.Content.ReadFromJsonAsync<OllamaPsResponse>(cts.Token)
                .ConfigureAwait(false);

            var models = (payload?.Models ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x?.Name))
                .Select(x => new RunningModel(x.Name, x.Size, x.SizeVram, x.ExpiresAt, x.ContextLength))
                .ToList();

            return new RunningModelsResult(true, models, null);
        }
        catch (Exception e)
        {
            return new RunningModelsResult(false, [], DescribeFailure(e, ct,
                $"Could not reach the Ollama service at {_client.BaseAddress}"));
        }
    }

    /// <summary>
    /// Time budget of a transcription request. A recording is at most a minute
    /// long, while the audio models are small, so the budget covers loading
    /// a "cold" model as well.
    /// </summary>
    public static readonly TimeSpan TranscriptionTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Transcription end-point of the service (OpenAI-compatible). Unlike a chat
    /// request, it prompts each audio model the way its publisher recommends;
    /// prompted through a chat, the small audio models tend to answer or reason
    /// about the recording instead of transcribing it.
    /// </summary>
    private const string TranscriptionEndPoint = "/v1/audio/transcriptions";

    /// <inheritdoc/>
    public async Task<TranscriptionResult> TranscribeAsync(string model, byte[] wavAudio, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model))
            return new TranscriptionResult(false, string.Empty, "No voice model selected.");

        if (wavAudio == null || wavAudio.Length == 0)
            return new TranscriptionResult(false, string.Empty, "Nothing to transcribe.");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TranscriptionTimeout);

        try
        {
            var audio = new ByteArrayContent(wavAudio);
            audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

            // Disposes the parts too.
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(model), "model");
            form.Add(new StringContent("0"), "temperature");
            form.Add(audio, "file", "recording.wav");

            using var response = await _client.PostAsync(TranscriptionEndPoint, form, cts.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorAsync(response, cts.Token).ConfigureAwait(false);
                return new TranscriptionResult(false, string.Empty, DescribeStatus(response, error));
            }

            var payload = await response.Content.ReadFromJsonAsync<OllamaTranscriptionResponse>(cts.Token)
                .ConfigureAwait(false);

            if (payload?.Text == null)
                throw new JsonException("No text in the response.");

            return new TranscriptionResult(true, payload.Text.Trim(), null);
        }
        catch (Exception e)
        {
            var cancelled = e is OperationCanceledException && ct.IsCancellationRequested;
            return new TranscriptionResult(false, string.Empty,
                DescribeFailure(e, ct, "Failed to transcribe the recording"), cancelled);
        }
    }

    /// <summary>
    /// Reads out the response of a streaming chat request and yields the portions of the answer as they arrive.
    /// </summary>
    /// <param name="response">The HTTP response message from the streaming chat request.</param>
    /// <param name="ct">The cancellation token used to cancel the operation.</param>
    private async IAsyncEnumerable<ChatChunk> ReadOutResponse(HttpResponseMessage response, [EnumeratorCancellation] CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (true)
        {
            var line = await reader.ReadLineAsync(cts.Token).ConfigureAwait(false);

            if (line == null) yield break;

            // The service sends one JSON object per line ("NDJSON").
            if (string.IsNullOrWhiteSpace(line)) continue;

            OllamaChatResponse payload;

            try
            {
                payload = JsonSerializer.Deserialize<OllamaChatResponse>(line);
            }
            catch (JsonException)
            {
                // A single unreadable line is not worth failing the whole answer for.
                continue;
            }

            if (payload == null) continue;

            var content = payload.Message?.Content;
            var thinking = payload.Message?.Thinking;
            var calls = ConvertToolCalls(payload.Message?.ToolCalls);

            if (!string.IsNullOrEmpty(content) || !string.IsNullOrEmpty(thinking) || calls != null)
                yield return new ChatChunk(content, thinking, calls);

            if (payload.Done) yield break;

            // The model is alive, so the "silence" budget starts anew.
            cts.CancelAfter(ChatTimeout);
        }
    }

    /// <summary>
    /// Converts the given tool calls to their public representation. Returns
    /// "null" if there is nothing to convert.
    /// </summary>
    private static IReadOnlyList<ToolCall> ConvertToolCalls(List<OllamaToolCallDto> calls)
    {
        var converted = calls?
            .Where(x => !string.IsNullOrWhiteSpace(x?.Function?.Name))
            .Select(x => new ToolCall(x.Id, x.Function.Name, x.Function.Arguments))
            .ToArray();

        return converted is { Length: > 0 } ? converted : null;
    }

    /// <summary>
    /// Describes the status of the given (failed) response, optionally
    /// followed by the error message the service has reported.
    /// </summary>
    private static string DescribeStatus(HttpResponseMessage response, string error = null)
    {
        var message = $"Service responded with {(int)response.StatusCode} ({response.ReasonPhrase})";
        return string.IsNullOrWhiteSpace(error) ? message + "." : $"{message}: {error}";
    }

    /// <summary>
    /// Converts the given exception into a message suitable for the user.
    /// </summary>
    private static string DescribeFailure(Exception e, CancellationToken ct, string fallbackPrefix)
    {
        if (e is OperationCanceledException)
            return ct.IsCancellationRequested ? "The request was canceled." : "The request timed out.";

        if (e is JsonException)
            return "The service returned an unexpected response.";

        return $"{fallbackPrefix}: {e.Message}";
    }

    /// <summary>
    /// Disposes the instance.
    /// </summary>
    public void Dispose()
    {
        if (_ownsClient)
            _client.Dispose();
    }
}
