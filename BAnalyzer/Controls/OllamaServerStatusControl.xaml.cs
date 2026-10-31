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

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using BAnalyzerCore.Ollama;

namespace BAnalyzer.Controls;

/// <summary>
/// View model of a model that is currently loaded by the Ollama service, as
/// it is shown in the server status panel.
/// </summary>
/// <param name="Name">Name of the model.</param>
/// <param name="Role">What the model is used for in the AI helper ("chat", "voice" or "other").</param>
/// <param name="Size">Memory the model occupies.</param>
/// <param name="Gpu">Part of the memory that resides on the GPU.</param>
/// <param name="Cpu">Part of the memory that resides in the main memory (and is run by the CPU).</param>
/// <param name="KeepAlive">How long the model is going to stay loaded.</param>
public sealed record LoadedModelVm(string Name, string Role, string Size, string Gpu, string Cpu, string KeepAlive)
{
    /// <summary>
    /// Creates the view model of the given loaded <paramref name="model"/>.
    /// </summary>
    /// <param name="model">The loaded model.</param>
    /// <param name="chatModel">Name of the model the questions are answered with.</param>
    /// <param name="voiceModel">Name of the model the questions asked by voice are transcribed with.</param>
    public static LoadedModelVm Create(RunningModel model, string chatModel, string voiceModel)
    {
        var isChat = model.Name == chatModel;
        var isVoice = model.Name == voiceModel;
        var role = isChat && isVoice ? "chat + voice" : isChat ? "chat" : isVoice ? "voice" : "other";

        var gpuShare = model.Size > 0 ? $" ({100.0 * model.SizeVram / model.Size:0}%)" : string.Empty;

        return new LoadedModelVm(model.Name, role, FormatSize(model.Size),
            FormatSize(model.SizeVram) + gpuShare, FormatSize(model.SizeCpu), DescribeKeepAlive(model));
    }

    /// <summary>
    /// Describes how long the given <paramref name="model"/> is going to stay loaded.
    /// </summary>
    private static string DescribeKeepAlive(RunningModel model)
    {
        if (model.IsKeptLoaded)
            return "until unloaded";

        if (model.ExpiresAt is not { } expiry)
            return "-";

        var left = expiry - DateTimeOffset.Now;

        return left <= TimeSpan.Zero ? "expiring" :
            left < TimeSpan.FromMinutes(1) ? "< 1 min" : $"{Math.Ceiling(left.TotalMinutes):0} min";
    }

    /// <summary>
    /// Formats the given amount of memory (in bytes) for display.
    /// </summary>
    public static string FormatSize(long bytes) => $"{bytes / (double)(1L << 30):0.0} GB";
}

/// <summary>
/// Collapsible panel that shows what the Ollama server is up to: which models
/// it has loaded and how much GPU / main memory each of them occupies.
/// </summary>
/// <remarks>
/// The status is refreshed periodically while the panel is visible, and on
/// demand via <see cref="RefreshAsync"/>.
/// </remarks>
public partial class OllamaServerStatusControl : INotifyPropertyChanged
{
    /// <summary>
    /// How often the status is refreshed.
    /// </summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(3);

    private readonly DispatcherTimer _timer;
    private bool _refreshing;

    /// <summary>
    /// Constructor.
    /// </summary>
    public OllamaServerStatusControl()
    {
        _timer = new DispatcherTimer { Interval = RefreshInterval };
        _timer.Tick += async (_, _) => await RefreshAsync().ConfigureAwait(true);

        InitializeComponent();

        IsVisibleChanged += async (_, _) =>
        {
            if (!IsVisible)
            {
                _timer.Stop();
                return;
            }

            _timer.Start();
            await RefreshAsync().ConfigureAwait(true);
        };
    }

    /// <summary>
    /// The service whose status is displayed.
    /// </summary>
    public IOllamaClient Client { get; set; }

    public static readonly DependencyProperty ChatModelProperty =
        DependencyProperty.Register(nameof(ChatModel), typeof(string), typeof(OllamaServerStatusControl));

    /// <summary>
    /// Name of the model the questions are answered with.
    /// </summary>
    public string ChatModel
    {
        get => (string)GetValue(ChatModelProperty);
        set => SetValue(ChatModelProperty, value);
    }

    public static readonly DependencyProperty VoiceModelProperty =
        DependencyProperty.Register(nameof(VoiceModel), typeof(string), typeof(OllamaServerStatusControl));

    /// <summary>
    /// Name of the model the questions asked by voice are transcribed with ("null" if none).
    /// </summary>
    public string VoiceModel
    {
        get => (string)GetValue(VoiceModelProperty);
        set => SetValue(VoiceModelProperty, value);
    }

    /// <summary>
    /// The models currently loaded by the server.
    /// </summary>
    public ObservableCollection<LoadedModelVm> LoadedModels { get; } = [];

    /// <summary>
    /// Indicates that there is at least one model loaded by the server.
    /// </summary>
    public bool HasLoadedModels => LoadedModels.Count > 0;

    /// <summary>
    /// One-line summary of the status of the server.
    /// </summary>
    public string Summary
    {
        get;
        private set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();
        }
    } = "Ollama server: checking...";

    /// <summary>
    /// Re-reads the models the server has loaded (unless the panel is hidden).
    /// </summary>
    public async Task RefreshAsync()
    {
        if (_refreshing || Client == null || !IsVisible) return;

        _refreshing = true;

        try
        {
            var result = await Client.GetRunningModelsAsync(CancellationToken.None).ConfigureAwait(true);

            if (!result.Available)
            {
                Summary = "Ollama server: not reachable";
                UpdateLoadedModels([]);
                return;
            }

            var models = result.Models;
            UpdateLoadedModels(models.Select(x => LoadedModelVm.Create(x, ChatModel, VoiceModel)).ToList());

            Summary = models.Count == 0
                ? "Ollama server: no models loaded"
                : $"Ollama server: {models.Count} model{(models.Count == 1 ? "" : "s")} loaded · " +
                  $"GPU {LoadedModelVm.FormatSize(models.Sum(x => x.SizeVram))} · " +
                  $"CPU {LoadedModelVm.FormatSize(models.Sum(x => x.SizeCpu))}";
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>
    /// Replaces the content of <see cref="LoadedModels"/> with the given
    /// <paramref name="models"/> (unless it is the same already).
    /// </summary>
    private void UpdateLoadedModels(IReadOnlyList<LoadedModelVm> models)
    {
        if (LoadedModels.SequenceEqual(models)) return;

        LoadedModels.Clear();

        foreach (var model in models)
            LoadedModels.Add(model);

        OnPropertyChanged(nameof(HasLoadedModels));
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Property changed notification.
    /// </summary>
    private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
