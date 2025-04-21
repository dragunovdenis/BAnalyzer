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

using BAnalyzerCore;
using BAnalyzerCore.Cache;
using Binance.Net.Enums;
using FluentAssertions;
using System.Linq;

namespace BAnalyzerCoreTest;

[TestClass]
public class BlockGridTest
{
    /// <summary>
    /// Returns "true" if the given <param name="block"/> does not cover
    /// the time interval that starts at <param name="intervalBegin"/>
    /// and ends at <param name="intervalEnd"/> time.
    /// </summary>
    private static bool IntervalExceedsBlock(IKLineBlockReadOnly block,
        DateTime intervalBegin, DateTime intervalEnd) => intervalBegin < block.Begin || intervalEnd > block.End;

    /// <summary>
    /// Returns "true" if the given <param name="block"/> covers
    /// the time interval that starts at <param name="intervalBegin"/>
    /// and ends at <param name="intervalEnd"/> time.
    /// </summary>
    private static bool BlockContainsInterval(IKLineBlockReadOnly block,
        DateTime intervalBegin, DateTime intervalEnd) => intervalBegin >= block.Begin && intervalEnd <= block.End;

    /// <summary>
    /// General method to exercise the append/retrieve
    /// functionality for the given <param name="grid"/>
    /// versus the given collection of <param name="composingBlocks"/>.
    /// Blocks in <param name="composingBlocks"/> collection are assumed to be non-mergeable.
    /// </summary>
    private static void RunExtensiveDataRetrievalTest(KLineBlock[] composingBlocks, BlockGrid grid)
    {
        var granularity = composingBlocks[0].Granularity;

        var minTimePoint = composingBlocks.MinBy(x => x.Begin).Begin.Subtract(10 * granularity);
        var maxTimePoint = composingBlocks.MaxBy(x => x.Begin).End.Add(10 * granularity);
        var stepCount = 2 * (int)((maxTimePoint - minTimePoint) / granularity);

        for (var i = 0; i < stepCount; i++)
        for (var j = i + 1; j < stepCount; j++)
        {
            var b = minTimePoint.Add(0.5 * i * granularity);
            var e = minTimePoint.Add(0.5 * j * granularity);

            var cachedData = grid.Retrieve(b, e);

            if (cachedData != null)
            {
                KLineBlock.CheckChronologicalIntegrity(cachedData.ToList(), granularity);

                cachedData.First().OpenTime.Should().Be(i % 2 == 0 ? b : b.Add(-0.5 * granularity),
                    "because this is supposed to be the beginning of the requested interval");

                cachedData.Last().CloseTime.AddSeconds(BinanceConstants.KLineTimeGapSec).Should()
                    .Be(j % 2 == 0 ? e : e.Add(0.5 * granularity),
                        "because this is supposed to be the end of requested interval");

                composingBlocks.Any(x => BlockContainsInterval(x, b, e)).Should()
                    .BeTrue("because cached data should be present in the composing intervals");
            }
            else
            {
                composingBlocks.All(x =>
                    IntervalExceedsBlock(x, b, e)).Should().BeTrue(
                    "because if interval is not found in the cache then " +
                    "it must not be covered by the intervals put into the cache");
            }
        }
    }

    [TestMethod]
    public void GridWithTwoDistinctBlocksTest()
    {
        // Arrange
        var beginTime = new DateTime(2000, 1, 1);
        var granularity = KlineInterval.OneHour;
        var block0 = KLineGenerator.GenerateBlock(beginTime, granularity, 10);
        var block1 = KLineGenerator.GenerateBlock(block0.End.AddHours(10), granularity, 10);

        block0.CanBeMergedWith(block1).Should().BeFalse("because the blocks are supposed to be distinct");

        var grid = new BlockGrid();
        grid.Append(block0.Data);
        grid.Append(block1.Data);

        // Act/Assert
        RunExtensiveDataRetrievalTest([block0, block1], grid);
    }

    [TestMethod]
    public void GridWithThreeAdjacentAndTwoSeparateStandingBlocksBlocksTest()
    {
        // Arrange
        var beginTime = new DateTime(2000, 1, 1);
        var granularity = KlineInterval.OneMinute;
        var adjacentBlock0 = KLineGenerator.GenerateBlock(beginTime, granularity, 10);
        var adjacentBlock1 = KLineGenerator.GenerateBlock(adjacentBlock0.End, granularity, 15);
        var adjacentBlock2 = KLineGenerator.GenerateBlock(adjacentBlock1.End, granularity, 20);

        adjacentBlock0.IsAdjacentAndPrecedingTo(adjacentBlock1).Should().BeTrue("by design");
        adjacentBlock1.IsAdjacentAndPrecedingTo(adjacentBlock2).Should().BeTrue("by design");

        var separateStandingBlockToTheLeft = KLineGenerator.
            GenerateBlock(beginTime.Subtract(20 * granularity.ToTimeSpan()), granularity, 10);
        var separateStandingBlockToTheRight = KLineGenerator.
            GenerateBlock(adjacentBlock2.End.Add(10 * granularity.ToTimeSpan()), granularity, 10);

        var grid = new BlockGrid();
        grid.Append(separateStandingBlockToTheLeft.Data);
        grid.Append(adjacentBlock1.Data);
        grid.Append(adjacentBlock2.Data);
        grid.Append(adjacentBlock0.Data);
        grid.Append(separateStandingBlockToTheRight.Data);

        var mergedAdjacentBlocks = adjacentBlock0.Copy().Merge(adjacentBlock1).Merge(adjacentBlock2);

        mergedAdjacentBlocks.CanBeMergedWith(separateStandingBlockToTheLeft).Should()
            .BeFalse("because the block is distinct by design");

        mergedAdjacentBlocks.CanBeMergedWith(separateStandingBlockToTheRight).Should()
            .BeFalse("because the block is distinct by design");

        RunExtensiveDataRetrievalTest([mergedAdjacentBlocks,
            separateStandingBlockToTheLeft, separateStandingBlockToTheRight], grid);
    }

    [TestMethod]
    public void GridWithThreeOverlappingBlocksTest()
    {
        // Arrange
        var beginTime = new DateTime(2000, 1, 1);
        var granularity = KlineInterval.FiveMinutes;
        var block0 = KLineGenerator.GenerateBlock(beginTime, granularity, 10);
        var block1 = KLineGenerator.GenerateBlock(block0.End.Add(-5 * granularity.ToTimeSpan()), granularity, 15);
        var block2 = KLineGenerator.GenerateBlock(block1.End.Add(-5 * granularity.ToTimeSpan()), granularity, 10);

        var grid = new BlockGrid();
        grid.Append(block0.Data);
        grid.Append(block1.Data);
        grid.Append(block2.Data);

        RunExtensiveDataRetrievalTest([block0.Copy().Merge(block1).Merge(block2)], grid);
    }

    /// <summary>
    /// Constructs a grid out of the two given blocks with <param name="block1"/> being added the last
    /// and ensures that data from <param name="block1"/> overrides the corresponding data in the grid.
    /// </summary>
    private static BlockGrid CheckThatAppendedBlockOverridesExistingDataInTheGrid(KLineBlock block0, KLineBlock block1)
    {
        var grid = new BlockGrid();
        grid.Append(block0.Data);
        grid.Append(block1.Data);

        var data = grid.Retrieve(block1.Begin, block1.End);
        data.Should().NotBeNull("because this interval must be present in the grid by design");

        data.Zip(block1.Data, (x, y) => x.Equals(y)).All(x => x).Should().BeTrue(
            "because block \"1\" was added to the grid last and it must override the corresponding data in the grid");

        return grid;
    }

    [TestMethod]
    public void GridLastBlockOverridesDataTest()
    {
        // Arrange
        var beginTime = new DateTime(2000, 1, 1);
        var granularity = KlineInterval.ThirtyMinutes;
        var block0 = KLineGenerator.GenerateBlock(beginTime, granularity, 30);
        var block1 = KLineGenerator.GenerateBlock(block0.Begin.Add(10 * granularity.ToTimeSpan()), granularity, 10);

        block0.Contains(block0).Should().BeTrue("because we need to model a situation when two blocks share a time interval");

        var gridBlock1AddedLast = CheckThatAppendedBlockOverridesExistingDataInTheGrid(block0, block1);
        RunExtensiveDataRetrievalTest([block0], gridBlock1AddedLast);

        var gridBlock0AddedLast = CheckThatAppendedBlockOverridesExistingDataInTheGrid(block1, block0);
        RunExtensiveDataRetrievalTest([block0], gridBlock0AddedLast);
    }
}