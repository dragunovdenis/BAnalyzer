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

using BAnalyzerCore.Utils;
using Binance.Net.Enums;
using Binance.Net.Interfaces;

namespace BAnalyzerCore.Cache;

/// <summary>
/// Binance exchange data of a certain granularity related to a certain asset.
/// </summary>
internal class AssetTimeView
{
    /// <summary>
    /// Grid data.
    /// </summary>
    private readonly BlockGrid _grid;

    private DateTime _zeroTime = DateTime.MinValue;

    /// <summary>
    /// Constructor.
    /// </summary>
    public AssetTimeView(KlineInterval granularity) => _grid = new BlockGrid(granularity);

    /// <summary>
    /// Updates the "history begin time" with the new value.
    /// </summary>
    public void SetZeroTimePoint(DateTime newTime) => _zeroTime = newTime;

    /// <summary>
    /// Resets the "history begin time" to its default value.
    /// </summary>
    public void ResetZeroTimePoint() => _zeroTime = DateTime.MinValue;

    /// <summary>
    /// Returns a collection of "k-lines" that cover the given time interval
    /// or null if the data in the grid does not fully cover the interval.
    /// </summary>
    public IList<IBinanceKline> Retrieve(DateTime timeBegin, DateTime timeEnd) =>
        _grid.Retrieve(DateTimeUtils.Max(timeBegin, _zeroTime), timeEnd);

    /// <summary>
    /// Append the given <param name="collection"/> of "k-lines" to the current "grid".
    /// </summary>
    public void Append(IReadOnlyList<IBinanceKline> collection) => _grid.Append(collection);
}