//Copyright (c) 2024 Denys Dragunov, dragunovdenis@gmail.com
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
using System.Runtime.Serialization;

namespace BAnalyzer.DataStructures;

/// <summary>
/// Readonly interface for the data structure below.
/// </summary>
public interface IApplicationSettings : INotifyPropertyChanged
{
    /// <summary>
    /// Collection of settings for each exchange control.
    /// </summary>
    Dictionary<string, ExchangeSettings> ExchangeSettings { get; }

    /// <summary>
    /// Dark mode toggle.
    /// </summary>
    bool DarkMode { get; }

    /// <summary>
    /// View synchronization toggle.
    /// </summary>
    bool ControlSynchronization { get; }
}

/// <summary>
/// Application level settings.
/// </summary>
[DataContract]
public class ApplicationSettings : IApplicationSettings
{
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    [DataMember]
    public Dictionary<string, ExchangeSettings> ExchangeSettings { get; private set; } = new();

    [DataMember]
    private bool _darkMode = true;

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public bool DarkMode
    {
        get => _darkMode;
        set => SetField(ref _darkMode, value);
    }

    [DataMember]
    private bool _synchronization = true;

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public bool ControlSynchronization
    {
        get => _synchronization;
        set =>SetField(ref _synchronization, value);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Sets field with property-changed notification.
    /// </summary>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}