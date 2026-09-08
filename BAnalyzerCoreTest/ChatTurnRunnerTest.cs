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

using System.Runtime.CompilerServices;
using System.Text.Json;
using BAnalyzerCore.Ollama;
using FluentAssertions;

namespace BAnalyzerCoreTest;

/// <summary>
/// Tests of <see cref="ChatTurnRunner"/>.
/// </summary>
/// <remarks>
/// The runner is exercised against the fakes below, so that the tests need
/// neither an Ollama service nor an exchange to run.
/// </remarks>
[TestClass]
public class ChatTurnRunnerTest
{
    /// <summary>
    /// A single response of a model: what it says and what it asks for.
    /// </summary>
    private sealed record Response(string Content, IReadOnlyList<ToolCall> Calls = null);

    /// <summary>
    /// A client that replays the pre-defined responses, one per call, and
    /// records the conversation it was given every time.
    /// </summary>
    private sealed class StubClient(params Response[] responses) : IOllamaClient
    {
        /// <summary>
        /// Snapshots of the history as of every streaming call, in order.
        /// </summary>
        public List<List<ChatMessage>> Conversations { get; } = [];

        /// <summary>
        /// The tool sets the client was called with, in order. A "null" entry
        /// means the tools were withheld.
        /// </summary>
        public List<IReadOnlyList<ToolDefinition>> OfferedTools { get; } = [];

        /// <summary>
        /// An exception to throw instead of streaming the response of the given index.
        /// </summary>
        public (int Index, Exception Error)? Failure { get; init; }

        public Task<ModelsResult> TryGetModelsAsync(CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ModelCaps?> GetModelCapsAsync(string model, CancellationToken ct) => 
            throw new NotSupportedException();

        public Task<bool> SupportsToolsAsync(string model, CancellationToken ct) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<ChatChunk> ChatStreamAsync(string model,
            IReadOnlyList<ChatMessage> history, IReadOnlyList<ToolDefinition> tools,
            [EnumeratorCancellation] CancellationToken ct)
        {
            var index = Conversations.Count;

            Conversations.Add(history.ToList());
            OfferedTools.Add(tools);

            if (Failure?.Index == index) throw Failure.Value.Error;

            // Running out of the scripted responses means the runner has looped
            // more times than the test expects, which must not pass silently.
            if (index >= responses.Length)
                throw new InvalidOperationException($"Unexpected request #{index}.");

            var response = responses[index];

            // The content is delivered in pieces to imitate the actual streaming.
            foreach (var piece in response.Content ?? "")
                yield return new ChatChunk(piece.ToString(), null, null);

            if (response.Calls != null)
                yield return new ChatChunk(null, null, response.Calls);

            await Task.CompletedTask;
        }

        public void Dispose() { }
    }

    /// <summary>
    /// An executor that serves every request with a fixed answer.
    /// </summary>
    private sealed class StubExecutor(bool success = true) : IToolExecutor
    {
        /// <summary>
        /// The calls that were actually executed, in order.
        /// </summary>
        public List<ToolCall> Executed { get; } = [];

        public ToolDefinition GetToolDefinition() => SomeTool();

        public Task<ToolCallResult> ExecuteAsync(ToolCall call, CancellationToken ct)
        {
            Executed.Add(call);

            return Task.FromResult(new ToolCallResult($"Data for {call.Name}",
                $"Summary of {call.Name}", success));
        }
    }

    /// <summary>
    /// An observer that merely records what it is told.
    /// </summary>
    private sealed class RecordingObserver : IChatTurnObserver
    {
        public string Content { get; private set; } = "";
        public string Thinking { get; private set; } = "";
        public int ThinkingBreaks { get; private set; }
        public int ToolCallsStarted { get; private set; }
        public List<(string Summary, bool Success)> ToolCallsCompleted { get; } = [];

        public void OnThinking(string text) => Thinking += text;
        public void OnContent(string text) => Content += text;
        public void OnThinkingBreak() => ThinkingBreaks++;
        public void OnToolCallStarted() => ToolCallsStarted++;

        public void OnToolCallCompleted(string summary, bool success) =>
            ToolCallsCompleted.Add((summary, success));
    }

    private static ToolDefinition SomeTool() =>
        new("get_candles", "Returns candles.",
            [new ToolParameter("symbol", "string", "Trading pair.")]);

    private static ToolCall SomeCall(string name = "get_candles") =>
        new("call-1", name, JsonDocument.Parse("""{"symbol":"BTCUSDT"}""").RootElement);

    private static List<ChatMessage> Conversation() =>
        [new(ChatRoles.User, "Hi")];

    /// <summary>
    /// Runs a turn with the given client and returns everything it produced.
    /// </summary>
    private static async Task<(ChatTurnResult Result, List<ChatMessage> History, RecordingObserver Observer)>
        RunAsync(StubClient client, IToolExecutor executor = null, IReadOnlyList<ToolDefinition> tools = null)
    {
        var history = Conversation();
        var observer = new RecordingObserver();

        var runner = new ChatTurnRunner(client, executor ?? new StubExecutor());

        var result = await runner.RunAsync("some-model", history,
            tools ?? [SomeTool()], observer, CancellationToken.None);

        return (result, history, observer);
    }

    [TestMethod]
    public async Task RunAsync_WithoutToolCalls_ReturnsTheAnswer()
    {
        var client = new StubClient(new Response("Hello there"));

        var (result, history, observer) = await RunAsync(client);

        result.Success.Should().BeTrue();
        result.Answer.Should().Be("Hello there");
        result.Error.Should().BeNull();
        observer.Content.Should().Be("Hello there");

        // A single pass is enough when the model does not ask for anything.
        client.Conversations.Should().HaveCount(1);

        history.Should().HaveCount(2);
        history[^1].Role.Should().Be(ChatRoles.Assistant);
        history[^1].Content.Should().Be("Hello there");
    }

    [TestMethod]
    public async Task RunAsync_ToolCall_IsServedAndTheAnswerIsResumed()
    {
        var client = new StubClient(
            new Response("", [SomeCall()]),
            new Response("Bitcoin is up."));

        var executor = new StubExecutor();

        var (result, history, observer) = await RunAsync(client, executor);

        result.Success.Should().BeTrue();
        result.Answer.Should().Be("Bitcoin is up.");

        executor.Executed.Should().HaveCount(1);
        observer.ToolCallsStarted.Should().Be(1);
        observer.ToolCallsCompleted.Should().Equal(("Summary of get_candles", true));

        // The reaction to the retrieved data is a new train of thought.
        observer.ThinkingBreaks.Should().Be(1);

        // The request must precede its result, otherwise the model can't tell
        // which answer belongs to which question.
        history.Select(x => x.Role).Should().Equal(ChatRoles.User,
            ChatRoles.Assistant, ChatRoles.Tool, ChatRoles.Assistant);

        history[1].ToolCalls.Should().HaveCount(1);
        history[2].ToolName.Should().Be("get_candles");
        history[2].Content.Should().Be("Data for get_candles");
    }

    [TestMethod]
    public async Task RunAsync_ReportsAFailedToolCallToTheModelAndTheUser()
    {
        var client = new StubClient(
            new Response("", [SomeCall()]),
            new Response("I could not get the data."));

        var executor = new StubExecutor(success: false);

        var (result, _, observer) = await RunAsync(client, executor);

        // A failure of a single request must not cost the user the whole turn.
        result.Success.Should().BeTrue();
        observer.ToolCallsCompleted.Should().Equal(("Summary of get_candles", false));
    }

    [TestMethod]
    public async Task RunAsync_ServesSeveralCallsOfASingleResponse()
    {
        var client = new StubClient(
            new Response("", [SomeCall(), SomeCall()]),
            new Response("Both are up."));

        var executor = new StubExecutor();

        var (result, history, observer) = await RunAsync(client, executor);

        result.Answer.Should().Be("Both are up.");
        executor.Executed.Should().HaveCount(2);
        observer.ToolCallsCompleted.Should().HaveCount(2);

        // One request message, then a result per call.
        history.Where(x => x.Role == ChatRoles.Tool).Should().HaveCount(2);
    }

    [TestMethod]
    public async Task RunAsync_BudgetExhausted_MakesAFinalPassWithoutTools()
    {
        // A model that asks for the data every single time it is given a chance.
        var responses = Enumerable.Range(0, ChatTurnRunner.MaxToolIterations)
            .Select(_ => new Response("", [SomeCall()]))
            .Append(new Response("Enough data.")).ToArray();

        var client = new StubClient(responses);
        var executor = new StubExecutor();

        var (result, history, _) = await RunAsync(client, executor);

        // The turn must terminate with an answer rather than loop forever.
        result.Success.Should().BeTrue();
        result.Answer.Should().Be("Enough data.");

        // Every iteration streams once, plus the final tools-withheld pass.
        client.Conversations.Should().HaveCount(ChatTurnRunner.MaxToolIterations + 1);

        // The last request is the only one made with the tools withheld, which
        // is what makes another request impossible.
        client.OfferedTools[^1].Should().BeNull();
        client.OfferedTools.SkipLast(1).Should().OnlyContain(x => x != null);

        // The budget allows one fewer execution than there are iterations: the
        // last request is answered with the note instead of the data.
        executor.Executed.Should().HaveCount(ChatTurnRunner.MaxToolIterations - 1);

        var lastToolMessage = history.Last(x => x.Role == ChatRoles.Tool);
        lastToolMessage.Content.Should().Contain("limit of the market data requests");
    }

    [TestMethod]
    public async Task RunAsync_StreamingFailure_RestoresTheHistory()
    {
        var client = new StubClient(new Response("Hi"))
        {
            Failure = (0, new HttpRequestException("The service is down."))
        };

        var (result, history, _) = await RunAsync(client);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("The service is down.");

        // A model that sees its own half-produced answer tends to get confused
        // by it, so the turn must leave no trace behind.
        history.Should().HaveCount(1);
        history[0].Role.Should().Be(ChatRoles.User);
    }

    [TestMethod]
    public async Task RunAsync_FailureAfterAToolCall_RemovesTheToolMessagesToo()
    {
        var client = new StubClient(new Response("", [SomeCall()]), new Response("Never sent"))
        {
            Failure = (1, new HttpRequestException("The service is down."))
        };

        var (result, history, _) = await RunAsync(client);

        result.Success.Should().BeFalse();

        // The request of the model and the data it received are both part of
        // the failed turn and must go away with it.
        history.Should().HaveCount(1);
        history[0].Role.Should().Be(ChatRoles.User);
    }

    [TestMethod]
    public async Task RunAsync_EmptyAnswer_IsReportedAsAFailedTurn()
    {
        var client = new StubClient(new Response("   "));

        var (result, history, _) = await RunAsync(client);

        // An answer of whitespace is of no use to the user and must not be
        // remembered as if the model had said something.
        result.Success.Should().BeFalse();
        history.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task RunAsync_Cancellation_IsReportedGracefully()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var client = new StubClient(new Response("Hi"))
        {
            Failure = (0, new OperationCanceledException())
        };

        var history = Conversation();

        var runner = new ChatTurnRunner(client, new StubExecutor());

        var result = await runner.RunAsync("some-model", history,
            [SomeTool()], new RecordingObserver(), cts.Token);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("canceled");
        history.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task RunAsync_TimeOut_IsDistinguishedFromCancellation()
    {
        var client = new StubClient(new Response("Hi"))
        {
            Failure = (0, new OperationCanceledException())
        };

        var (result, _, _) = await RunAsync(client);

        // The user has not asked to stop, so the same exception means the
        // request has run out of time instead.
        result.Error.Should().Contain("timed out");
    }

    [TestMethod]
    public async Task RunAsync_WithoutHistory_Throws()
    {
        var runner = new ChatTurnRunner(new StubClient(), new StubExecutor());

        var act = async () => await runner.RunAsync("some-model", null,
            [SomeTool()], new RecordingObserver(), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [TestMethod]
    public async Task RunAsync_WithoutObserver_Throws()
    {
        var runner = new ChatTurnRunner(new StubClient(), new StubExecutor());

        var act = async () => await runner.RunAsync("some-model", Conversation(),
            [SomeTool()], null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
