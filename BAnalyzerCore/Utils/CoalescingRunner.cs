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

namespace BAnalyzerCore.Utils;

/// <summary>
/// Runs an asynchronous action on request so that runs never overlap: the requests made while
/// the action is running are coalesced into exactly one more run, which starts after the current one ends
/// (so the effect of the latest request is never lost).
/// </summary>
/// <remarks>
/// The action is always invoked in the synchronization context of the request that started the run
/// (e.g. on the UI thread if requested from there).
/// </remarks>
public sealed class CoalescingRunner
{
    private readonly Func<Task> _action;

    private readonly object _lock = new();

    private bool _running;

    private bool _pending;

    /// <summary>
    /// Constructor.
    /// </summary>
    public CoalescingRunner(Func<Task> action) =>
        _action = action ?? throw new ArgumentNullException(nameof(action));

    /// <summary>
    /// Requests a run of the action. The returned task completes when the action is not running
    /// anymore if this call has started a run, or immediately if a run was already in progress.
    /// An exception thrown by the action ends the run and is propagated to the caller that started it.
    /// </summary>
    public async Task RunAsync()
    {
        lock (_lock)
        {
            _pending = true;

            if (_running)
                return;

            _running = true;
        }

        try
        {
            while (true)
            {
                lock (_lock)
                {
                    if (!_pending)
                    {
                        _running = false;
                        return;
                    }

                    _pending = false;
                }

                await _action();
            }
        }
        catch
        {
            lock (_lock)
                _running = false;

            throw;
        }
    }
}
