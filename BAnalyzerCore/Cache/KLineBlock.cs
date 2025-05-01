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

using Binance.Net.Interfaces;
using BAnalyzerCore.Utils;
using Binance.Net.Enums;

namespace BAnalyzerCore.Cache;

/// <summary>
/// Read-only interface for the class below.
/// </summary>
public interface IKLineBlockReadOnly
{
    /// <summary>
    /// Duration of a time-interval covered by a single data item in the block.
    /// </summary>
    KlineInterval Granularity { get; }

    /// <summary>
    /// Read-only access to the data.
    /// </summary>
    IReadOnlyList<IBinanceKline> Data { get; }

    /// <summary>
    /// The left end of the time interval covered by the clock.
    /// </summary>
    DateTime Begin { get; }

    /// <summary>
    /// The right end of the time interval covered by th block.
    /// </summary>
    DateTime End { get; }

    /// <summary>
    /// Returns "true" if the time interval covered by the block is "empty".
    /// </summary>
    bool IsEmpty();

    /// <summary>
    /// Returns "true" if the "end" of the current block is immediately
    /// followed by the "begin" of the given <param name="block"/>.
    /// </summary>
    bool IsAdjacentAndPrecedingTo(IKLineBlockReadOnly block);

    /// <summary>
    /// Returns "true" if the "end" of the given <param name="block"/>
    /// is immediately followed by the "begin" of the current one.
    /// </summary>
    bool IsAdjacentAndFollowingTo(IKLineBlockReadOnly block);

    /// <summary>
    /// Returns "true" if the two blocks do not intersect each other (time-wise)
    /// and there is no gap between them (again, time-wise).
    /// </summary>
    bool IsAdjacentTo(IKLineBlockReadOnly block);

    /// <summary>
    /// Returns "true" if the current and the given <param name="block"/> blocks
    /// have a nontrivial intersection (time-wise).
    /// </summary>
    bool Intersects(IKLineBlockReadOnly block);

    /// <summary>
    /// Returns "true" if the current block contains the given <param name="block"/>.
    /// </summary>
    bool Contains(IKLineBlockReadOnly block);

    /// <summary>
    /// Returns "true" if the current block coincide with the given one.
    /// </summary>
    bool Coincide(IKLineBlockReadOnly block);

    /// <summary>
    /// Returns "true" if the granularities of the given
    /// <param name="block"/> and the current block are the same.
    /// </summary>
    bool SameGranularity(IKLineBlockReadOnly block);

    /// <summary>
    /// Returns "true" if the current block can be merged
    /// with the given <param name="block"/>/
    /// </summary>
    bool CanBeMergedWith(IKLineBlockReadOnly block);

    /// <summary>
    /// Returns index of an item with the "open-time" equal to that of
    /// the given <param name="item"/> or "-1" if there is no such item.
    /// </summary>
    int FindItem(IBinanceKline item);
}

/// <summary>
/// A continuous (with respect to time) block of trading information.
/// </summary>
public class KLineBlock : IKLineBlockReadOnly
{
    private readonly KlineInterval _granularity;

    /// <inheritdoc/>
    public KlineInterval Granularity => _granularity;

    private readonly List<IBinanceKline> _data = new();

    /// <inheritdoc/>
    public IReadOnlyList<IBinanceKline> Data => _data;

    /// <inheritdoc/>
    public DateTime Begin => _data.Count > 0 ? _data.First().OpenTime : DateTime.MaxValue;

    /// <inheritdoc/>
    public DateTime End => _data.Count > 0 ? _data.Last().CloseTime.
        AddSeconds(BinanceConstants.KLineTimeGapSec) : DateTime.MinValue;

    /// <inheritdoc/>
    public bool IsEmpty() => _data.Count == 0;

    /// <inheritdoc/>
    public bool IsAdjacentAndPrecedingTo(IKLineBlockReadOnly block) => End == block.Begin;

    /// <inheritdoc/>
    public bool IsAdjacentAndFollowingTo(IKLineBlockReadOnly block) => Begin == block.End;

    /// <inheritdoc/>
    public bool IsAdjacentTo(IKLineBlockReadOnly block) =>
        IsAdjacentAndFollowingTo(block) || IsAdjacentAndPrecedingTo(block);

    /// <inheritdoc/>
    public bool Intersects(IKLineBlockReadOnly block) => Begin < block.End && block.Begin < End;

    /// <inheritdoc/>
    public bool Contains(IKLineBlockReadOnly block) => Begin <= block.Begin && End >= block.End;

    /// <inheritdoc/>
    public bool Coincide(IKLineBlockReadOnly block) => Contains(block) && block.Contains(this);

    /// <inheritdoc/>
    public bool SameGranularity(IKLineBlockReadOnly block) => Granularity.Equals(block.Granularity);

    /// <inheritdoc/>
    public bool CanBeMergedWith(IKLineBlockReadOnly block) => IsAdjacentTo(block) || Intersects(block);

    /// <summary>
    /// Returns difference between "open" and "close" times of the given <param name="item"/>.
    /// </summary>
    private static TimeSpan GetSpan(IBinanceKline item) => item.CloseTime.AddSeconds(BinanceConstants.KLineTimeGapSec) - item.OpenTime;

    /// <summary>
    /// Returns index of the first item from the data collection
    /// whose "close time" is not less than the given <param name="time"/>
    /// or the number of items in the data collection if there is no such item.
    /// </summary>
    public int FindEndKLineId(DateTime time) =>
        _data.LowerBoundTime(b => b.CloseTime.
            AddSeconds(BinanceConstants.KLineTimeGapSec), time);

    /// <summary>
    /// Returns index of the last item from the data collection
    /// whose "open time" is not greater than the given <param name="time"/>
    /// or "-1" if there is no such item.
    /// </summary>
    public int FindBeginKLineId(DateTime time) => _data.UpperBoundTime(b => b.OpenTime, time) - 1;

    /// <summary>
    /// Returns "true" if the "k-line" data contained in the
    /// block satisfies chronological integrity requirements
    /// (i.e., same granularity, no gaps).
    /// </summary>
    public bool IsValid() => CheckChronologicalIntegrity(_data, _granularity);

    /// <summary>
    /// Returns maximal acceptable deviation of a "k-line" with the given <param name="granularity"/>
    /// from a "canonical" time span assumed for <param name="granularity"/>.
    /// </summary>
    private static TimeSpan GetMaximalAcceptableKlineDeviationFromCanonicalTimeSpan(KlineInterval granularity)
    {
        switch (granularity)
        {
            case KlineInterval.OneMonth: return TimeSpan.FromDays(2);
            default: return TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Returns "true" if the given collection of "k-lines"
    /// satisfies chronological integrity requirements
    /// (i.e., same granularity, no gaps).
    /// </summary>
    public static bool CheckChronologicalIntegrity(IReadOnlyList<IBinanceKline> data, KlineInterval granularity)
    {
        if (data.Count == 0) return true;

        var prevItem = data[0];
        var granularityTimeSpan = granularity.ToTimeSpan();
        var acceptableSpanDeviation = GetMaximalAcceptableKlineDeviationFromCanonicalTimeSpan(granularity);
        for (var itemId = 1; itemId < data.Count; itemId++)
        {
            var nextItem = data[itemId];

            if (prevItem.CloseTime.AddSeconds(BinanceConstants.KLineTimeGapSec) != nextItem.OpenTime
                || (granularityTimeSpan - GetSpan(nextItem)).Duration() > acceptableSpanDeviation)
                return false;

            prevItem = nextItem;
        }

        return true;
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    public KLineBlock(KlineInterval granularity) => _granularity = granularity;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="data">Chronologically ordered data.</param>
    public KLineBlock(KlineInterval granularity, IReadOnlyList<IBinanceKline> data)
    {
        if (data.Count == 0)
            throw new ArgumentException("An empty block is not supposed to be created");

        _granularity = granularity;
        _data = data.ToList();

        if (!IsValid())
            throw new ArgumentException("Unexpected inconsistency detected");
    }

    /// <summary>
    /// Compares items of `IBinanceKline` by their open time. 
    /// </summary>
    private class OpenTimeComparer : IComparer<IBinanceKline>
    {
        /// <summary>
        /// Compares the two items by their "open times".
        /// </summary>
        public int Compare(IBinanceKline x, IBinanceKline y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (y is null) return 1;
            if (x is null) return -1;
            return x.OpenTime.CompareTo(y.OpenTime);
        }
    }

    /// <inheritdoc/>
    public int FindItem(IBinanceKline item) => _data.BinarySearch(item, new OpenTimeComparer());

    /// <summary>
    /// Subtracts the given <param name="blockToSubtract"/> from the current block.
    /// The two blocks are supposed to have the same "granularity", otherwise
    /// an exception will be thrown.
    /// Returns pointer to the resulting block.
    /// Raises an exception if subtraction would result in a non-continuous
    /// block (which is not basically a block). The latter, obviously, can happen
    /// if and only if the given <param name="blockToSubtract"/> is contained inside the current
    /// one (and they do not share any of the boundaries).
    /// </summary>
    public KLineBlock Subtract(IKLineBlockReadOnly blockToSubtract)
    {
        if (!SameGranularity(blockToSubtract))
            throw new ArgumentException("The operation is supported only for blocks with the same granularity");

        if (blockToSubtract.Begin > Begin && blockToSubtract.End < End && !blockToSubtract.IsEmpty())
            throw new InvalidOperationException("Subtraction would result in a discontinuous block");

        if (blockToSubtract.End <= Begin || blockToSubtract.Begin >= End)
            return this; // Nothing to subtract.

        if (blockToSubtract.Begin <= Begin && blockToSubtract.End >= End)
        {
            _data.Clear();
        } else if (blockToSubtract.Begin > Begin)
        {
            var startId = FindItem(blockToSubtract.Data.First());
            if (startId < 0) throw new InvalidOperationException("Unexpected scenario");

            _data.RemoveRange(startId, _data.Count - startId);
        } else if (blockToSubtract.End < End)
        {
            var endId = FindItem(blockToSubtract.Data.Last());
            if (endId < 0) throw new InvalidOperationException("Unexpected scenario");

            _data.RemoveRange(0, endId + 1);
        } else throw new InvalidOperationException("Unexpected scenario");

        return this;
    }

    /// <summary>
    /// Inserts the data from the given <param name="blockToInsert"/>
    /// into the current block.
    /// IMPORTANT: <param name="blockToInsert"/> must be contained
    /// by the current block (chronologically).
    /// </summary>
    private void Insert(IKLineBlockReadOnly blockToInsert)
    {
        if (blockToInsert.IsEmpty())
            return;

        var insertStartId = FindItem(blockToInsert.Data[0]);

        for (var itemId = 0; itemId < blockToInsert.Data.Count; itemId++)
            _data[insertStartId + itemId] = blockToInsert.Data[itemId];
    }

    /// <summary>
    /// Merges given <param name="blockToMerge"/> to the current one.
    /// For the operation to succeed the two blocks should be either
    /// intersecting each other of adjacent to each other;
    /// IMPORTANT: the data from <param name="blockToMerge"/> which
    /// is duplicated (chronologically) in the current block will be
    /// overwritten with the corresponding data from <param name="blockToMerge"/>.
    /// I.e., "k-lines" from <param name="blockToMerge"/> will substitute
    /// the corresponding "k-lines" from the current block.
    /// Returns pointer to the merged instance.
    /// </summary>
    public KLineBlock MergeOverwrite(IKLineBlockReadOnly blockToMerge)
    {
        if (!SameGranularity(blockToMerge))
            throw new ArgumentException("The operation is supported only for blocks with the same granularity");

        if (Contains(blockToMerge))
        {
            Insert(blockToMerge);
            return this;
        }

        Subtract(blockToMerge);

        if (IsEmpty() || IsAdjacentAndPrecedingTo(blockToMerge))
        {
            _data.AddRange(blockToMerge.Data);
        } else if (IsAdjacentAndFollowingTo(blockToMerge))
        {
            _data.InsertRange(0, blockToMerge.Data);
        } else throw new InvalidOperationException("Unexpected scenario");

        return this;
    }

    /// <summary>
    /// Merges given <param name="blockToMerge"/> to the current one.
    /// For the operation to succeed the two blocks should be either
    /// intersecting each other of adjacent to each other;
    /// IMPORTANT: the data from <param name="blockToMerge"/>
    /// which is duplicated (chronologically) in the current block will
    /// be ignored during the merge.
    /// </summary>
    public KLineBlock MergePreserve(IKLineBlockReadOnly blockToMerge)
    {
        if (!SameGranularity(blockToMerge))
            throw new ArgumentException("The operation is supported only for blocks with the same granularity");

        if (Contains(blockToMerge))
            return this;

        if (IsEmpty() || IsAdjacentAndPrecedingTo(blockToMerge))
        {
            _data.AddRange(blockToMerge.Data);
        } else if (IsAdjacentAndFollowingTo(blockToMerge))
        {
            _data.InsertRange(0, blockToMerge.Data);
        } else if (Intersects(blockToMerge))
        {
            var itemsToAddFromLeft = blockToMerge.FindItem(_data.First());

            if (itemsToAddFromLeft > 0)
                _data.InsertRange(0, blockToMerge.Data.Take(itemsToAddFromLeft));

            var indexOfTheRightEndOfTheCurrentBlockInTheBlockToMerge = blockToMerge.FindItem(_data.Last());

            if (indexOfTheRightEndOfTheCurrentBlockInTheBlockToMerge >= 0)
            {
                var itemsToAddFromRight =
                    blockToMerge.Data.Count - 1 - indexOfTheRightEndOfTheCurrentBlockInTheBlockToMerge;
                _data.AddRange(blockToMerge.Data.TakeLast(itemsToAddFromRight));
            }

        } else throw new InvalidOperationException("Blocks can't be merged");

        return this;
    }

    /// <summary>
    /// Splits the given block onto two adjacent blocks.
    /// If the number of "k-lines" in the current block is even
    /// then both returned blocks have the same number of elements.
    /// Otherwise, the "firmer" one will have one less elements
    /// than the "latter" one. Can return empty blocks.
    /// </summary>
    public (KLineBlock PreviousBlock, KLineBlock NextBlock) SplitByHalf()
    {
        var elementsInTheFirstBlock = _data.Count / 2;

        return (PreviousBlock: new KLineBlock(_granularity, _data.Take(elementsInTheFirstBlock).ToList()),
            NextBlock: new KLineBlock(_granularity, _data.Skip(elementsInTheFirstBlock).ToList()));
    }

    /// <summary>
    /// Returns a copy of the current instance.
    /// </summary>
    public KLineBlock Copy() => IsEmpty() ? new(_granularity) : new(_granularity, _data);
}