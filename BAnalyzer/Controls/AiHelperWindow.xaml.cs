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

using BAnalyzer.DataStructures;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using BAnalyzerCore.Ollama;
using BAnalyzer.Utils;

namespace BAnalyzer.Controls;

/// <summary>
/// Display wrapper (view model) of <see cref="ModelInfo"/> that lets the context window be
/// filled in once it is fetched (which takes a separate, slower request),
/// without disturbing the rest of the properties or the selection in the
/// drop-down list the instances are displayed in.
/// </summary>
public class ModelOptionVm(ModelInfo model) : INotifyPropertyChanged
{
    /// <summary>
    /// Name of the model (the identifier accepted by the "/api/chat" end-point).
    /// </summary>
    public string Name { get; } = model.Name;

    /// <summary>
    /// Context window of the model, in tokens, or "null" if not fetched (yet)
    /// or not reported by the service.
    /// </summary>
    public int? ContextWindow
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Description));
        }
    }

    /// <summary>
    /// Human-readable summary of the model's size, parameter count,
    /// quantization level and context window, suitable for display next to
    /// the model name. Empty if none of the details are available.
    /// </summary>
    public string Description
    {
        get
        {
            var description = model.Description;

            if (ContextWindow is not > 0) return description;

            var contextText = ContextWindow.Value >= 1024
                ? $"{ContextWindow.Value / 1024.0:0.#}K ctx"
                : $"{ContextWindow.Value} ctx";

            return string.IsNullOrEmpty(description) ? contextText : $"{description} · {contextText}";
        }
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Raises the "PropertyChanged" event.
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// View model of a single market data request issued by a model, as it is
/// shown in the transcript.
/// </summary>
/// <param name="Description">Short description of what has been requested.</param>
/// <param name="Success">Whether the requested data could be retrieved.</param>
public sealed record ToolCallVm(string Description, bool Success);

/// <summary>
/// A single item of the visible conversation transcript.
/// </summary>
/// <remarks>
/// Unlike an ordinary "immutable" view model, this one is mutable: a response
/// of a model is displayed while it is being generated, so the corresponding
/// item grows "in place" instead of being added to the transcript as a whole.
/// </remarks>
public class ChatMessageVm : INotifyPropertyChanged
{
    /// <summary>
    /// Constructor.
    /// </summary>
    public ChatMessageVm(string title, string content, bool isRequest,
        string model = null, string question = null, DateTime timeStamp = default)
    {
        Title = title;
        _content = content;
        Alignment = isRequest ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        Model = model;
        Question = question;
        TimeStamp = timeStamp;
    }

    /// <summary>
    /// Caption of the item.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Position of the item within the transcript.
    /// </summary>
    public HorizontalAlignment Alignment { get; }

    /// <summary>
    /// Model that produced the item ("null" for the items that are not model responses).
    /// </summary>
    public string Model { get; }

    /// <summary>
    /// The message the response was given to.
    /// </summary>
    public string Question { get; }

    /// <summary>
    /// Moment the item was added.
    /// </summary>
    public DateTime TimeStamp { get; }

    /// <summary>
    /// Indicates that the item is a model response and thus can be saved to a file.
    /// </summary>
    public bool CanSave => Model != null;

    private string _content;

    /// <summary>
    /// Text of the item. It is the "markdown" source as the model produced it,
    /// which is what gets saved to a file, no matter how it is displayed.
    /// </summary>
    public string Content
    {
        get => _content;
        set
        {
            if (_content == value) return;

            _content = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasContent));
            OnPropertyChanged(nameof(MaxWidth));
        }
    }

    /// <summary>
    /// Indicates that there is an answer to display.
    /// </summary>
    public bool HasContent => !string.IsNullOrEmpty(Content);

    /// <summary>
    /// Indicates that the answer is displayed as a formatted "markdown"
    /// rather than as a plain text.
    /// </summary>
    /// <remarks>
    /// While an answer is being streamed, its "markdown" is necessarily
    /// incomplete (a table is a table only once its last row has arrived), so
    /// re-rendering it on every token would produce a flicker of half-built
    /// constructs. The plain text is therefore shown until the answer is
    /// complete, and only then it is handed over to the renderer.
    /// </remarks>
    public bool IsFormatted
    {
        get;
        private set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPlainText));
        }
    }

    /// <summary>
    /// Indicates that the answer is displayed as a plain text.
    /// </summary>
    public bool IsPlainText => !IsFormatted;

    /// <summary>
    /// Declares the item complete, so that its content can be displayed in
    /// its final, formatted form.
    /// </summary>
    public void Complete()
    {
        IsFormatted = true;
        OnPropertyChanged(nameof(MaxWidth));
    }

    /// <summary>
    /// Width the item is allowed to occupy. A table does not wrap, so a message
    /// that contains one is given more room than an ordinary one, which would
    /// otherwise be squeezed into a barely readable column of clipped cells.
    /// </summary>
    public double MaxWidth => IsFormatted && MarkdownPresenter.ContainsTable(Content)
        ? WideWidth : NormalWidth;

    /// <summary>
    /// Width of an ordinary message.
    /// </summary>
    private const double NormalWidth = 480;

    /// <summary>
    /// Width of a message containing a table.
    /// </summary>
    private const double WideWidth = 640;

    /// <summary>
    /// The "reasoning" the model produced before the answer. Stays empty for
    /// the models that do not report it.
    /// </summary>
    public string Thinking
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasThinking));
        }
    }

    /// <summary>
    /// Indicates that there is a "reasoning" to display.
    /// </summary>
    public bool HasThinking => !string.IsNullOrEmpty(Thinking);

    /// <summary>
    /// Indicates that the "reasoning" section is unfolded. It is kept unfolded
    /// while the reasoning is being generated (so that the user can watch the
    /// process) and gets folded when the answer itself begins to arrive.
    /// </summary>
    public bool IsReasoningExpanded
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Appends the given portion of the "reasoning" to the item.
    /// </summary>
    public void AppendThinking(string text)
    {
        if (_thinkingBreakPending && HasThinking)
        {
            Thinking = Thinking.TrimEnd() + Environment.NewLine + Environment.NewLine;
            _thinkingBreakPending = false;
        }

        Thinking += text;
    }

    private bool _thinkingBreakPending;

    /// <summary>
    /// Separates the "reasoning" produced so far from the one that is about to
    /// follow. Called between the rounds of a conversation turn in which the
    /// model requests the market data: the rounds are separate trains of
    /// thought and running them together reads as a single confused one.
    /// </summary>
    /// <remarks>
    /// The separator is inserted lazily, when (and if) the next portion of the
    /// reasoning arrives, so that a round that produces no reasoning at all
    /// does not leave a dangling blank line at the end.
    /// </remarks>
    public void BreakThinking() => _thinkingBreakPending = true;

    /// <summary>
    /// Appends the given portion of the answer to the item.
    /// </summary>
    public void AppendContent(string text) => Content += text;

    /// <summary>
    /// Descriptions of the market data requests the model has issued while
    /// producing the item.
    /// </summary>
    public ObservableCollection<ToolCallVm> ToolCalls { get; } = [];

    /// <summary>
    /// Indicates that there are market data requests to display.
    /// </summary>
    public bool HasToolCalls => ToolCalls.Count > 0;

    /// <summary>
    /// Registers a market data request issued by the model.
    /// </summary>
    public void AddToolCall(string description, bool success)
    {
        ToolCalls.Add(new ToolCallVm(description, success));
        OnPropertyChanged(nameof(HasToolCalls));
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Property changed notification.
    /// </summary>
    private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// States the "AI helper" window can be in.
/// </summary>
internal enum AiHelperStatus
{
    Checking,
    Unavailable,
    NoModels,
    Ready,
    Busy,
    Streaming
}

/// <summary>
/// Interaction logic for AiHelperWindow.xaml
/// </summary>
public partial class AiHelperWindow : INotifyPropertyChanged
{
    /// <summary>
    /// Instruction that sets up the "role" of the model.
    /// </summary>
    private const string SystemPrompt =
        "You are an analysis assistant embedded in BAnalyzer, a crypto market analysis application. " +
        "When market data context is provided, base your answers on it and state the figures you used. " +
        "You are free to use statistical analysis to build hypotheses and validate your conclusions. " +
        "Whenever you come up with a hypothesis on how the market behaves (including forecasts on how " +
        "market may behave in future), explain your reasoning and give a statistical estimate of the " +
        "reliability of your conclusions." +
        "You are not a financial advisor; do not give investment advice.";

    private readonly IOllamaClient _ollamaClient;
    private readonly CandleToolExecutor _toolExecutor;
    private readonly ChatTurnRunner _turnRunner;

    /// <summary>
    /// The conversation as it is sent to the model (includes the invisible
    /// "system" messages, unlike <see cref="Transcript"/>).
    /// </summary>
    private readonly List<ChatMessage> _history = [new(ChatRoles.System, SystemPrompt)];

    /// <summary>
    /// Cancellation source of the request that is currently in progress (if any).
    /// </summary>
    private CancellationTokenSource _requestCts;

    /// <summary>
    /// Constructor.
    /// </summary>
    public AiHelperWindow(IOllamaClient ollamaClient, IMultiExchange exchange)
    {
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));

        if (exchange == null)
            throw new ArgumentNullException(nameof(exchange));

        _toolExecutor = new CandleToolExecutor(exchange[ExchangeId.Binance]);
        _turnRunner = new ChatTurnRunner(_ollamaClient, _toolExecutor);

        InitializeComponent();
    }

    /// <summary>
    /// The visible part of the conversation.
    /// </summary>
    public ObservableCollection<ChatMessageVm> Transcript { get; } = [];

    /// <summary>
    /// Collection of the locally available models.
    /// </summary>
    public ObservableCollection<ModelOptionVm> Models { get; } = [];

    /// <summary>
    /// The model the conversation is run with.
    /// </summary>
    public string SelectedModel
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Description of the operation that is currently in progress.
    /// </summary>
    public string StatusText
    {
        get;
        private set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Text of the warning banner.
    /// </summary>
    public string BannerText
    {
        get;
        private set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasBanner));
        }
    }

    /// <summary>
    /// Indicates that the warning banner should be shown.
    /// </summary>
    public bool HasBanner => !string.IsNullOrEmpty(BannerText);

    /// <summary>
    /// Indicates that a request is in progress but nothing has been received yet,
    /// so that an "indeterminate" progress indication is the only thing that can
    /// be shown. As soon as the answer starts arriving it becomes its own
    /// progress indication and the indicator is not needed anymore.
    /// </summary>
    public bool IsBusy => Status is AiHelperStatus.Checking or AiHelperStatus.Busy;

    /// <summary>
    /// Indicates that the user can type and send messages.
    /// </summary>
    public bool CanInteract => Status == AiHelperStatus.Ready;

    /// <summary>
    /// Indicates that the "retry" button should be shown.
    /// </summary>
    public bool CanRetry => Status is AiHelperStatus.Unavailable or AiHelperStatus.NoModels;

    /// <summary>
    /// Indicates that there is a request that can be terminated.
    /// </summary>
    public bool CanTerminate => Status is AiHelperStatus.Busy or AiHelperStatus.Streaming;

    /// <summary>
    /// Current state of the window.
    /// </summary>
    private AiHelperStatus Status
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(CanInteract));
            OnPropertyChanged(nameof(CanRetry));
            OnPropertyChanged(nameof(CanTerminate));
        }
    } = AiHelperStatus.Checking;

    private bool _initialized;

    /// <summary>
    /// Loaded event handler.
    /// </summary>
    private async void AiHelperWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized) return;

        _initialized = true;
        await ConnectAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Looks the Ollama service up and retrieves the collection of the available models.
    /// </summary>
    private async Task ConnectAsync()
    {
        Status = AiHelperStatus.Checking;
        StatusText = "Looking for the Ollama service...";
        BannerText = null;

        var result = await _ollamaClient.TryGetModelsAsync(CancellationToken.None).ConfigureAwait(true);

        Models.Clear();

        if (!result.Available)
        {
            Status = AiHelperStatus.Unavailable;
            BannerText = "Ollama service is not available. Please make sure that Ollama is installed " +
                         "and running (see https://ollama.com) in order to use the AI Helper." +
                         Environment.NewLine + result.Error;
            return;
        }

        if (result.Models.Count == 0)
        {
            Status = AiHelperStatus.NoModels;
            BannerText = "No models are available locally. Install one first, for example " +
                         "by running \"ollama pull llama3\" in a terminal.";
            return;
        }

        foreach (var model in result.Models)
            Models.Add(new ModelOptionVm(model));

        SelectedModel = Models[0].Name;
        Status = AiHelperStatus.Ready;
        StatusText = null;

        // Fetching the context window takes a separate, slower request per
        // model, so it is not worth holding the "ready" status up for it: the
        // drop-down list is updated progressively as the answers come in.
        _ = FetchContextWindowsAsync();
    }

    /// <summary>
    /// Retrieves and displays the context window of every model currently
    /// listed in <see cref="Models"/>.
    /// </summary>
    private async Task FetchContextWindowsAsync()
    {
        var options = Models.ToArray();

        var tasks = options.Select(async option =>
        {
            var caps = await _ollamaClient
                .GetModelCapsAsync(option.Name, CancellationToken.None).ConfigureAwait(true);

            option.ContextWindow = caps?.ContextWindow ?? -1;
        });

        await Task.WhenAll(tasks).ConfigureAwait(true);
    }

    /// <summary>
    /// Handles the "click" event of the "retry" button.
    /// </summary>
    private async void Retry_OnClick(object sender, RoutedEventArgs e) =>
        await ConnectAsync().ConfigureAwait(true);

    /// <summary>
    /// Handles the "click" event of the "send" button.
    /// </summary>
    private async void Send_OnClick(object sender, RoutedEventArgs e) =>
        await SendAsync().ConfigureAwait(true);

    /// <summary>
    /// Sends message with "enter" and inserts a new line with "shift + enter".
    /// </summary>
    private async void InputBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            return;

        e.Handled = true;
        await SendAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Runs a single turn of the conversation.
    /// </summary>
    private async Task SendAsync()
    {
        if (Status != AiHelperStatus.Ready) return;

        var text = InputBox.Text?.Trim();

        if (string.IsNullOrEmpty(text)) return;

        InputBox.Clear();
        Status = AiHelperStatus.Busy;

        using var cts = new CancellationTokenSource();
        _requestCts = cts;

        var model = SelectedModel;

        try
        {
            // A model that can fetch the data on its own is given the means to do
            // so instead of a fixed snapshot it has not asked for.
            var tools = await ResolveToolsAsync(model, cts.Token).ConfigureAwait(true);

            if (tools == null)
            {
                throw new InvalidOperationException($"The model {model} does not support tools and cannot fetch the data on its own.");
            }
            else AddToTranscript("You", text, isRequest: true);

            _history.Add(new ChatMessage(ChatRoles.User, text));

            StatusText = $"Waiting for \"{model}\"... this may take a while for large models.";

            var response = new ChatMessageVm(model, string.Empty, isRequest: false,
                model, text, DateTime.Now);

            var result = await _turnRunner.RunAsync(model, _history, tools,
                new TranscriptObserver(this, response, model), cts.Token).ConfigureAwait(true);

            if (!result.Success)
            {
                if (cts.IsCancellationRequested)
                {
                    // Whatever has been generated so far is kept visible, but the
                    // conversation proceeds as if the turn never happened.
                    if (Transcript.Contains(response))
                        response.AppendContent(Environment.NewLine + "[terminated by the user]");
                    else
                        AddToTranscript("Terminated", "The request was terminated by the user.",
                            isRequest: false);

                    // Give the user a chance to edit and re-send the message.
                    InputBox.Text = text;
                    InputBox.CaretIndex = text.Length;
                }
                else
                {
                    if (Transcript.Contains(response))
                    {
                        Transcript.Remove(response);
                        OnPropertyChanged(nameof(HasTranscript));
                    }

                    AddToTranscript("Error", $"Failed to reach Ollama: " +
                                             $"{result.Error ?? "The model returned an empty answer."}",
                        isRequest: false);
                }
            }

            // The answer is not going to change anymore, so whatever has been
            // collected (including a partial answer of a terminated request)
            // can now be displayed as a formatted "markdown".
            response.Complete();
        }
        finally
        {
            _requestCts = null;
            StatusText = null;
            Status = AiHelperStatus.Ready;
        }
    }

    /// <summary>
    /// Returns the tools the given <paramref name="model"/> is to be offered or
    /// "null" if it can't call them, in which case the caller is responsible
    /// for supplying the market data itself.
    /// </summary>
    private async Task<IReadOnlyList<ToolDefinition>> ResolveToolsAsync(string model, CancellationToken ct)
    {
        StatusText = "Checking the capabilities of the model...";

        var caps = await _ollamaClient.GetModelCapsAsync(model, ct).ConfigureAwait(true);
        if (caps is not { Tools: true })
            return null;

        var definition = _toolExecutor.GetToolDefinition();

        return definition == null ? null : new[] { definition };
    }

    /// <summary>
    /// Displays the progress of a conversation turn in the transcript.
    /// </summary>
    /// <remarks>
    /// The turn is run by <see cref="ChatTurnRunner"/>, which is deliberately
    /// unaware of the user interface and therefore reports on whatever thread
    /// happens to be carrying the response. Marshalling is consequently the
    /// job of this class: every callback is routed through <see cref="Dispatch"/>.
    /// </remarks>
    private sealed class TranscriptObserver(AiHelperWindow window, ChatMessageVm response, string model)
        : IChatTurnObserver
    {
        /// <summary>
        /// Executes the given <paramref name="action"/> on the thread of the
        /// user interface.
        /// </summary>
        /// <remarks>
        /// The call is synchronous, so that the portions of the answer reach
        /// the transcript in the order they were produced. It can't deadlock:
        /// the thread being waited for is the one running the turn, which the
        /// interface does not block on.
        /// </remarks>
        private void Dispatch(Action action)
        {
            if (window.Dispatcher.CheckAccess()) action();
            else window.Dispatcher.Invoke(action);
        }

        /// <summary>
        /// Makes sure the item being filled in is visible and that the window
        /// reflects the fact that the answer is on its way.
        /// </summary>
        private void BeginStreaming()
        {
            if (window.Status != AiHelperStatus.Streaming)
            {
                // The very first portion has arrived: from now on the answer
                // itself shows that the work is in progress.
                window.Status = AiHelperStatus.Streaming;
                window.StatusText = null;
            }

            // The item is reused across the iterations of the tool loop, so
            // whether it has to be added is a question about the transcript
            // rather than about the status (which goes back to "busy" while
            // the data requested by the model is being retrieved).
            window.AddToTranscript(response);
        }

        /// <inheritdoc/>
        public void OnThinking(string text) => Dispatch(() =>
        {
            BeginStreaming();

            // Keep the reasoning unfolded while it is being produced.
            response.IsReasoningExpanded = true;
            response.AppendThinking(text);
            window.ScrollIfPinned();
        });

        /// <inheritdoc/>
        public void OnContent(string text) => Dispatch(() =>
        {
            BeginStreaming();

            // The answer has started, so the reasoning is no longer the
            // center of attention and can be folded away.
            response.IsReasoningExpanded = false;
            response.AppendContent(text);
            window.ScrollIfPinned();
        });

        /// <inheritdoc/>
        public void OnThinkingBreak() => Dispatch(response.BreakThinking);

        /// <inheritdoc/>
        public void OnToolCallStarted() => Dispatch(() =>
        {
            window.Status = AiHelperStatus.Busy;
            window.StatusText = "Retrieving the market data requested by the model...";
        });

        /// <inheritdoc/>
        public void OnToolCallCompleted(string summary, bool success) => Dispatch(() =>
        {
            response.AddToolCall(summary, success);
            window.ScrollIfPinned();

            window.StatusText = $"Waiting for \"{model}\"...";
        });
    }

    /// <summary>
    /// Handles the "click-event" of the "terminate" button.
    /// </summary>
    private void Terminate_OnClick(object sender, RoutedEventArgs e)
    {
        StatusText = "Terminating...";

        // Read once: the field is cleared by the "sending" method, so testing
        // it and using it separately could well be done on different objects.
        var cts = _requestCts;

        if (cts == null) return;

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The request has finished on its own in the meantime.
        }
    }

    /// <summary>
    /// Handles the "click-event" of the "clear" button.
    /// </summary>
    /// <remarks>
    /// The conversation retains everything the model has ever requested, which
    /// is what makes the follow-up questions work but also makes it grow without
    /// bound. This is the only way to reclaim the context, so it is deliberately
    /// a full reset rather than a partial cleanup.
    /// </remarks>
    private void ClearContext_OnClick(object sender, RoutedEventArgs e)
    {
        if (Status is AiHelperStatus.Busy or AiHelperStatus.Streaming) return;

        if (Transcript.Count == 0) return;

        if (MessageBox.Show(this, "Clear the conversation and everything the model remembers of it?",
                "AI helper", MessageBoxButton.OKCancel, MessageBoxImage.Question,
                MessageBoxResult.Cancel) != MessageBoxResult.OK) return;

        Transcript.Clear();

        _history.Clear();
        _history.Add(new ChatMessage(ChatRoles.System, SystemPrompt));

        OnPropertyChanged(nameof(HasTranscript));
    }

    /// <summary>
    /// Indicates that there is something to clear.
    /// </summary>
    public bool HasTranscript => Transcript.Count > 0;

    /// <summary>
    /// Appends an item to the visible transcript and scrolls to it.
    /// </summary>
    private void AddToTranscript(string title, string content, bool isRequest) =>
        AddToTranscript(new ChatMessageVm(title, content, isRequest));

    /// <summary>
    /// Appends the given item to the visible transcript and scrolls to it.
    /// </summary>
    private void AddToTranscript(ChatMessageVm item)
    {
        if (Transcript.Contains(item))
            return;

        Transcript.Add(item);
        OnPropertyChanged(nameof(HasTranscript));
        TranscriptScroll.ScrollToEnd();
    }

    /// <summary>
    /// Scrolls the transcript to its end, but only if it is already scrolled
    /// there: doing it unconditionally would fight the user who has scrolled
    /// back to read something while the answer is still being generated.
    /// </summary>
    private void ScrollIfPinned()
    {
        const double tolerance = 2.0;

        if (TranscriptScroll.VerticalOffset >=
            TranscriptScroll.ScrollableHeight - tolerance)
            TranscriptScroll.ScrollToEnd();
    }

    /// <summary>
    /// Copies the corresponding model response to the clipboard.
    /// </summary>
    /// <remarks>
    /// The "markdown" source is copied rather than the rendered text, so that
    /// the formatting survives being pasted into any tool that understands it.
    /// </remarks>
    private void CopyResponse_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ChatMessageVm response })
            return;

        if (string.IsNullOrEmpty(response.Content)) return;

        try
        {
            Clipboard.SetText(response.Content);
        }
        catch (Exception exception)
        {
            // The clipboard can be locked by another application.
            MessageBox.Show(this, $"Failed to copy the response: {exception.Message}",
                "BAnalyzer", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Saves the corresponding model response to a text file.
    /// </summary>
    private void SaveResponse_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ChatMessageVm response })
            return;

        var dialog = new SaveFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".txt",
            FileName = BuildFileName(response)
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            File.WriteAllText(dialog.FileName, BuildFileContent(response));
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"Failed to save the response: {exception.Message}",
                "BAnalyzer", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Returns a default name of the file to save the given <paramref name="response"/> to.
    /// </summary>
    private static string BuildFileName(ChatMessageVm response)
    {
        var model = new string(response.Model
            .Select(x => char.IsLetterOrDigit(x) ? x : '_').ToArray());

        return $"BAnalyzer_{model}_{response.TimeStamp:yyyyMMdd_HHmmss}.txt";
    }

    /// <summary>
    /// Returns the text to be written to the file for the given <paramref name="response"/>.
    /// </summary>
    private static string BuildFileContent(ChatMessageVm response)
    {
        var builder = new StringBuilder();

        builder.AppendLine("BAnalyzer AI helper response");
        builder.AppendLine($"Model: {response.Model}");
        builder.AppendLine($"Saved: {response.TimeStamp:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine("Question:");
        builder.AppendLine(response.Question);
        builder.AppendLine(new string('-', 60));
        builder.AppendLine(response.Content);

        return builder.ToString();
    }

    /// <summary>
    /// Handles window header-related actions.
    /// </summary>
    private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
        else
            DragMove();
    }

    /// <summary>
    /// Handles the "click-event" of the "close" button.
    /// </summary>
    private void Close_OnClick(object sender, RoutedEventArgs e) => Hide();

    /// <inheritdoc/>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Property changed notification.
    /// </summary>
    private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
