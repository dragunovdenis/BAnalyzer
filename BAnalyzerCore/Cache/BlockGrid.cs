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

using BAnalyzerCore.DataStructures;
using BAnalyzerCore.Utils;
using Binance.Net.Enums;
using Binance.Net.Interfaces;

namespace BAnalyzerCore.Cache;

/// <summary>
/// A collection of blocks aligned along certain time-grid.
/// </summary>
public class BlockGrid
{
    private readonly KlineInterval _granularity;

    /// <summary>
    /// Constructor.
    /// </summary>
    public BlockGrid(KlineInterval granularity) => _granularity = granularity;

    private readonly List<KLineBlock> _blocks = new();

    /// <summary>
    /// Collection of blocks.
    /// </summary>
    public IReadOnlyList<IKLineBlockReadOnly> Blocks => _blocks;

    /// <summary>
    /// Returns index of the first block whose "end time"
    /// is not less than the given <param name="time"/>.
    /// In case there is no such a block "-1" is returned.
    /// </summary>
    private int LowerBound(DateTime time) => Blocks.LowerBoundTime(b => b.End, time);

    /// <summary>
    /// Returns index of a block covering a time interval
    /// containing the given <param name="endTime"/> point,
    /// or "-1" if such a block is not present.
    /// NB: by design, the block whose index is returned can't have
    /// its "begin" time be equal to <param name="endTime"/>
    /// </summary>
    private int FindEndBlock(DateTime endTime)
    {
        var candidateId = LowerBound(endTime);

        if (candidateId == Blocks.Count || Blocks[candidateId].Begin >= endTime)
            return -1;

        return candidateId;
    }

    /// <summary>
    /// Returns index of the block containing the given <param name="beginTime"/>
    /// or "-1" of there is no such a block.
    /// NB: by design, the block whose index is returned can't have
    /// its "end" time be equal to <param name="beginTime"/>.
    /// </summary>
    private int FindBeginBlock(DateTime beginTime)
    {
        var guess = Blocks.UpperBoundTime(x => x.Begin, beginTime) - 1;

        if (guess >= 0 && Blocks[guess].End > beginTime)
            return guess;

        return -1;
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
    /// Returns "true" if the block grid contains data to fill the interval represented
    /// with <param name="timeBegin"/> and <param name="timeEnd"/> end-points in which case
    /// the output parameters <param name="blockIdBegin"/> and <param name="blockIdEnd"/>
    /// will be assigned indices of the blocks that cover points <param name="timeBegin"/>
    /// and <param name="timeEnd"/> respectively.
    /// In case "false" is returned, the data in the requested time interval can't be
    /// retrieved to the full extent. However, in the latter case values of the output
    /// parameters <param name="blockIdBegin"/> and <param name="blockIdEnd"/> (if non-negative)
    /// are equal to the blocks that are adjacent to the "gap" of missing data,
    /// from the left and from the right respectively.
    /// </summary>
    private bool CanRetrieveData(DateTime timeBegin, DateTime timeEnd, out int blockIdBegin, out int blockIdEnd)
    {
        blockIdBegin = -1;
        blockIdEnd = -1;

        if (timeBegin >= timeEnd) return false;

        blockIdBegin = FindBeginBlock(timeBegin);

        if (blockIdBegin >= 0)
        {
            blockIdEnd = blockIdBegin;

            while (blockIdEnd + 1 < Blocks.Count &&
                   Blocks[blockIdEnd].IsAdjacentAndPrecedingTo(Blocks[blockIdEnd + 1]) &&
                   Blocks[blockIdEnd].End < timeEnd)
            {
                blockIdEnd++;
            }

            if (Blocks[blockIdEnd].End >= timeEnd)
                return true;

            blockIdBegin = blockIdEnd;
            blockIdEnd = -1;
        }

        blockIdEnd = FindEndBlock(timeEnd);

        if (blockIdEnd < 0) return false;

        while (blockIdEnd - 1 >= 0 && Blocks[blockIdEnd - 1].IsAdjacentAndPrecedingTo(Blocks[blockIdEnd]))
            blockIdEnd--;

        return false;
    }

    /// <summary>
    /// Returns a collection of "k-lines" that cover the given time <param name="interval"/>
    /// or null if the data in the grid does not fully cover the interval.
    /// </summary>
    public IList<IBinanceKline> Retrieve(TimeInterval interval, out TimeInterval gapIndicator) =>
        Retrieve(interval.Begin, interval.End, out gapIndicator);

    /// <summary>
    /// Returns a collection of "k-lines" that covers the given time interval
    /// or null if the data in the grid do not (fully) cover the interval.
    /// </summary>
    public IList<IBinanceKline> Retrieve(DateTime timeBegin, DateTime timeEnd, out TimeInterval gapIndicator)
    {
        gapIndicator = null;
        if (CanRetrieveData(timeBegin, timeEnd, out int blockIdBegin, out int blockIdEnd))
        {
            // All the blocks are there, we need just collect the relevant data.
            var result = new List<IBinanceKline>();

            var indexBegin = _blocks[blockIdBegin].FindBeginKLineId(timeBegin);
            var indexEnd = _blocks[blockIdEnd].FindEndKLineId(timeEnd);

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

        gapIndicator = new TimeInterval(blockIdBegin >= 0 ? Blocks[blockIdBegin].End : null,
            blockIdEnd >= 0 ? Blocks[blockIdEnd].Begin : null);

        return null;
    }

    /// <summary>
    /// Append the given <param name="collection"/> of "k-lines" to the current "grid".
    /// </summary>
    public void Append(IReadOnlyList<IBinanceKline> collection)
    {
        var blockToAppend = new KLineBlock(_granularity, collection);

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
                _blocks.RemoveAt(blockId);
            }
        }

        _blocks.Insert(beginBlockIndexToCheck, blockToAppend);

        if (!BlocksAreOrdered())
            throw new InvalidOperationException("Collection of blocks is broken");
    }

    /// <summary>
    /// Ensures that the size of blocks in the grid does
    /// not exceed 2*<param name="targetKlineCountInBlock"/>.
    /// </summary>
    public void Refine(int targetKlineCountInBlock)
    {
        var blocksNew = new List<KLineBlock>();

        foreach (var block in Blocks)
            blocksNew.AddRange(block.Split(targetKlineCountInBlock));

        _blocks.Clear();
        _blocks.AddRange(blocksNew);
    }
}