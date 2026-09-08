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
using BAnalyzerCore.Ollama;
using FluentAssertions;
using System.Net;
using System.Text;

namespace BAnalyzerCoreTest;

/// <summary>
/// Tests of <see cref="OllamaClient"/>.
/// </summary>
[TestClass]
public class OllamaClientTest
{
    /// <summary>
    /// A message handler that returns a canned response (or throws a canned exception).
    /// </summary>
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(responder(request));
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string payload) =>
        new(code) { Content = new StringContent(payload, Encoding.UTF8, "application/json") };

    /// <summary>
    /// Address the tested clients are pointed at.
    /// </summary>
    private static readonly Uri TestAddress = new("http://ollama.test:11434");

    private static OllamaClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        new(TestAddress, new StubHandler(responder));

    /// <summary>
    /// Creates a client on top of the given handler, so that a test can inspect
    /// the requests that were (or were not) made.
    /// </summary>
    private static OllamaClient CreateClient(StubHandler handler) => new(TestAddress, handler);

    [TestMethod]
    public async Task TryGetModelsAsync_ValidPayload_ReturnsModelNames()
    {
        using var client = CreateClient(r =>
        {
            if (r!.RequestUri!.AbsolutePath.EndsWith("/api/tags"))
                return Json(HttpStatusCode.OK,
                    """{"models":[{"name":"qwen3.8:27b","size":123,"details":{"parameter_size":"27B","quantization_level":"Q4_K_M"}},{"name":"ornith-1.5:35b"}]}""");

            if (r!.RequestUri!.AbsolutePath.EndsWith("/api/show"))
            {
                // Models that do not have "tool" capability will be ignored.
                // // Make sure that the response contains the "tools" capability.
                return Json(HttpStatusCode.OK, """{ "capabilities":["completion", "vision", "tools", "thinking"]}""");
            }

            throw new InvalidOperationException("Unexpected request");
        });

        var result = await client.TryGetModelsAsync(CancellationToken.None);

        result.Available.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Models.Should().Equal(
            new ModelInfo("qwen3.8:27b", 123, "27B", "Q4_K_M"),
            new ModelInfo("ornith-1.5:35b", null, null, null));
    }

    [TestMethod]
    public async Task TryGetModelsAsync_EmptyModelList_IsAvailableWithNoModels()
    {
        using var client = CreateClient(_ => Json(HttpStatusCode.OK, """{"models":[]}"""));

        var result = await client.TryGetModelsAsync(CancellationToken.None);

        result.Available.Should().BeTrue();
        result.Models.Should().BeEmpty();
    }

    [TestMethod]
    public async Task TryGetModelsAsync_ServiceUnreachable_ReportsUnavailable()
    {
        using var client = CreateClient(_ => throw new HttpRequestException("Connection refused"));

        var result = await client.TryGetModelsAsync(CancellationToken.None);

        result.Available.Should().BeFalse();
        result.Models.Should().BeEmpty();
        result.Error.Should().Contain("Connection refused");
    }

    [TestMethod]
    public async Task TryGetModelsAsync_ErrorStatusCode_ReportsUnavailable()
    {
        using var client = CreateClient(_ => Json(HttpStatusCode.InternalServerError, "{}"));

        var result = await client.TryGetModelsAsync(CancellationToken.None);

        result.Available.Should().BeFalse();
        result.Error.Should().Contain("500");
    }

    [TestMethod]
    public async Task TryGetModelsAsync_MalformedPayload_ReportsUnavailable()
    {
        using var client = CreateClient(_ => Json(HttpStatusCode.OK, "not a json"));

        var result = await client.TryGetModelsAsync(CancellationToken.None);

        result.Available.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    [DataRow(null, "http://localhost:11434/")]
    [DataRow("", "http://localhost:11434/")]
    [DataRow("   ", "http://localhost:11434/")]
    [DataRow("Example-PC:11434", "http://example-pc:11434/")]
    [DataRow("example-pc", "http://example-pc:11434/")]
    [DataRow("127.0.0.1:11434", "http://127.0.0.1:11434/")]
    [DataRow("http://example-pc:11434", "http://example-pc:11434/")]
    [DataRow("https://example-pc:443", "https://example-pc/")]
    [DataRow("example-pc:8080", "http://example-pc:8080/")]
    [DataRow(":11434", "http://localhost:11434/")]
    public void ParseHost_ProducesExpectedAddress(string host, string expected)
    {
        OllamaClient.ParseHost(host).ToString().Should().Be(expected);
    }

    /// <summary>
    /// Reads out the given stream of chunks into a list.
    /// </summary>
    private static async Task<List<ChatChunk>> CollectAsync(IAsyncEnumerable<ChatChunk> stream)
    {
        var result = new List<ChatChunk>();

        await foreach (var chunk in stream)
            result.Add(chunk);

        return result;
    }

    [TestMethod]
    public async Task ChatStreamAsync_ReturnsChunksInOrder()
    {
        using var client = CreateClient(_ => Json(HttpStatusCode.OK,
            """
            {"message":{"role":"assistant","content":"Hello"},"done":false}
            {"message":{"role":"assistant","content":" there"},"done":false}
            {"message":{"role":"assistant","content":"!"},"done":true}
            """));

        var chunks = await CollectAsync(client.ChatStreamAsync("some-model",
            [new ChatMessage(ChatRoles.User, "Hi")], null, CancellationToken.None));

        chunks.Select(x => x.Content).Should().Equal("Hello", " there", "!");
    }

    [TestMethod]
    public async Task ChatStreamAsync_SeparatesThinkingFromContent()
    {
        using var client = CreateClient(_ => Json(HttpStatusCode.OK,
            """
            {"message":{"role":"assistant","content":"","thinking":"Let me"},"done":false}
            {"message":{"role":"assistant","content":"","thinking":" check."},"done":false}
            {"message":{"role":"assistant","content":"Four."},"done":true}
            """));

        var chunks = await CollectAsync(client.ChatStreamAsync("some-model",
            [new ChatMessage(ChatRoles.User, "2+2?")], null, CancellationToken.None));

        string.Concat(chunks.Select(x => x.Thinking)).Should().Be("Let me check.");
        string.Concat(chunks.Select(x => x.Content)).Should().Be("Four.");
    }

    [TestMethod]
    public async Task ChatStreamAsync_StopsAtTheDoneMarker()
    {
        using var client = CreateClient(_ => Json(HttpStatusCode.OK,
            """
            {"message":{"role":"assistant","content":"Answer"},"done":true}
            {"message":{"role":"assistant","content":"MUST NOT APPEAR"},"done":false}
            """));

        var chunks = await CollectAsync(client.ChatStreamAsync("some-model",
            [new ChatMessage(ChatRoles.User, "Hi")], null, CancellationToken.None));

        chunks.Select(x => x.Content).Should().Equal("Answer");
    }

    [TestMethod]
    public async Task ChatStreamAsync_SkipsMalformedAndEmptyLines()
    {
        using var client = CreateClient(_ => Json(HttpStatusCode.OK,
            """
            {"message":{"role":"assistant","content":"A"},"done":false}
            this is not json

            {"message":{"role":"assistant","content":"B"},"done":true}
            """));

        var chunks = await CollectAsync(client.ChatStreamAsync("some-model",
            [new ChatMessage(ChatRoles.User, "Hi")], null, CancellationToken.None));

        chunks.Select(x => x.Content).Should().Equal("A", "B");
    }

    [TestMethod]
    public async Task ChatStreamAsync_ErrorStatusCode_Throws()
    {
        using var client = CreateClient(_ => Json(HttpStatusCode.NotFound, "{}"));

        var act = async () => await CollectAsync(client.ChatStreamAsync("missing-model",
            [new ChatMessage(ChatRoles.User, "Hi")], null, CancellationToken.None));

        (await act.Should().ThrowAsync<HttpRequestException>()).WithMessage("*404*");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public async Task ChatStreamAsync_WithoutModel_Throws(string model)
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("Must not be called"));
        using var client = CreateClient(handler);

        var act = async () => await CollectAsync(client.ChatStreamAsync(model,
            [new ChatMessage(ChatRoles.User, "Hi")], null, CancellationToken.None));

        await act.Should().ThrowAsync<ArgumentException>();
        handler.LastRequest.Should().BeNull();
    }

    [TestMethod]
    public async Task ChatStreamAsync_WithoutHistory_Throws()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("Must not be called"));
        using var client = CreateClient(handler);

        var act = async () => await CollectAsync(client.ChatStreamAsync("some-model",
            [], null, CancellationToken.None));

        await act.Should().ThrowAsync<ArgumentException>();
        handler.LastRequest.Should().BeNull();
    }

    [TestMethod]
    public async Task ChatStreamAsync_RequestsStreamingMode()
    {
        var body = (string)null;

        // The body must be read here: the request gets disposed by the time
        // the enumeration is over.
        var handler = new StubHandler(request =>
        {
            body = request.Content!.ReadAsStringAsync().Result;

            return Json(HttpStatusCode.OK,
                """{"message":{"role":"assistant","content":"Hi"},"done":true}""");
        });

        using var client = CreateClient(handler);

        await CollectAsync(client.ChatStreamAsync("some-model",
            [new ChatMessage(ChatRoles.User, "Hi")], null, CancellationToken.None));

        body.Should().Contain("\"stream\":true");
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/api/chat");
    }

    [TestMethod]
    public async Task ChatStreamAsync_DoesNotMixTheReasoningIntoTheAnswer()
    {
        using var client = CreateClient(_ => Json(HttpStatusCode.OK,
            """
            {"message":{"role":"assistant","content":"","thinking":"hmm"},"done":false}
            {"message":{"role":"assistant","content":"Hello"},"done":false}
            {"message":{"role":"assistant","content":" world"},"done":true}
            """));

        var chunks = await CollectAsync(client.ChatStreamAsync("some-model",
            [new ChatMessage(ChatRoles.User, "Hi")], null, CancellationToken.None));

        // The reasoning must not leak into the answer.
        string.Concat(chunks.Select(x => x.Content)).Should().Be("Hello world");
        string.Concat(chunks.Select(x => x.Thinking)).Should().Be("hmm");
    }

    [TestMethod]
    public async Task SupportsToolsAsync_CapabilityDeclared_ReturnsTrue()
    {
        using var client = CreateClient(_ => Json(HttpStatusCode.OK,
            """{"capabilities":["completion","vision","tools","thinking"]}"""));

        (await client.SupportsToolsAsync("some-model", CancellationToken.None)).Should().BeTrue();
    }

    [TestMethod]
    public async Task SupportsToolsAsync_CapabilityMissing_ReturnsFalse()
    {
        using var client = CreateClient(_ => Json(HttpStatusCode.OK,
            """{"capabilities":["completion","thinking"]}"""));

        (await client.SupportsToolsAsync("some-model", CancellationToken.None)).Should().BeFalse();
    }

    [TestMethod]
    public async Task SupportsToolsAsync_NoCapabilitiesField_ReturnsFalse()
    {
        // Builds of the service that predate the capability reporting.
        using var client = CreateClient(_ => Json(HttpStatusCode.OK, """{"details":{"family":"qwen"}}"""));

        (await client.SupportsToolsAsync("some-model", CancellationToken.None)).Should().BeFalse();
    }

    [TestMethod]
    public async Task SupportsToolsAsync_ServiceUnreachable_ReturnsFalse()
    {
        using var client = CreateClient(_ => throw new HttpRequestException("Connection refused"));

        (await client.SupportsToolsAsync("some-model", CancellationToken.None)).Should().BeFalse();
    }

    [TestMethod]
    public async Task SupportsToolsAsync_ErrorStatusCode_ReturnsFalse()
    {
        using var client = CreateClient(_ => Json(HttpStatusCode.NotFound, "{}"));

        (await client.SupportsToolsAsync("some-model", CancellationToken.None)).Should().BeFalse();
    }

    [TestMethod]
    public async Task SupportsToolsAsync_QueriesTheServiceOnlyOnce()
    {
        var requests = 0;

        using var client = CreateClient(_ =>
        {
            requests++;
            return Json(HttpStatusCode.OK, """{"capabilities":["tools"]}""");
        });

        await client.SupportsToolsAsync("some-model", CancellationToken.None);
        await client.SupportsToolsAsync("some-model", CancellationToken.None);

        requests.Should().Be(1);
    }

    /// <summary>
    /// A tool declaration to be offered to a model in the tests.
    /// </summary>
    private static ToolDefinition SomeTool() =>
        new("get_candles", "Returns the candles.",
        [
            new ToolParameter("symbol", "string", "The pair."),
            new ToolParameter("granularity", "string", "Candle duration.", ["1h", "1d"]),
            new ToolParameter("count", "integer", "How many.", Required: false)
        ]);

    [TestMethod]
    public async Task ChatStreamAsync_ReportsToolCalls()
    {
        // The call arrives with the "done" flag not set and with no content
        // whatsoever, and the stream ends the way an ordinary answer ends.
        using var client = CreateClient(_ => Json(HttpStatusCode.OK,
            """
            {"message":{"role":"assistant","content":"","tool_calls":[{"id":"call_1","function":{"name":"get_candles","arguments":{"symbol":"BTCUSDT","granularity":"1d","count":3}}}]},"done":false}
            {"message":{"role":"assistant","content":""},"done":true,"done_reason":"stop"}
            """));

        var chunks = await CollectAsync(client.ChatStreamAsync("some-model",
            [new ChatMessage(ChatRoles.User, "How did BTC do?")], [SomeTool()], CancellationToken.None));

        var calls = chunks.Where(x => x.ToolCalls != null).SelectMany(x => x.ToolCalls).ToArray();

        calls.Should().HaveCount(1);
        calls[0].Id.Should().Be("call_1");
        calls[0].Name.Should().Be("get_candles");
        calls[0].Arguments.GetProperty("symbol").GetString().Should().Be("BTCUSDT");
        calls[0].Arguments.GetProperty("count").GetInt32().Should().Be(3);
    }

    [TestMethod]
    public async Task ChatStreamAsync_ToolCallWithoutContent_IsNotSwallowed()
    {
        using var client = CreateClient(_ => Json(HttpStatusCode.OK,
            """
            {"message":{"role":"assistant","content":"","tool_calls":[{"id":"c","function":{"name":"get_candles","arguments":{}}}]},"done":true}
            """));

        var chunks = await CollectAsync(client.ChatStreamAsync("some-model",
            [new ChatMessage(ChatRoles.User, "Hi")], [SomeTool()], CancellationToken.None));

        chunks.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task ChatStreamAsync_WithoutTools_OmitsTheField()
    {
        var body = (string)null;

        var handler = new StubHandler(request =>
        {
            body = request.Content!.ReadAsStringAsync().Result;

            return Json(HttpStatusCode.OK,
                """{"message":{"role":"assistant","content":"Hi"},"done":true}""");
        });

        using var client = CreateClient(handler);

        await CollectAsync(client.ChatStreamAsync("some-model",
            [new ChatMessage(ChatRoles.User, "Hi")], null, CancellationToken.None));

        body.Should().NotContain("\"tools\"");
    }

    [TestMethod]
    public async Task ChatStreamAsync_DeclaresTheToolsAndTheirAllowedValues()
    {
        var body = (string)null;

        var handler = new StubHandler(request =>
        {
            body = request.Content!.ReadAsStringAsync().Result;

            return Json(HttpStatusCode.OK,
                """{"message":{"role":"assistant","content":"Hi"},"done":true}""");
        });

        using var client = CreateClient(handler);

        await CollectAsync(client.ChatStreamAsync("some-model",
            [new ChatMessage(ChatRoles.User, "Hi")], [SomeTool()], CancellationToken.None));

        body.Should().Contain("\"get_candles\"");
        body.Should().Contain("\"enum\":[\"1h\",\"1d\"]");

        // The optional parameter must not be listed as a required one.
        body.Should().Contain("\"required\":[\"symbol\",\"granularity\"]");
    }

    [TestMethod]
    public async Task ChatStreamAsync_SendsBackTheToolResults()
    {
        var body = (string)null;

        var handler = new StubHandler(request =>
        {
            body = request.Content!.ReadAsStringAsync().Result;

            return Json(HttpStatusCode.OK,
                """{"message":{"role":"assistant","content":"Hi"},"done":true}""");
        });

        using var client = CreateClient(handler);

        var arguments = System.Text.Json.JsonDocument.Parse("""{"symbol":"BTCUSDT"}""").RootElement;

        await CollectAsync(client.ChatStreamAsync("some-model",
        [
            new ChatMessage(ChatRoles.User, "How did BTC do?"),
            new ChatMessage(ChatRoles.Assistant, "", [new ToolCall("call_1", "get_candles", arguments)]),
            new ChatMessage(ChatRoles.Tool, "BTCUSDT: 3 candles", ToolName: "get_candles")
        ], [SomeTool()], CancellationToken.None));

        body.Should().Contain("\"tool_calls\"");
        body.Should().Contain("\"call_1\"");
        body.Should().Contain("\"role\":\"tool\"");
        body.Should().Contain("BTCUSDT: 3 candles");
    }
}
