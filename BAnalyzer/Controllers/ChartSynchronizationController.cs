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

using BAnalyzer.Interfaces;
using System.ComponentModel;

namespace BAnalyzer.Controllers;

/// <summary>
/// Interface for the corresponding class below.
/// </summary>
public interface IChartSynchronizationController
{
    /// <summary>
    /// Un-registers the given control.
    /// Returns "true" in case of success.
    /// </summary>
    bool UnRegister(ISynchronizableExchangeControl control);

    /// <summary>
    /// Registers the given control.
    /// </summary>
    void Register(ISynchronizableExchangeControl control);
}

/// <summary>
/// Synchronization controller for <see cref="ISynchronizableChart"/>.
/// </summary>
internal class ChartSynchronizationController : IChartSynchronizationController
{
    private readonly List<ISynchronizableExchangeControl> _controls = new();

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

                if (_synchronizationEnabled && _controls.Count > 1)
                {
                    var source = _controls.MaxBy(x => x.SyncChart.TimeFrameEndLocalTime);
                    BroadcastFrameEnd(source, source.SyncChart.TimeFrameEndLocalTime);
                    BroadcastInFocusTime(source, source.SyncChart.InFocusTime);
                    SynchronizeSettings(source.Settings);
                }
            }
        }
    }

    /// <inheritdoc/>
    public void Register(ISynchronizableExchangeControl control)
    {
        if (control == null) throw new ArgumentNullException(nameof(control));

        if (_controls.Contains(control))
            throw new OperationCanceledException("An attempt to register the same control twice");

        _controls.Add(control);

        control.SyncChart.BroadcastFrameEndEvent += BroadcastFrameEnd;
        control.SyncChart.BroadcastInFocusTimeEvent += BroadcastInFocusTime;

        if (_controls.Count > 1 && _synchronizationEnabled)
        {
            var source = _controls[0];
            control.SyncChart.UpdateTimeFrameEndNoBroadcast(source.SyncChart.TimeFrameEndLocalTime);
            control.SyncChart.UpdateInFocusTimeNoBroadcast(source.SyncChart.InFocusTime);
            control.Settings.Assign(source.Settings, excludeExchangeDescriptor: true);
        }

        control.Settings.PropertyChanged += ExchangeSettings_PropertyChanged;
    }

    /// <inheritdoc/>
    public bool UnRegister(ISynchronizableExchangeControl control)
    {
        if (control == null)
            return false;

        if (_controls.Remove(control))
        {
            control.SyncChart.BroadcastFrameEndEvent -= BroadcastFrameEnd;
            control.SyncChart.BroadcastInFocusTimeEvent -= BroadcastInFocusTime;

            control.Settings.PropertyChanged -= ExchangeSettings_PropertyChanged;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Property changed handler of all exchange controls.
    /// </summary>
    private void ExchangeSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is not IExchangeSettings source || !_synchronizationEnabled)
            return;

        SynchronizeSettings(source);
    }

    /// <summary>
    /// Synchronizes settings of all the exchange controls with the given <param name="source"/>
    /// </summary>
    private void SynchronizeSettings(IExchangeSettings source)
    {
        foreach (var control in _controls)
        {
            // Unsubscribe from the "property changed" event to
            // avoid reacting on the changes that follow. Not a
            // very elegant solution byt let it be so for e while.
            control.Settings.PropertyChanged -= ExchangeSettings_PropertyChanged;
            control.Settings.Assign(source, excludeExchangeDescriptor: true);
            control.Settings.PropertyChanged += ExchangeSettings_PropertyChanged;
        }
    }

    /// <summary>
    /// General method to broadcast property values.
    /// </summary>
    private void BroadcastProperty(object sender, Action<ISynchronizableChart> propertyUpdater)
    {
        if (!SynchronizationEnabled)
            return;

        foreach (var c in _controls)
            if (!sender.Equals(c.SyncChart))
                propertyUpdater(c.SyncChart);
    }

    /// <summary>
    /// Event handler for the "broadcast frame end" event.
    /// </summary>
    private void BroadcastFrameEnd(object sender, double frameEnd) =>
        BroadcastProperty(sender, chart => chart.UpdateTimeFrameEndNoBroadcast(frameEnd));

    /// <summary>
    /// Event handler for the "broadcast in-focus time" event.
    /// </summary>
    private void BroadcastInFocusTime(object sender, double inFocusTime) =>
        BroadcastProperty(sender, chart => chart.UpdateInFocusTimeNoBroadcast(inFocusTime));
}