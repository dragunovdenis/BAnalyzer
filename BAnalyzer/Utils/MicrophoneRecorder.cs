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

using System.IO;
using NAudio.Wave;

namespace BAnalyzer.Utils;

/// <summary>
/// Records a microphone of the PC into an in-memory WAV file
/// (16 kHz, 16 bit, mono - the format the speech models are trained on)
/// and keeps track of the sound level.
/// </summary>
/// <remarks>
/// One recording at a time: <see cref="Start"/>, then <see cref="StopAsync"/>.
/// </remarks>
public sealed class MicrophoneRecorder : IDisposable
{
    /// <summary>
    /// Format of the recordings.
    /// </summary>
    private static readonly WaveFormat Format = new(16000, 16, 1);

    private WaveIn _waveIn;
    private WaveFileWriter _writer;
    private MemoryStream _buffer;
    private TaskCompletionSource<byte[]> _stopped;

    private readonly Lock _levelLock = new();
    private float _recentPeak;
    private float _maxPeak;

    /// <summary>
    /// Indicates that a recording is in progress.
    /// </summary>
    public bool IsRecording => _waveIn != null;

    /// <summary>
    /// The highest peak level (0..1) of the current (or the last) recording.
    /// </summary>
    public float MaxPeak
    {
        get
        {
            lock (_levelLock)
                return _maxPeak;
        }
    }

    /// <summary>
    /// Returns the peak level (0..1) of the audio recorded since the previous call.
    /// </summary>
    public float TakeRecentPeak()
    {
        lock (_levelLock)
        {
            var result = _recentPeak;
            _recentPeak = 0;
            return result;
        }
    }

    /// <summary>
    /// Starts recording the given device.
    /// </summary>
    /// <param name="deviceNumber">WinMM number of the device (see <see cref="MicrophoneDevice"/>).</param>
    /// <exception cref="NAudio.MmException">
    /// The device could not be opened (for example, because the access to
    /// the microphone is blocked by the privacy settings of Windows).
    /// </exception>
    public void Start(int deviceNumber)
    {
        if (IsRecording)
            throw new InvalidOperationException("The recording is already in progress.");

        lock (_levelLock)
            _recentPeak = _maxPeak = 0;

        _buffer = new MemoryStream();
        _writer = new WaveFileWriter(_buffer, Format);
        _stopped = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        _waveIn = new WaveIn { DeviceNumber = deviceNumber, WaveFormat = Format, BufferMilliseconds = 50 };
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;

        try
        {
            _waveIn.StartRecording();
        }
        catch
        {
            Cleanup();
            throw;
        }
    }

    /// <summary>
    /// Stops the recording and returns the complete WAV file.
    /// </summary>
    /// <remarks>
    /// If the recording has already been stopped by the system (for example,
    /// because the device has been unplugged), the exception it has ended
    /// with is re-thrown.
    /// </remarks>
    public async Task<byte[]> StopAsync()
    {
        var completion = _stopped ?? throw new InvalidOperationException("No recording has been started.");

        if (_waveIn != null)
        {
            try
            {
                _waveIn.StopRecording();
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
                Cleanup();
            }
        }

        try
        {
            return await completion.Task.ConfigureAwait(true);
        }
        finally
        {
            if (_stopped == completion)
                _stopped = null;
        }
    }

    /// <summary>
    /// Handles a portion of the recorded audio (called on a background thread).
    /// </summary>
    private void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        _writer?.Write(e.Buffer, 0, e.BytesRecorded);

        var peak = 0;

        for (var i = 0; i + 1 < e.BytesRecorded; i += 2)
            peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(e.Buffer, i)));

        var level = peak / 32768f;

        lock (_levelLock)
        {
            _recentPeak = Math.Max(_recentPeak, level);
            _maxPeak = Math.Max(_maxPeak, level);
        }
    }

    /// <summary>
    /// Finalizes the recording.
    /// </summary>
    private void OnRecordingStopped(object sender, StoppedEventArgs e)
    {
        var completion = _stopped;

        try
        {
            // Disposing the writer is what fills in the sizes in the RIFF header. It disposes
            // the stream as well, but the content of a disposed memory stream is still readable.
            _writer?.Dispose();
            _writer = null;

            var audio = _buffer?.ToArray() ?? [];

            if (e.Exception != null)
                completion?.TrySetException(e.Exception);
            else
                completion?.TrySetResult(audio);
        }
        catch (Exception ex)
        {
            completion?.TrySetException(ex);
        }
        finally
        {
            Cleanup();
        }
    }

    /// <summary>
    /// Releases the resources of the current recording.
    /// </summary>
    private void Cleanup()
    {
        var waveIn = _waveIn;
        _waveIn = null;

        if (waveIn != null)
        {
            waveIn.DataAvailable -= OnDataAvailable;
            waveIn.RecordingStopped -= OnRecordingStopped;

            try
            {
                waveIn.Dispose();
            }
            catch (Exception)
            {
                // A failed device may fail to close as well; there is nothing to be done about it.
            }
        }

        _writer?.Dispose();
        _writer = null;
        _buffer?.Dispose();
        _buffer = null;
    }

    /// <summary>
    /// Disposes the instance (an ongoing recording is abandoned).
    /// </summary>
    public void Dispose()
    {
        _stopped?.TrySetCanceled();
        Cleanup();
    }
}
