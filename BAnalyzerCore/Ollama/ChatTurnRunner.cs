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

using System.Text;

namespace BAnalyzerCore.Ollama;

/// <summary>
/// Reports the progress of a conversation turn to whoever displays it.
/// </summary>
/// <remarks>
/// The turn is driven by <see cref="ChatTurnRunner"/>, which knows nothing
/// about the way it is presented. The methods are called from the thread the
/// turn is running on, so an implementation that touches a user interface is
/// responsible for marshalling them itself.
/// </remarks>
public interface IChatTurnObserver
{
    /// <summary>
    /// Called when a portion of the "reasoning" arrives.
    /// </summary>
    void OnThinking(string text);

    /// <summary>
    /// Called when a portion of the answer arrives.
    /// </summary>
    void OnContent(string text);

    /// <summary>
    /// Called when the model starts a new train of thought, i.e. when it
    /// reacts to the data it has just requested.
    /// </summary>
    void OnThinkingBreak();

    /// <summary>
    /// Called before the data requested by the model is retrieved.
    /// </summary>
    void OnToolCallStarted();

    /// <summary>
    /// Called once a request of the model for the data has been served.
    /// </summary>
    void OnToolCallCompleted(string summary, bool success);
}

/// <summary>
/// Outcome of a conversation turn.
/// </summary>
/// <param name="Answer">The answer of the model (empty if the turn failed).</param>
/// <param name="Error">Description of the failure or "null" if there was none.</param>
public sealed record ChatTurnResult(string Answer, string Error)
{
    /// <summary>
    /// Indicates that the turn has produced an answer.
    /// </summary>
    public bool Success => Error == null && !string.IsNullOrWhiteSpace(Answer);
}

/// <summary>
/// Runs a single conversation turn: streams the answer of the model, serves
/// the requests for the market data it makes along the way and maintains the
/// conversation history accordingly.
/// </summary>
public sealed class ChatTurnRunner(IOllamaClient client, IToolExecutor toolExecutor)
{
    /// <summary>
    /// The largest number of times a model is allowed to request the data
    /// within a single conversation turn. Guards against a model that keeps
    /// asking for the data instead of answering.
    /// </summary>
    public const int MaxToolIterations = 25;

    /// <summary>
    /// The note that is put into the conversation once the model has exhausted
    /// its budget of the data requests.
    /// </summary>
    internal const string BudgetExhaustedNote =
        "The limit of the market data requests per message has been reached. " +
        "Answer with the data you already have.";

    /// <summary>
    /// Runs a turn against the given <paramref name="history"/>, which gets
    /// extended with everything the turn produces.
    /// </summary>
    /// <remarks>
    /// The history is restored to its original state if the turn fails, so that
    /// the conversation proceeds as if the turn had never happened: a model
    /// that sees its own half-produced answer tends to get confused by it.
    /// </remarks>
    public async Task<ChatTurnResult> RunAsync(string model, List<ChatMessage> history,
        IReadOnlyList<ToolDefinition> tools, IChatTurnObserver observer, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(observer);

        // The exact state to return to if the turn does not work out. A count
        // of the added messages would not do, because the caller can prune the
        // history in the course of preparing the turn, which leaves any
        // count-based bookkeeping pointing at the wrong messages.
        var restorePoint = history.ToList();

        var answer = new StringBuilder();
        string error = null;

        try
        {
            await RunIterationsAsync(model, history, tools, observer, answer, ct).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            // Streaming is the one operation of the client that throws.
            error = Describe(e, ct);
        }

        var result = new ChatTurnResult(answer.ToString(), error);

        if (!result.Success)
        {
            history.Clear();
            history.AddRange(restorePoint);
        }
        else history.Add(new ChatMessage(ChatRoles.Assistant, result.Answer));

        return result;
    }

    /// <summary>
    /// Runs the turn until the model stops requesting the data (or runs out of
    /// the opportunities to do so).
    /// </summary>
    private async Task RunIterationsAsync(string model, List<ChatMessage> history,
        IReadOnlyList<ToolDefinition> tools, IChatTurnObserver observer,
        StringBuilder answer, CancellationToken ct)
    {
        for (var iteration = 0; ; iteration++)
        {
            var calls = await StreamAsync(model, history, tools, observer, answer, ct)
                .ConfigureAwait(false);

            // There is no dedicated "the model wants a tool" terminator in the
            // protocol: a turn that requests the data ends exactly like a turn
            // that answers, so the decision is made on what has been collected.
            if (calls.Count == 0) return;

            // The request must precede its results, otherwise the model can't
            // tell which answer belongs to which question. Only the calls are
            // attributed to it: the text produced so far is accounted for by
            // the answer as a whole and repeating it here would feed the model
            // its own words twice.
            history.Add(new ChatMessage(ChatRoles.Assistant, null, calls));

            if (iteration >= MaxToolIterations - 1)
            {
                // The budget is spent: nothing is executed anymore and the
                // model is merely told why.
                foreach (var call in calls)
                    history.Add(new ChatMessage(ChatRoles.Tool, BudgetExhaustedNote, ToolName: call.Name));

                observer.OnThinkingBreak();

                // The final pass is made with the tools withheld, so that the
                // model is unable to ask for anything else and has to answer.
                // Merely telling it to stop would not do: the request that got
                // us here proves it is willing to ignore that.
                await StreamAsync(model, history, null, observer, answer, ct).ConfigureAwait(false);

                return;
            }

            await ExecuteCallsAsync(calls, history, observer, ct).ConfigureAwait(false);

            // What the model thinks next is a reaction to the data it has just
            // received, i.e. a new train of thought.
            observer.OnThinkingBreak();
        }
    }

    /// <summary>
    /// Streams a single response of the model, reporting it to the observer and
    /// returning the tool calls it has requested (if any).
    /// </summary>
    private async Task<IReadOnlyList<ToolCall>> StreamAsync(string model, IReadOnlyList<ChatMessage> history,
        IReadOnlyList<ToolDefinition> tools, IChatTurnObserver observer,
        StringBuilder answer, CancellationToken ct)
    {
        var calls = new List<ToolCall>();

        await foreach (var chunk in client.ChatStreamAsync(model, history, tools, ct).ConfigureAwait(false))
        {
            if (chunk.ToolCalls != null) calls.AddRange(chunk.ToolCalls);

            if (!string.IsNullOrEmpty(chunk.Thinking)) observer.OnThinking(chunk.Thinking);

            if (string.IsNullOrEmpty(chunk.Content)) continue;

            observer.OnContent(chunk.Content);
            answer.Append(chunk.Content);
        }

        return calls;
    }

    /// <summary>
    /// Serves the given requests of the model for the data and puts the results
    /// into the conversation.
    /// </summary>
    private async Task ExecuteCallsAsync(IReadOnlyList<ToolCall> calls, List<ChatMessage> history,
        IChatTurnObserver observer, CancellationToken ct)
    {
        foreach (var call in calls)
        {
            observer.OnToolCallStarted();

            var result = await toolExecutor.ExecuteAsync(call, ct).ConfigureAwait(false);

            history.Add(new ChatMessage(ChatRoles.Tool, result.Content, ToolName: call.Name));

            // The data the model has chosen for itself is only trustworthy if
            // the choice is visible, hence it is always reported.
            observer.OnToolCallCompleted(result.Summary, result.Success);
        }
    }

    /// <summary>
    /// Converts an exception thrown by the streaming method into a message for the user.
    /// </summary>
    private static string Describe(Exception e, CancellationToken ct)
    {
        if (e is OperationCanceledException)
            return ct.IsCancellationRequested ? "The request was canceled." : "The request timed out.";

        return e.Message;
    }
}
