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

namespace BAnalyzer.Controllers;

/// <summary>
/// Read-only interface to the controller below.
/// </summary>
internal interface IUpdateControllerReadOnly
{
    /// <summary>
    /// Returns "true" if the request with given <param name="requestId"/>
    /// still has a potential to be applied if processed further on.
    /// </summary>
    public bool IsRequestStillRelevant(int requestId);
}

/// <summary>
/// Provides a mechanism of controlling the order of update requests as well as
/// a criteria of discarding update requests that are not relevant in the
/// middle of the processing (that way we can reduce client-server traffic).
/// </summary>
internal class UpdateController : IUpdateControllerReadOnly
{
    private volatile int _latestIssueRequestId = 0;
    private volatile int _latestAppliedRequestId = -1;

    /// <summary>
    /// Issues id for a new update request.
    /// </summary>
    public int IssueNewRequest() => Interlocked.Increment(ref _latestIssueRequestId);

    /// <summary>
    /// Returns "true" if the given <param name="requestToApplyId"/>is larger
    /// than the ID of "latest applied request", in which case the local
    /// variable that stores ID of the "latest applied request" gets updated.
    /// </summary>
    public bool TryApplyRequest(int requestToApplyId)
    {
        if (_latestAppliedRequestId < requestToApplyId)
        {
            _latestAppliedRequestId = requestToApplyId;
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public bool IsRequestStillRelevant(int requestId)
    {
        return _latestAppliedRequestId < requestId;
    }
}