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

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace BAnalyzer.Utils;

/// <summary>
/// How a recording of a spoken question has ended up.
/// </summary>
public enum SpeechRecordingOutcome
{
    /// <summary>The recording is fit to be transcribed.</summary>
    Recorded,

    /// <summary>The recording has been thrown away on request.</summary>
    Discarded,

    /// <summary>The recording is too short to be anything but an accidental click.</summary>
    TooShort,

    /// <summary>Nothing but silence has been recorded.</summary>
    Silent,

    /// <summary>The device has failed while recording.</summary>
    Failed
}

/// <summary>
/// Result of <see cref="SpeechRecorder.StopAsync"/>.
/// </summary>
/// <param name="Outcome">How the recording has ended up.</param>
/// <param name="Audio">The recorded WAV file (only if <paramref name="Outcome"/> is "recorded").</param>
/// <param name="Error">Description of the failure (only if <paramref name="Outcome"/> is "failed").</param>
public sealed record SpeechRecording(SpeechRecordingOutcome Outcome, byte[] Audio = null, string Error = null);

/// <summary>
/// Records the spoken questions: on top of <see cref="MicrophoneRecorder"/>
/// it tracks the duration and the sound level (for display) and tells the
/// recordings worth transcribing from the accidental or silent ones.
/// </summary>
/// <remarks>
/// To be used on the thread of the user interface.
/// </remarks>
public sealed class SpeechRecorder : INotifyPropertyChanged, IDisposable
{
    /// <summary>
    /// Recordings shorter than this are most likely accidental clicks.
    /// </summary>
    private static readonly TimeSpan MinDuration = TimeSpan.FromSeconds(0.4);

    /// <summary>
    /// Recordings are not supposed to get longer than this (see <see cref="MaxDurationReached"/>).
    /// </summary>
    public static readonly TimeSpan MaxDuration = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Recordings whose peak level stays below this (-40 dBFS) contain no audible speech.
    /// </summary>
    private const float SilencePeakLevel = 0.01f;

    /// <summary>
    /// Lower end (in dBFS) of the <see cref="Level"/> scale.
    /// </summary>
    private const double LevelFloorDb = -60;

    /// <summary>
    /// Fraction of the <see cref="Level"/> retained per tick when the sound gets quieter.
    /// </summary>
    private const double LevelRelease = 0.85;

    private readonly MicrophoneRecorder _recorder = new();
    private readonly DispatcherTimer _timer;
    private DateTime _started;
    private bool _isRecording;

    /// <summary>
    /// Constructor.
    /// </summary>
    public SpeechRecorder()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += Timer_OnTick;
    }

    /// <summary>
    /// Duration of the ongoing recording.
    /// </summary>
    public TimeSpan Elapsed
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
    /// Sound level (0..1, logarithmic scale) of the ongoing recording.
    /// </summary>
    public double Level
    {
        get;
        private set
        {
            if (field.Equals(value)) return;

            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Raised when the ongoing recording reaches <see cref="MaxDuration"/>
    /// (the recording goes on until it is stopped).
    /// </summary>
    public event EventHandler MaxDurationReached;

    /// <summary>
    /// Starts recording the given device.
    /// </summary>
    /// <param name="deviceNumber">WinMM number of the device (see <see cref="MicrophoneDevice"/>).</param>
    /// <exception cref="NAudio.MmException">The device could not be opened.</exception>
    public void Start(int deviceNumber)
    {
        if (_isRecording)
            throw new InvalidOperationException("The recording is already in progress.");

        _recorder.Start(deviceNumber);

        _isRecording = true;
        _started = DateTime.UtcNow;
        Elapsed = TimeSpan.Zero;
        Level = 0;
        _timer.Start();
    }

    /// <summary>
    /// Stops the ongoing recording and tells whether it is worth transcribing.
    /// </summary>
    /// <param name="discard">Indicates that the recording is to be thrown away.</param>
    public async Task<SpeechRecording> StopAsync(bool discard = false)
    {
        if (!_isRecording)
            return new SpeechRecording(SpeechRecordingOutcome.Discarded);

        _isRecording = false;
        _timer.Stop();
        Level = 0;

        var duration = DateTime.UtcNow - _started;
        byte[] audio;

        try
        {
            audio = await _recorder.StopAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            return new SpeechRecording(SpeechRecordingOutcome.Failed, Error: exception.Message);
        }

        if (discard)
            return new SpeechRecording(SpeechRecordingOutcome.Discarded);

        if (duration < MinDuration)
            return new SpeechRecording(SpeechRecordingOutcome.TooShort);

        return _recorder.MaxPeak < SilencePeakLevel
            ? new SpeechRecording(SpeechRecordingOutcome.Silent)
            : new SpeechRecording(SpeechRecordingOutcome.Recorded, audio);
    }

    /// <summary>
    /// Updates the duration and the sound level of the ongoing recording.
    /// </summary>
    private void Timer_OnTick(object sender, EventArgs e)
    {
        Elapsed = DateTime.UtcNow - _started;

        // Fast attack, slow release: the level follows the speech without flickering.
        Level = Math.Max(ToLevel(_recorder.TakeRecentPeak()), Level * LevelRelease);

        if (Elapsed >= MaxDuration)
            MaxDurationReached?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Converts the given peak level (0..1) into a position (0..1) on the logarithmic <see cref="Level"/> scale.
    /// </summary>
    private static double ToLevel(float peak) =>
        peak <= 0 ? 0 : Math.Clamp((20 * Math.Log10(peak) - LevelFloorDb) / -LevelFloorDb, 0, 1);

    /// <summary>
    /// Disposes the instance (an ongoing recording is abandoned).
    /// </summary>
    public void Dispose()
    {
        _timer.Stop();
        _isRecording = false;
        _recorder.Dispose();
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Property changed notification.
    /// </summary>
    private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
