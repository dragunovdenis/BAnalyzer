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

namespace BAnalyzer.Interfaces;

/// <summary>
/// Interface for the chart control that can
/// be synchronized with other chart controls.
/// </summary>
public interface ISynchronizableChart
{
    /// <summary>
    /// The end of displayed time frame in OLE Automation Date format, local time.
    /// Can be "NaN" meaning "Now" (time-wise).
    /// </summary>
    double TimeFrameEndLocalTime { get; }

    /// <summary>
    /// Point in time that is under the mouse pointer.
    /// </summary>
    double InFocusTime { get; }

    /// <summary>
    /// Updates "in focus time" property with the given
    /// <param name="newValue"/> without broadcasting.
    /// </summary>
    bool UpdateInFocusTimeNoBroadcast(double newValue);

    /// <summary>
    /// Updates value of the "end of time frame" parameter but does not "broadcast" the change.
    /// </summary>
    bool UpdateTimeFrameEndNoBroadcast(double newValue);

    /// <summary>
    /// Event that occurs when chart wants to broadcast new "frame end" point.
    /// </summary>
    event Action<object, double> BroadcastFrameEndEvent;

    /// <summary>
    /// Event that occurs when chart wants to broadcast new "in-focus time" point.
    /// </summary>
    event Action<object, double> BroadcastInFocusTimeEvent;
}