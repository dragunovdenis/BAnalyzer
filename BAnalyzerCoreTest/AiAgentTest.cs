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
using BAnalyzerCore.Ollama;
using FluentAssertions;

namespace BAnalyzerCoreTest;

/// <summary>
/// Tests of <see cref="AiAgent"/>.
/// </summary>
/// <remarks>
/// The agent is exercised against the fakes below, so that the tests need
/// neither an Ollama service nor an exchange to run.
/// </remarks>
[TestClass]
public class AiAgentTest
{
    /// <summary>
    /// A client whose behaviour is fully configurable from the tests.
    /// </summary>
    private sealed class StubClient : IOllamaClient
    {
        public ModelsResult ModelsResult { get; init; } = new(true, [], null);

        /// <summary>
        /// Capabilities returned for a given model name, or "null" if the
        /// model is not known.
        /// </summary>
        public Dictionary<string, ModelCaps?> Caps { get; } = new();

        /// <summary>
        /// The tool sets the client was called with, in order. A "null" entry
        /// means the tools were withheld.
        /// </summary>
        public List<IReadOnlyList<ToolDefinition>> OfferedTools { get; } = new();

        /// <summary>
        /// Snapshots of the history as of every streaming call, in order.
        /// </summary>
        public List<List<ChatMessage>> Conversations { get; } = new();

        /// <summary>
        /// The response to stream back, or "null" to block until the given
        /// cancellation token is triggered (used to simulate a turn that is
        /// terminated by the user).
        /// </summary>
        public string Answer { get; set; } = "Hello there";

        public Task<ModelsResult> TryGetModelsAsync(CancellationToken ct) =>
            Task.FromResult(ModelsResult);

        public Task<ModelCaps?> GetModelCapsAsync(string model, CancellationToken ct) =>
            Task.FromResult(Caps.GetValueOrDefault(model));

        public async Task<bool> SupportsToolsAsync(string model, CancellationToken ct) =>
            (await GetModelCapsAsync(model, ct).ConfigureAwait(false))?.Tools ?? false;

        public async IAsyncEnumerable<ChatChunk> ChatStreamAsync(string model,
            IReadOnlyList<ChatMessage> history, IReadOnlyList<ToolDefinition> tools,
            [EnumeratorCancellation] CancellationToken ct)
        {
            Conversations.Add(history.ToList());
            OfferedTools.Add(tools);

            if (Answer == null)
            {
                // Blocks until cancelled, so that a turn can be terminated
                // mid-flight, just like a real streaming call would be.
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                yield break;
            }

            foreach (var piece in Answer)
                yield return new ChatChunk(piece.ToString(), null, null);

            await Task.CompletedTask;
        }

        public void Dispose() { }
    }

    /// <summary>
    /// An executor that serves every request with a fixed answer.
    /// </summary>
    private sealed class StubExecutor : IToolExecutor
    {
        public ToolDefinition Definition { get; set; } =
            new("get_candles", "Returns candles.",
                [new ToolParameter("symbol", "string", "Trading pair.")]);

        public IReadOnlyList<ToolDefinition> GetToolDefinitions() =>
            Definition == null ? [] : [Definition];

        public Task<ToolCallResult> ExecuteAsync(ToolCall call, CancellationToken ct) =>
            Task.FromResult(new ToolCallResult($"Data for {call.Name}", $"Summary of {call.Name}", true));
    }

    /// <summary>
    /// An observer that merely records what it is told.
    /// </summary>
    private sealed class RecordingTurnObserver : IChatTurnObserver
    {
        public string Content { get; private set; } = "";

        public void OnThinking(string text) { }
        public void OnContent(string text) => Content += text;
        public void OnThinkingBreak() { }
        public void OnToolCallStarted() { }
        public void OnToolCallCompleted(string summary, bool success) { }
    }

    /// <summary>
    /// An observer that records every activity it is told about, in order.
    /// </summary>
    private sealed class RecordingAgentObserver : IAiAgentObserver
    {
        public List<(AiAgentActivity Activity, string Model)> Activities { get; } = [];

        public void OnActivityChanged(AiAgentActivity activity, string model) =>
            Activities.Add((activity, model));
    }

    private const string SystemPrompt = "You are a helpful assistant.";

    /// <summary>
    /// Capabilities of a model that can request the data it needs.
    /// </summary>
    private static readonly ModelCaps ToolCaps = new(0, false, true, false, true);

    /// <summary>
    /// Builds an agent for a client that knows a single tool-capable model.
    /// </summary>
    private static AiAgent BuildAgent(StubClient client, string model = "some-model")
    {
        client.Caps[model] = ToolCaps;

        return new AiAgent(client, new StubExecutor(), SystemPrompt);
    }

    [TestMethod]
    public async Task ConnectAsync_ReturnsWhateverTheClientReports()
    {
        // Arrange
        var client = new StubClient
        {
            ModelsResult = new ModelsResult(false, [], "boom")
        };

        var observer = new RecordingAgentObserver();
        var agent = new AiAgent(client, new StubExecutor(), SystemPrompt, observer);

        // Act
        var result = await agent.ConnectAsync(CancellationToken.None);

        // Assert
        result.Available.Should().BeFalse();
        result.Error.Should().Be("boom");
        observer.Activities.Should().ContainSingle()
            .Which.Activity.Should().Be(AiAgentActivity.Connecting);
    }

    [TestMethod]
    public async Task SendAsync_AppendsTheUserMessageAndRunsTheTurn()
    {
        // Arrange
        var client = new StubClient { Answer = "Hi there" };
        client.Caps["some-model"] = ToolCaps;

        var executor = new StubExecutor();
        var agentObserver = new RecordingAgentObserver();
        var agent = new AiAgent(client, executor, SystemPrompt, agentObserver);
        var turnObserver = new RecordingTurnObserver();

        // Act
        var result = await agent.SendAsync("some-model", "Hello", turnObserver);

        // Assert
        result.Success.Should().BeTrue();
        result.Answer.Should().Be("Hi there");
        result.Cancelled.Should().BeFalse();
        result.ToolsUnavailable.Should().BeFalse();
        turnObserver.Content.Should().Be("Hi there");

        agent.History.Select(x => x.Role).Should().Equal(
            ChatRoles.System, ChatRoles.User, ChatRoles.Assistant);
        agent.History[1].Content.Should().Be("Hello");

        // The tools the agent has resolved are the ones the model is offered.
        client.OfferedTools.Should().ContainSingle()
            .Which.Should().ContainSingle().Which.Should().BeSameAs(executor.Definition);

        agentObserver.Activities.Should().Equal(
            (AiAgentActivity.CheckingModelCapabilities, "some-model"),
            (AiAgentActivity.WaitingForModel, "some-model"));
    }

    [TestMethod]
    public async Task SendAsync_ModelWithoutToolsCapability_ReportsItAndLeavesTheHistoryAlone()
    {
        // Arrange
        var client = new StubClient();
        client.Caps["no-tools-model"] = new ModelCaps(0, false, true, false, false);

        var agent = new AiAgent(client, new StubExecutor(), SystemPrompt);

        // Act
        var result = await agent.SendAsync("no-tools-model", "Hello", new RecordingTurnObserver());

        // Assert
        result.ToolsUnavailable.Should().BeTrue();
        result.Success.Should().BeFalse();
        client.Conversations.Should().BeEmpty();
        agent.History.Should().ContainSingle();
    }

    [TestMethod]
    public async Task SendAsync_ExecutorWithoutADefinition_ReportsTheToolsAsUnavailable()
    {
        // Arrange
        var client = new StubClient();
        client.Caps["some-model"] = ToolCaps;

        // The model is perfectly capable, but there is nothing to offer it.
        var agent = new AiAgent(client, new StubExecutor { Definition = null }, SystemPrompt);

        // Act
        var result = await agent.SendAsync("some-model", "Hello", new RecordingTurnObserver());

        // Assert
        result.ToolsUnavailable.Should().BeTrue();
        client.Conversations.Should().BeEmpty();
        agent.History.Should().ContainSingle();
    }

    [TestMethod]
    public async Task SendAsync_FailedTurn_RestoresTheHistory()
    {
        // Arrange
        var client = new StubClient { Answer = "" }; // empty answer => the turn fails
        var agent = BuildAgent(client);

        // Act
        var result = await agent.SendAsync("some-model", "Hello", new RecordingTurnObserver());

        // Assert
        result.Success.Should().BeFalse();
        result.Cancelled.Should().BeFalse();

        // The user message is kept (ChatTurnRunner only rolls back what a
        // failed turn itself produced), so the user can still see what was
        // asked and retry.
        agent.History.Select(x => x.Role).Should().Equal(ChatRoles.System, ChatRoles.User);
    }

    [TestMethod]
    public async Task Cancel_TerminatesTheInProgressTurn()
    {
        // Arrange
        var client = new StubClient { Answer = null }; // blocks until cancelled
        var agent = BuildAgent(client);

        // Act
        var sendTask = agent.SendAsync("some-model", "Hello", new RecordingTurnObserver());

        // Give the turn a chance to actually start before it is cancelled.
        while (client.Conversations.Count == 0)
            await Task.Delay(10);

        // Act
        agent.Cancel();

        var result = await sendTask;

        result.Success.Should().BeFalse();
        result.Cancelled.Should().BeTrue();
    }

    [TestMethod]
    public async Task SendAsync_CancelledThroughTheGivenToken_IsNotReportedAsTerminatedByTheUser()
    {
        // Arrange
        var client = new StubClient { Answer = null }; // blocks until cancelled
        var agent = BuildAgent(client);
        using var cts = new CancellationTokenSource();

        // Act
        var sendTask = agent.SendAsync("some-model", "Hello", new RecordingTurnObserver(), cts.Token);

        while (client.Conversations.Count == 0)
            await Task.Delay(10);

        await cts.CancelAsync();

        var result = await sendTask;

        // Assert
        result.Success.Should().BeFalse();
        result.Cancelled.Should().BeFalse();
    }

    [TestMethod]
    public void Cancel_WithoutAnInProgressTurn_DoesNothing()
    {
        // Arrange
        var agent = new AiAgent(new StubClient(), new StubExecutor(), SystemPrompt);

        // Act
        var act = agent.Cancel;
        
        // Assert
        act.Should().NotThrow();
    }

    [TestMethod]
    public async Task ClearHistory_ResetsToJustTheSystemPrompt()
    {
        // Arrange
        var client = new StubClient { Answer = "Hi" };
        var agent = BuildAgent(client);

        await agent.SendAsync("some-model", "Hello", new RecordingTurnObserver());
        agent.History.Should().HaveCount(3);

        // Act
        agent.ClearHistory();

        // Assert
        agent.History.Should().ContainSingle();
        agent.History[0].Role.Should().Be(ChatRoles.System);
        agent.History[0].Content.Should().Be(SystemPrompt);
    }
}
