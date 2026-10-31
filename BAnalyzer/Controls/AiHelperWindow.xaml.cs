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

using BAnalyzer.Controllers;
using BAnalyzer.DataStructures;
using BAnalyzer.Utils;
using Microsoft.Win32;
using NAudio;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using BAnalyzerCore.Ollama;

namespace BAnalyzer.Controls;

/// <summary>
/// Display wrapper (view model) of <see cref="ModelInfo"/> that lets the context window be
/// filled in once it is fetched (which takes a separate, slower request),
/// without disturbing the rest of the properties or the selection in the
/// drop-down list the instances are displayed in.
/// </summary>
public class ModelOptionVm(ModelInfo model)
{
    /// <summary>
    /// Name of the option that stands for "no model selected".
    /// </summary>
    private const string NoneName = "(none)";

    /// <summary>
    /// Creates the option that stands for "no model selected".
    /// </summary>
    public static ModelOptionVm CreateNone() => new(new ModelInfo(NoneName, null, null, null, -1))
    {
        IsNone = true
    };

    /// <summary>
    /// Indicates that the option stands for "no model selected".
    /// </summary>
    public bool IsNone { get; private init; }

    /// <summary>
    /// Name of the model (the identifier accepted by the "/api/chat" end-point).
    /// </summary>
    public string Name { get; } = model.Name;

    /// <summary>
    /// Human-readable summary of the model's size, parameter count,
    /// quantization level and context window, suitable for display next to
    /// the model name. Empty if none of the details are available.
    /// </summary>
    public string Description => IsNone ? null : model.Description;
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
    }

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
    Streaming,
    Recording,
    Transcribing
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
        "You are supposed to answer questions asked by users. To build your answers use the relevant " +
        "market data context provided by the available tools. When market data context is provided, " +
        "base your answers on it and clearly state the reasoning you used to arrive at those answers. " +
        "Base your reasoning, hypothesis and conclusions on a thorough statistical analysis of " +
        "the relevant market data context provided by relevant tools. Whenever you come up with a " +
        "hypothesis on how the market behaves (including forecasts on how market may behave in future), " +
        "explain your reasoning and give a statistical estimate of the reliability of your conclusions. " +
        "In case answering a question requires an extra market data which can't be retrieved via the " +
        "available set of tools, clearly state to the user what exact piece of data is missing. " +
        "You are not a financial advisor; do not give investment advice.";

    private readonly AiAgent _agent;

    /// <summary>
    /// Constructor.
    /// </summary>
    public AiHelperWindow(IOllamaClient ollamaClient, IMultiExchange exchange)
    {
        if (ollamaClient == null)
            throw new ArgumentNullException(nameof(ollamaClient));

        if (exchange == null)
            throw new ArgumentNullException(nameof(exchange));

        var client = exchange[ExchangeId.Binance];
        var toolExecutor = new ToolRegistry(new CandleTool(client),
            new OrderBookTool(client), new MarketStatsTool(client));

        _agent = new AiAgent(ollamaClient, toolExecutor, SystemPrompt, new StatusObserver(this));

        Microphones = new MicrophoneSelection(
            () => ApplicationController.Instance.ApplicationSettings.AiHelperMicrophone,
            name =>
            {
                if (ApplicationController.Instance.ApplicationSettings is ApplicationSettings settings)
                    settings.AiHelperMicrophone = name;
            });
        Microphones.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(MicrophoneSelection.IsAvailable)) return;

            OnPropertyChanged(nameof(CanUseMicrophone));
            OnPropertyChanged(nameof(MicrophoneToolTip));
        };

        Speech.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SpeechRecorder.Elapsed) && IsRecording)
                UpdateRecordingStatus();
        };
        Speech.MaxDurationReached += async (_, _) => await StopRecordingAsync().ConfigureAwait(true);

        InitializeComponent();
    }

    /// <summary>
    /// The visible part of the conversation.
    /// </summary>
    public ObservableCollection<ChatMessageVm> Transcript { get; } = [];

    /// <summary>
    /// Collection of the locally available models that can answer the questions
    /// (see <see cref="AiAgent.IsChatModel"/>).
    /// </summary>
    public ObservableCollection<ModelOptionVm> Models { get; } = [];

    /// <summary>
    /// The thinking levels the user can choose from.
    /// </summary>
    public IReadOnlyList<ThinkingLevel> ThinkingLevels { get; } = Enum.GetValues<ThinkingLevel>();

    /// <summary>
    /// The thinking level the questions are asked with.
    /// </summary>
    public ThinkingLevel SelectedThinkingLevel
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
        }
    } = ThinkingLevel.Medium;

    /// <summary>
    /// Collection of the locally available models that can convert speech
    /// into text (see <see cref="AiAgent.IsVoiceModel"/>), headed by the
    /// "none" option that switches the voice input off.
    /// </summary>
    public ObservableCollection<ModelOptionVm> VoiceModels { get; } = [];

    /// <summary>
    /// Indicates that there is at least one voice model to choose from.
    /// </summary>
    public bool HasVoiceModels => VoiceModels.Any(x => !x.IsNone);

    /// <summary>
    /// The model the questions asked by voice are transcribed with.
    /// </summary>
    public ModelOptionVm SelectedVoiceModel
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasVoiceModelSelected));
            OnPropertyChanged(nameof(ActiveVoiceModel));
        }
    }

    /// <summary>
    /// Indicates that the voice input is switched on.
    /// </summary>
    public bool HasVoiceModelSelected => SelectedVoiceModel is { IsNone: false };

    /// <summary>
    /// Name of the model the questions asked by voice are transcribed with
    /// ("null" if the voice input is switched off).
    /// </summary>
    public string ActiveVoiceModel => HasVoiceModelSelected ? SelectedVoiceModel.Name : null;

    /// <summary>
    /// The choice of the device the questions asked by voice are recorded with.
    /// </summary>
    public MicrophoneSelection Microphones { get; }

    /// <summary>
    /// Records the questions asked by voice.
    /// </summary>
    public SpeechRecorder Speech { get; } = new();

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
    public bool IsBusy => Status is AiHelperStatus.Checking or AiHelperStatus.Busy or AiHelperStatus.Transcribing;

    /// <summary>
    /// Indicates that the microphone is being recorded.
    /// </summary>
    public bool IsRecording => Status == AiHelperStatus.Recording;

    /// <summary>
    /// Indicates that the status panel (the description of the operation in
    /// progress) should be shown.
    /// </summary>
    public bool HasStatusPanel => IsBusy || IsRecording;

    /// <summary>
    /// Indicates that the microphone button can be used (it stays enabled
    /// while recording, so that it does not lose the mouse capture).
    /// </summary>
    public bool CanUseMicrophone => Microphones.IsAvailable &&
                                    Status is AiHelperStatus.Ready or AiHelperStatus.Recording;

    /// <summary>
    /// Tool tip of the microphone button.
    /// </summary>
    public string MicrophoneToolTip => Microphones.IsAvailable
        ? "Hold (or hold the space bar) and speak; release to convert the speech into text"
        : "No microphone has been found";

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
    public bool CanTerminate => Status is AiHelperStatus.Busy or AiHelperStatus.Streaming or AiHelperStatus.Transcribing;

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
            OnPropertyChanged(nameof(IsRecording));
            OnPropertyChanged(nameof(HasStatusPanel));
            OnPropertyChanged(nameof(CanUseMicrophone));
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
        BannerText = null;

        var result = await _agent.ConnectAsync(CancellationToken.None).ConfigureAwait(true);

        Models.Clear();
        VoiceModels.Clear();
        SelectedVoiceModel = null;
        OnPropertyChanged(nameof(HasVoiceModels));

        if (!result.Available)
        {
            Status = AiHelperStatus.Unavailable;
            BannerText = "Ollama service is not available. Please make sure that Ollama is installed " +
                         "and running (see https://ollama.com) in order to use the AI Helper." +
                         Environment.NewLine + result.Error;
            return;
        }

        if (result.ChatModels.Count == 0)
        {
            Status = AiHelperStatus.NoModels;
            BannerText = "No suitable models are available locally. The AI Helper needs a model " +
                         "capable of both calling tools and thinking; install one first, for example " +
                         "by running \"ollama pull qwen3\" in a terminal.";
            return;
        }

        foreach (var model in result.ChatModels)
            Models.Add(new ModelOptionVm(model));

        var none = ModelOptionVm.CreateNone();
        VoiceModels.Add(none);

        foreach (var model in result.VoiceModels)
            VoiceModels.Add(new ModelOptionVm(model));

        SelectedVoiceModel = VoiceModels.FirstOrDefault(x => !x.IsNone) ?? none;
        OnPropertyChanged(nameof(HasVoiceModels));
        Microphones.Refresh();

        SelectedModel = Models[0].Name;
        Status = AiHelperStatus.Ready;
        StatusText = null;
    }

    /// <summary>
    /// Refreshes the list of the recording devices (they could have been plugged in or out).
    /// </summary>
    private void MicrophoneSelector_OnDropDownOpened(object sender, EventArgs e) => Microphones.Refresh();

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
        BannerText = null;
        Status = AiHelperStatus.Busy;

        var model = SelectedModel;
        var thinking = SelectedThinkingLevel;

        try
        {
            var request = new ChatMessageVm("You", text, isRequest: true);
            AddToTranscript(request);

            var response = new ChatMessageVm(model, string.Empty, isRequest: false,
                model, text, DateTime.Now);

            var result = await _agent.SendAsync(model, text, thinking,
                new TranscriptObserver(this, response, model)).ConfigureAwait(true);

            if (result.ToolsUnavailable)
            {
                // Nothing has been asked of the model, so the message goes back
                // to the input box for the user to re-send once the market data
                // can be served again.
                RemoveFromTranscript(request);
                RestoreInput(text);

                AddToTranscript("Error", "No market data tools are available for this " +
                                         "conversation, so the question was not asked.",
                    isRequest: false);

                return;
            }

            if (!result.Success)
            {
                if (result.Cancelled)
                {
                    // Whatever has been generated so far is kept visible, but the
                    // conversation proceeds as if the turn never happened.
                    if (Transcript.Contains(response))
                        response.AppendContent(Environment.NewLine + "[terminated by the user]");
                    else
                        AddToTranscript("Terminated", "The request was terminated by the user.",
                            isRequest: false);

                    // Give the user a chance to edit and re-send the message.
                    RestoreInput(text);
                }
                else
                {
                    RemoveFromTranscript(response);

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
            StatusText = null;
            Status = AiHelperStatus.Ready;
        }
    }

    /// <summary>
    /// Puts the activities reported by <see cref="AiAgent"/> into words and
    /// displays them as <see cref="StatusText"/>.
    /// </summary>
    /// <remarks>
    /// The agent, being unaware of the user interface, reports from whatever
    /// thread the operation happens to be running on, so the update is
    /// marshalled here.
    /// </remarks>
    private sealed class StatusObserver(AiHelperWindow window) : IAiAgentObserver
    {
        /// <inheritdoc/>
        public void OnActivityChanged(AiAgentActivity activity, string model)
        {
            var text = activity switch
            {
                AiAgentActivity.Connecting => "Looking for the Ollama service...",
                AiAgentActivity.CheckingModelCapabilities => "Checking the capabilities of the model...",
                AiAgentActivity.WaitingForModel =>
                    $"Waiting for \"{model}\"... this may take a while for large models.",
                AiAgentActivity.Transcribing => $"Converting the speech into text with \"{model}\"...",
                _ => null
            };

            if (window.Dispatcher.CheckAccess()) window.StatusText = text;
            else window.Dispatcher.Invoke(() => window.StatusText = text);
        }
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
        _agent.Cancel();
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
        if (Status is AiHelperStatus.Busy or AiHelperStatus.Streaming
            or AiHelperStatus.Recording or AiHelperStatus.Transcribing) return;

        if (Transcript.Count == 0) return;

        if (MessageBox.Show(this, "Clear the conversation and everything the model remembers of it?",
                "AI helper", MessageBoxButton.OKCancel, MessageBoxImage.Question,
                MessageBoxResult.Cancel) != MessageBoxResult.OK) return;

        Transcript.Clear();

        _agent.ClearHistory();

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
    /// Removes the given item from the visible transcript, if it is there.
    /// </summary>
    private void RemoveFromTranscript(ChatMessageVm item)
    {
        if (!Transcript.Remove(item)) return;

        OnPropertyChanged(nameof(HasTranscript));
    }

    /// <summary>
    /// Puts the given message back into the input box, so that the user can
    /// edit and re-send it.
    /// </summary>
    private void RestoreInput(string text)
    {
        InputBox.Text = text;
        InputBox.CaretIndex = text.Length;
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
    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        // A recording nobody is going to release the button of is abandoned,
        // and so is the transcription of one.
        if (Status == AiHelperStatus.Recording)
            _ = StopRecordingAsync(discard: true);
        else if (Status == AiHelperStatus.Transcribing)
            _agent.Cancel();

        Hide();
    }

    /// <summary>
    /// Starts recording the question asked by voice.
    /// </summary>
    private void StartRecording()
    {
        if (Status != AiHelperStatus.Ready || !HasVoiceModelSelected || Microphones.Selected is not { } microphone)
            return;

        try
        {
            Speech.Start(microphone.Number);
        }
        catch (MmException)
        {
            BannerText = "The microphone could not be opened. Make sure that it is connected and that " +
                         "desktop applications are allowed to access it (Windows Settings > " +
                         "Privacy & security > Microphone).";
            return;
        }
        catch (Exception exception)
        {
            BannerText = $"Failed to start the recording: {exception.Message}";
            return;
        }

        Microphones.RememberSelected();
        BannerText = null;
        Status = AiHelperStatus.Recording;
        UpdateRecordingStatus();
    }

    /// <summary>
    /// Displays the duration of the ongoing recording.
    /// </summary>
    private void UpdateRecordingStatus() =>
        StatusText = $"Recording... {(int)Speech.Elapsed.TotalSeconds} s " +
                     $"(up to {(int)SpeechRecorder.MaxDuration.TotalSeconds} s) - " +
                     "release the button to convert the speech into text.";

    /// <summary>
    /// Stops the ongoing recording (if any), converts it into text and puts
    /// the text into the input box for the user to review and send.
    /// </summary>
    /// <param name="discard">Indicates that the recording is to be thrown away.</param>
    /// <remarks>
    /// Can be called several times for the same recording (for example, by
    /// both the "button up" and the "duration limit" events): only the first
    /// call does anything.
    /// </remarks>
    private async Task StopRecordingAsync(bool discard = false)
    {
        if (Status != AiHelperStatus.Recording) return;

        // Switching the status first is what makes the repeated calls harmless.
        Status = AiHelperStatus.Transcribing;
        StatusText = "Finishing the recording...";

        try
        {
            var recording = await Speech.StopAsync(discard).ConfigureAwait(true);

            BannerText = recording.Outcome switch
            {
                SpeechRecordingOutcome.Failed => $"Failed to record the microphone: {recording.Error}",
                SpeechRecordingOutcome.TooShort =>
                    "The recording is too short. Hold the microphone button down while speaking.",
                SpeechRecordingOutcome.Silent =>
                    $"Nothing but silence has been recorded from \"{Microphones.Selected?.Name}\". " +
                    "Make sure that it is the microphone you are speaking into (choose another one " +
                    "otherwise) and that it is not muted.",
                _ => null
            };

            if (recording.Outcome != SpeechRecordingOutcome.Recorded) return;

            var result = await _agent.TranscribeAsync(ActiveVoiceModel, recording.Audio).ConfigureAwait(true);

            if (result.Cancelled) return;

            if (!result.Success)
                BannerText = $"Failed to convert the speech into text: {result.Error}";
            else if (string.IsNullOrWhiteSpace(result.Text))
                BannerText = "No speech has been recognized. Please try again.";
            else
                InsertIntoInput(result.Text);
        }
        finally
        {
            StatusText = null;
            Status = AiHelperStatus.Ready;

            // The input box gets enabled together with the "ready" status, and
            // only then it can take the focus.
            InputBox.Focus();
        }
    }

    /// <summary>
    /// Inserts the given text at the caret of the input box, separating it
    /// with spaces from the text around it (if there is any).
    /// </summary>
    private void InsertIntoInput(string text)
    {
        var current = InputBox.Text ?? string.Empty;
        var caret = Math.Clamp(InputBox.CaretIndex, 0, current.Length);

        var prefix = caret > 0 && !char.IsWhiteSpace(current[caret - 1]) ? " " : string.Empty;
        var suffix = caret < current.Length && !char.IsWhiteSpace(current[caret]) ? " " : string.Empty;

        InputBox.Text = current.Insert(caret, prefix + text + suffix);
        InputBox.CaretIndex = caret + prefix.Length + text.Length;
    }

    /// <summary>
    /// Starts the recording when the microphone button is pressed.
    /// </summary>
    private void Mic_OnTalkStarted(object sender, EventArgs e) => StartRecording();

    /// <summary>
    /// Stops the recording when the microphone button is released.
    /// </summary>
    private async void Mic_OnTalkEnded(object sender, EventArgs e) =>
        await StopRecordingAsync().ConfigureAwait(true);

    /// <inheritdoc/>
    protected override void OnClosed(EventArgs e)
    {
        Speech.Dispose();
        base.OnClosed(e);
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Property changed notification.
    /// </summary>
    private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
