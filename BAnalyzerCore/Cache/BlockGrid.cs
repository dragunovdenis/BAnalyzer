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
using Binance.Net.Interfaces;

namespace BAnalyzerCore.Cache;

/// <summary>
/// A collection of blocks aligned along certain time-grid.
/// </summary>
public class BlockGrid
{
    /// <summary>
    /// Collection of blocks.
    /// </summary>
    public List<KLineBlock> Blocks { get; } = new();

    /// <summary>
    /// Returns index of the first block whose "end time"
    /// is not less than the given <param name="time"/>.
    /// In case there is no such a block "-1" is returned.
    /// </summary>
    private int LowerBound(DateTime time) => Blocks.LowerBoundTime(b => b.End, time);

    /// <summary>
    /// Returns index of a block covering a time interval
    /// containing the given <param name="time"/> point,
    /// or "-1" if such a block is not present.
    /// </summary>
    private int FindBlock(DateTime time)
    {
        var candidateId = LowerBound(time);

        if (candidateId == Blocks.Count || Blocks[candidateId].Begin > time)
            return -1;

        return candidateId;
    }

    /// <summary>
    /// Returns "true" if all the blocks are ordered in ascending order (chronologically).
    /// </summary>
    private bool BlocksAreOrdered()
    {
        for (var blockId = 1; blockId < Blocks.Count; blockId++)
            if (Blocks[blockId - 1].End > Blocks[blockId].Begin) return false;

        return true;
    }

    /// <summary>
    /// Returns a collection of "k-lines" that cover the given time interval
    /// or null if the data in the grid does not fully cover the interval.
    /// </summary>
    public IList<IBinanceKline> Retrieve(DateTime timeBegin, DateTime timeEnd)
    {
        var blockIdBegin = FindBlock(timeBegin);

        if (blockIdBegin < 0)
            return null;

        var blockIdEnd = FindBlock(timeEnd);

        if (blockIdEnd < 0)
            return null;

        for (var blockId = blockIdBegin + 1; blockId <= blockIdEnd; blockId++)
        {
            if (!Blocks[blockIdBegin].IsAdjacentAndPrecedingTo(Blocks[blockId]))
                return null;
        }

        // All the blocks are there, we need just collect the relevant data.
        var result = new List<IBinanceKline>();

        var indexBegin = Blocks[blockIdBegin].FindBeginKLineId(timeBegin);
        var indexEnd = Blocks[blockIdEnd].FindEndKLineId(timeEnd);

        if (blockIdBegin == blockIdEnd)
        {
            var data = Blocks[blockIdBegin].Data;
            result.AddRange(data.Skip(indexBegin).SkipLast(data.Count - indexEnd - 1));
        }
        else
        {
            result.AddRange(Blocks[blockIdBegin].Data.Skip(indexBegin));

            for (var blockId = blockIdBegin + 1; blockId < blockIdEnd; blockId++)
                result.AddRange(Blocks[blockId].Data);

            result.AddRange(Blocks[blockIdEnd].Data.Take(indexEnd + 1));
        }

        return result;
    }

    /// <summary>
    /// Append the given <param name="collection"/> of "k-lines" to the current "grid".
    /// </summary>
    public void Append(IReadOnlyList<IBinanceKline> collection)
    {
        var blockToAppend = new KLineBlock(collection);

        if (blockToAppend.IsEmpty())
            return;

        var beginBlockIndexToCheck = LowerBound(blockToAppend.Begin);
        var endBlockIndexToCheck = LowerBound(blockToAppend.End);

        for (var blockId = Math.Min(endBlockIndexToCheck, Blocks.Count - 1);
             blockId >= beginBlockIndexToCheck; blockId--)
        {
            var block = Blocks[blockId];
            if (blockToAppend.IsAdjacentTo(block) || blockToAppend.Intersects(block))
            {
                blockToAppend.MergePreserve(block);
                Blocks.RemoveAt(blockId);
            }
        }

        Blocks.Insert(beginBlockIndexToCheck, blockToAppend);

        if (!BlocksAreOrdered())
            throw new InvalidOperationException("Collection of blocks is broken");
    }
}