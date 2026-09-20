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

namespace BAnalyzerCore.Ollama;

/// <summary>
/// The long-running operations the agent can be busy with.
/// </summary>
/// <remarks>
/// Deliberately an enumeration rather than a ready-made message: wording and
/// localization of what the user sees belong to the presentation layer.
/// </remarks>
public enum AiAgentActivity
{
    /// <summary>
    /// The Ollama service is being looked up.
    /// </summary>
    Connecting,

    /// <summary>
    /// The capabilities of a model are being checked.
    /// </summary>
    CheckingModelCapabilities,

    /// <summary>
    /// A request has been issued and the answer of the model is awaited.
    /// </summary>
    WaitingForModel
}

/// <summary>
/// Reports the status of the agent to whoever displays it.
/// </summary>
/// <remarks>
/// Unlike <see cref="IChatTurnObserver"/>, which reports the progress of a
/// single turn, this one reports on the session as a whole (connecting,
/// checking model capabilities, waiting for a response). The methods are
/// called from the thread the corresponding operation is running on, so an
/// implementation that touches a user interface is responsible for
/// marshalling them itself.
/// </remarks>
public interface IAiAgentObserver
{
    /// <summary>
    /// Called whenever the agent begins another long-running operation.
    /// </summary>
    /// <param name="activity">The operation that has just begun.</param>
    /// <param name="model">
    /// The model the operation is about or "null" if it is not about one.
    /// </param>
    void OnActivityChanged(AiAgentActivity activity, string model);
}

/// <summary>
/// Outcome of a turn as the agent sees it.
/// </summary>
/// <param name="Answer">The answer of the model (empty if the turn failed).</param>
/// <param name="Error">Description of the failure or "null" if there was none.</param>
/// <param name="Cancelled">Indicates that the turn was terminated via <see cref="AiAgent.Cancel"/>.</param>
/// <param name="ToolsUnavailable">
/// Indicates that the turn was not run at all, because no tools could be put
/// at the disposal of the model: either it can't call them or the executor
/// has none to offer.
/// </param>
public sealed record AiTurnResult(string Answer, string Error,
    bool Cancelled = false, bool ToolsUnavailable = false)
{
    /// <summary>
    /// Indicates that the turn has produced an answer.
    /// </summary>
    public bool Success => !Cancelled && !ToolsUnavailable &&
                           Error == null && !string.IsNullOrWhiteSpace(Answer);
}

/// <summary>
/// Drives a conversation with a model over the course of a session: looks the
/// service up, keeps track of the history, resolves the tools a given model
/// can be offered and runs the turns, one at a time.
/// </summary>
/// <remarks>
/// This class is deliberately unaware of the user interface: it is a plain
/// session/agent abstraction that <see cref="ChatTurnRunner"/> (a single turn)
/// builds upon, so that the presentation layer only has to translate its
/// calls and callbacks into whatever a particular UI needs.
/// </remarks>
public sealed class AiAgent
{
    private readonly IOllamaClient _ollamaClient;
    private readonly IToolExecutor _toolExecutor;
    private readonly ChatTurnRunner _turnRunner;
    private readonly IAiAgentObserver _observer;

    /// <summary>
    /// The instruction that sets up the "role" of the model. Kept as the very
    /// first entry of <see cref="History"/> at all times, including after
    /// <see cref="ClearHistory"/> is called.
    /// </summary>
    private readonly string _systemPrompt;

    /// <summary>
    /// The conversation as it is sent to the model (includes the invisible
    /// "system" message).
    /// </summary>
    private readonly List<ChatMessage> _history;

    /// <summary>
    /// Cancellation source of the turn that is currently in progress (if any).
    /// </summary>
    private CancellationTokenSource _turnCts;

    /// <summary>
    /// Indicates that the turn currently in progress has been terminated via
    /// <see cref="Cancel"/> as opposed to having failed on its own.
    /// </summary>
    private volatile bool _cancelRequested;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="ollamaClient">The service the conversation is run against.</param>
    /// <param name="toolExecutor">The means the models are given to fetch the data they need.</param>
    /// <param name="systemPrompt">The instruction that sets up the "role" of the model.</param>
    /// <param name="observer">
    /// Optional recipient of the status reports of the agent.
    /// </param>
    public AiAgent(IOllamaClient ollamaClient, IToolExecutor toolExecutor, string systemPrompt,
        IAiAgentObserver observer = null)
    {
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _turnRunner = new ChatTurnRunner(_ollamaClient, toolExecutor);
        _systemPrompt = systemPrompt;
        _observer = observer;
        _history = [new ChatMessage(ChatRoles.System, _systemPrompt)];
    }

    /// <summary>
    /// The conversation as it is sent to the model (includes the "invisible"
    /// "system" message).
    /// </summary>
    public IReadOnlyList<ChatMessage> History => _history;

    /// <summary>
    /// Looks the Ollama service up and returns the collection of the
    /// available models.
    /// </summary>
    public Task<ModelsResult> ConnectAsync(CancellationToken ct)
    {
        _observer?.OnActivityChanged(AiAgentActivity.Connecting, null);

        return _ollamaClient.TryGetModelsAsync(ct);
    }

    /// <summary>
    /// Returns the tools the given <paramref name="model"/> is to be offered or
    /// "null" if there are none to offer, be it because the model can't call
    /// them or because the executor has nothing to expose.
    /// </summary>
    /// <remarks>
    /// The capabilities are cached by <see cref="IOllamaClient"/>, so asking
    /// about the same model again costs no extra request.
    /// </remarks>
    private async Task<IReadOnlyList<ToolDefinition>> ResolveToolsAsync(string model, CancellationToken ct)
    {
        _observer?.OnActivityChanged(AiAgentActivity.CheckingModelCapabilities, model);

        var caps = await _ollamaClient.GetModelCapsAsync(model, ct).ConfigureAwait(false);

        var definitions = caps is { Tools: true } ? _toolExecutor.GetToolDefinitions() : null;

        return definitions is { Count: > 0 } ? definitions : null;
    }

    /// <summary>
    /// Runs a single turn of the conversation: resolves the tools the given
    /// <paramref name="model"/> is to be offered, adds the given
    /// <paramref name="userText"/> to the history and streams the answer
    /// through <paramref name="turnObserver"/>. The turn can be interrupted
    /// with <see cref="Cancel"/> or through <paramref name="ct"/>.
    /// </summary>
    public async Task<AiTurnResult> SendAsync(string model, string userText,
        IChatTurnObserver turnObserver, CancellationToken ct = default)
    {
        _cancelRequested = false;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _turnCts = cts;

        try
        {
            // A model that can fetch the data on its own is given the means to
            // do so instead of a fixed snapshot it has not asked for.
            var tools = await ResolveToolsAsync(model, cts.Token).ConfigureAwait(false);

            if (tools == null)
                return new AiTurnResult(string.Empty, null, ToolsUnavailable: true);

            _history.Add(new ChatMessage(ChatRoles.User, userText));

            _observer?.OnActivityChanged(AiAgentActivity.WaitingForModel, model);

            var result = await _turnRunner.RunAsync(model, _history, tools, turnObserver, cts.Token)
                .ConfigureAwait(false);

            return new AiTurnResult(result.Answer, result.Error,
                Cancelled: !result.Success && _cancelRequested);
        }
        finally
        {
            _turnCts = null;
        }
    }

    /// <summary>
    /// Cancels the turn that is currently in progress, if any. Safe to call
    /// when the agent is idle.
    /// </summary>
    public void Cancel()
    {
        var cts = _turnCts;

        if (cts == null) return;

        _cancelRequested = true;

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The turn has finished on its own in the meantime.
        }
    }

    /// <summary>
    /// Resets the conversation back to just the system prompt.
    /// </summary>
    public void ClearHistory()
    {
        _history.Clear();
        _history.Add(new ChatMessage(ChatRoles.System, _systemPrompt));
    }
}
