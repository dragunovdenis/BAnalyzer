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
using Binance.Net.Enums;

namespace BAnalyzer.DataStructures;

/// <summary>
/// Readonly interface for the corresponding data struct below.
/// </summary>
public interface IExchangeSettings : INotifyPropertyChanged
{
    /// <summary>
    /// Descriptor of the crypto-currency pair.
    /// </summary>
    string ExchangeDescriptor { get; }

    /// <summary>
    /// Time discretization to use when displaying charts.
    /// </summary>
    KlineInterval TimeDiscretization { get; }

    /// <summary>
    /// Current analysis indicator to visualize.
    /// </summary>
    AnalysisIndicatorType CurrentAnalysisIndicator { get; }

    /// <summary>
    /// Size of the main analysis window to use.
    /// </summary>
    int MainAnalysisWindow { get; }

    /// <summary>
    /// Number of sticks to display on the chart.
    /// </summary>
    int StickRange { get; }

    /// <summary>
    /// Determines whether the focus time marker should be shown.
    /// </summary>
    bool ShowFocusTimeMarker { get; }

    /// <summary>
    /// Assigns current instance with the given one.
    /// </summary>
    void Assign(IExchangeSettings source, bool excludeExchangeDescriptor = false);
}

/// <summary>
/// Setting of the crypto exchange control.
/// </summary>
[DataContract]
public class ExchangeSettings : IExchangeSettings
{
    [DataMember]
    private string _exchangeDescriptor;

    /// <inheritdoc/>
    public string ExchangeDescriptor
    {
        get => _exchangeDescriptor;
        set => SetField(ref _exchangeDescriptor, value);
    }

    [DataMember]
    private KlineInterval _timeDiscretization;

    /// <inheritdoc/>
    public KlineInterval TimeDiscretization
    {
        get => _timeDiscretization;
        set => SetField(ref _timeDiscretization, value);
    }

    [DataMember]
    private AnalysisIndicatorType _currentAnalysisIndicator;

    /// <inheritdoc/>
    public AnalysisIndicatorType CurrentAnalysisIndicator
    {
        get => _currentAnalysisIndicator;
        set => SetField(ref _currentAnalysisIndicator, value);
    }

    [DataMember]
    private int _mainAnalysisWindow;

    /// <inheritdoc/>
    public int MainAnalysisWindow
    {
        get => _mainAnalysisWindow;
        set => SetField(ref _mainAnalysisWindow, value);
    }

    [DataMember]
    private int _stickRange;

    /// <inheritdoc/>
    public int StickRange
    {
        get => _stickRange;
        set => SetField(ref _stickRange, value);
    }

    [DataMember]
    private bool _showFocusTimeMarker = true;

    /// <summary>
    /// Determines whether the focus time marker should be shown.
    /// </summary>
    public bool ShowFocusTimeMarker
    {
        get => _showFocusTimeMarker;
        set => SetField(ref _showFocusTimeMarker, value);
    }

    /// <inheritdoc/>
    public void Assign(IExchangeSettings source, bool excludeExchangeDescriptor = false)
    {
        if (this == source) return;

        if (!excludeExchangeDescriptor)
            ExchangeDescriptor = source.ExchangeDescriptor;

        TimeDiscretization = source.TimeDiscretization;
        CurrentAnalysisIndicator = source.CurrentAnalysisIndicator;
        MainAnalysisWindow = source.MainAnalysisWindow;
        StickRange = source.StickRange;
        ShowFocusTimeMarker = source.ShowFocusTimeMarker;
    }

    /// <summary>
    /// Event to listen to the changes in the properties of the data struct.
    /// </summary>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Property changed handler.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Field setter.
    /// </summary>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}