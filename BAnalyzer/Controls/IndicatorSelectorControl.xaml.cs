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

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using BAnalyzer.DataStructures;

namespace BAnalyzer.Controls;

/// <summary>
/// Interaction logic for IndicatorSelectorControl.xaml
/// </summary>
public partial class IndicatorSelectorControl : INotifyPropertyChanged
{
    /// <summary>
    /// Constructor.
    /// </summary>
    public IndicatorSelectorControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Collection of available indicators
    /// </summary>
    public ObservableCollection<AnalysisIndicatorType> AvailableIndicators =>
        new(Enum.GetValues(typeof(AnalysisIndicatorType)).Cast<AnalysisIndicatorType>());

    public static readonly DependencyProperty CurrentIndicatorTypeProperty =
        DependencyProperty.Register(nameof(CurrentIndicatorType), typeof(AnalysisIndicatorType),
            typeof(IndicatorSelectorControl), new PropertyMetadata(AnalysisIndicatorType.None));

    /// <summary>
    /// Type of the current indicator
    /// </summary>
    public AnalysisIndicatorType CurrentIndicatorType
    {
        get => (AnalysisIndicatorType)GetValue(CurrentIndicatorTypeProperty);
        set => SetValue(CurrentIndicatorTypeProperty, value);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Raises the "property changed" event.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// General field setter.
    /// </summary>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}