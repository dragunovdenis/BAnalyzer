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
using System.Windows.Threading;

namespace BAnalyzer.Utils;

/// <summary>
/// View model of the choice of the recording device: the devices to choose
/// from and the chosen one, which defaults to (in this order) the one chosen
/// last time, the default device of Windows or the first one available.
/// </summary>
/// <remarks>
/// To be used on the thread of the user interface.
/// </remarks>
/// <param name="loadPreferredName">Returns the name of the device chosen last time.</param>
/// <param name="savePreferredName">Stores the name of the device to be preferred next time.</param>
public sealed class MicrophoneSelection(Func<string> loadPreferredName, Action<string> savePreferredName)
    : INotifyPropertyChanged, IDisposable
{
    /// <summary>
    /// While there are no devices, the system is re-checked this often (one could get plugged in).
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    private DispatcherTimer _pollTimer;
    private bool _disposed;
    private bool _refreshing;

    /// <summary>
    /// The devices to choose from.
    /// </summary>
    public ObservableCollection<MicrophoneDevice> Devices { get; } = [];

    /// <summary>
    /// The chosen device ("null" only if there are no devices). A device chosen
    /// from outside (by the user) becomes the preferred one, i.e. the one chosen
    /// by default next time.
    /// </summary>
    public MicrophoneDevice Selected
    {
        get;
        set
        {
            if (field == value) return;

            field = value;
            OnPropertyChanged();

            if (!_refreshing && value != null)
                savePreferredName?.Invoke(value.Name);
        }
    }

    /// <summary>
    /// Indicates that there is at least one device.
    /// </summary>
    public bool IsAvailable
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
    /// Re-reads the devices of the system, keeping the chosen one if it is still there.
    /// </summary>
    public void Refresh()
    {
        var selectedName = Selected?.Name;
        var preferredName = loadPreferredName?.Invoke();
        var defaultNumber = MicrophoneDevice.GetDefaultNumber();

        // Neither the selection a bound list drops while being cleared nor
        // a fallback chosen in place of a missing device is the user's choice.
        _refreshing = true;

        try
        {
            Devices.Clear();

            foreach (var device in MicrophoneDevice.GetAll())
                Devices.Add(device);

            Selected = Devices.FirstOrDefault(x => x.Name == selectedName) ??
                       Devices.FirstOrDefault(x => x.Name == preferredName) ??
                       Devices.FirstOrDefault(x => x.Number == defaultNumber) ??
                       Devices.FirstOrDefault();
        }
        finally
        {
            _refreshing = false;
        }

        IsAvailable = Devices.Count > 0;
        UpdatePolling();
    }

    /// <summary>
    /// Re-reads the devices of the system if they have changed since the last
    /// time (the numbers of the devices shift when some get plugged in or out)
    /// and returns the chosen one.
    /// </summary>
    public MicrophoneDevice RefreshIfChanged()
    {
        if (!MicrophoneDevice.GetAll().SequenceEqual(Devices))
            Refresh();

        return Selected;
    }

    /// <summary>
    /// Keeps re-checking the system while there are no devices.
    /// </summary>
    private void UpdatePolling()
    {
        if (IsAvailable || _disposed)
        {
            _pollTimer?.Stop();
            return;
        }

        if (_pollTimer == null)
        {
            _pollTimer = new DispatcherTimer { Interval = PollInterval };
            _pollTimer.Tick += (_, _) => Refresh();
        }

        _pollTimer.Start();
    }

    /// <summary>
    /// Stops re-checking the system (a running timer would keep the instance,
    /// and everything listening to it, alive).
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
        _pollTimer?.Stop();
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Property changed notification.
    /// </summary>
    private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
