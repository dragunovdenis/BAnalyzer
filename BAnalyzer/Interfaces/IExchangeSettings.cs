//Copyright (c) 2025 Denys Dragunov, dragunovdenis@gmail.com
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
using BAnalyzerCore.DataStructures;
using System.ComponentModel;

namespace BAnalyzer.Interfaces;

/// <summary>
/// Interface for exchange settings.
/// </summary>
public interface IExchangeSettings : INotifyPropertyChanged
{
    /// <summary>
    /// ID of the "current" crypto-exchange.
    /// </summary>
    ExchangeId ExchangeId { get; }

    /// <summary>
    /// Descriptor of the crypto-currency pair.
    /// </summary>
    string ExchangeDescriptor { get; }

    /// <summary>
    /// Time discretization to use when displaying charts.
    /// </summary>
    TimeGranularity TimeDiscretization { get; }

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

    /// <summary>
    /// Returns "true" if the instance is valid.
    /// </summary>
    bool IsValid();
}