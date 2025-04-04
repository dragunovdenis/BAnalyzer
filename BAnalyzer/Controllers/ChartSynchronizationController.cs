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

using BAnalyzer.Controls;

namespace BAnalyzer.Controllers;

/// <summary>
/// Interface for the corresponding class below.
/// </summary>
public interface IChartSynchronizationController
{
    /// <summary>
    /// Registers the given control.
    /// </summary>
    void Register(ExchangeChartControl control);

    /// <summary>
    /// Broadcasts given time-frame end between all the registered controls.
    /// </summary>
    void BroadcastFrameEnd(object sender, double frameEnd);
}

/// <summary>
/// Synchronization controller for <see cref="ExchangeChartControl"/>.
/// </summary>
internal class ChartSynchronizationController : IChartSynchronizationController
{
    private readonly List<ExchangeChartControl> _controls = new();

    private bool _synchronizationEnabled = true;

    /// <summary>
    /// Toggles the synchronization mode on and off.
    /// </summary>
    public bool SynchronizationEnabled
    {
        get => _synchronizationEnabled;
        set
        {
            if (_synchronizationEnabled != value)
            {
                _synchronizationEnabled = value;

                if (_synchronizationEnabled && _controls.Count > 0)
                    BroadcastFrameEnd(_controls.First(), _controls.First().TimeFrameEnd);
            }
        }
    }

    /// <summary>
    /// Registers the given control.
    /// </summary>
    public void Register(ExchangeChartControl control) => _controls.Add(control);

    /// <summary>
    /// Broadcasts given time-frame end between all the registered controls.
    /// </summary>
    public void BroadcastFrameEnd(object sender, double frameEnd)
    {
        if (!SynchronizationEnabled)
            return;

        foreach (var c in _controls)
            if (!sender.Equals(c))
                c.UpdateTimeFrameEndNoBroadcast(frameEnd);
    }
}