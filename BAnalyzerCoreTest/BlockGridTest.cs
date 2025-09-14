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
using BAnalyzerCore.DataStructures;
using Binance.Net.Enums;
using FluentAssertions;

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
    private static void RunExtensiveDataRetrievalTest(IKLineBlockReadOnly[] composingBlocks, BlockGrid grid)
    {
        var granularity = composingBlocks[0].Granularity;
        var granularitySpan = granularity.Span;

        var minTimePoint = composingBlocks.MinBy(x => x.Begin).Begin.Subtract(10 * granularitySpan);
        var maxTimePoint = composingBlocks.MaxBy(x => x.Begin).End.Add(10 * granularitySpan);
        var stepCount = 2 * (int)((maxTimePoint - minTimePoint) / granularitySpan);

        for (var i = 0; i < stepCount; i++)
        for (var j = i + 1; j < stepCount; j++)
        {
            var b = minTimePoint.Add(0.5 * i * granularitySpan);
            var e = minTimePoint.Add(0.5 * j * granularitySpan);

            var cachedData = grid.Retrieve(b, e, out var gapIndicator);

            if (cachedData != null)
            {
                gapIndicator.Should().BeNull("because gap indicator is supposed to be not null " +
                                             "in case the requested data can't be retrieved");

                KLineBlock.CheckChronologicalIntegrity(cachedData.ToList(), granularity);

                cachedData.First().OpenTime.Should().Be(i % 2 == 0 ? b : b.Add(-0.5 * granularitySpan),
                    "because this is supposed to be the beginning of the requested interval");

                cachedData.Last().CloseTime.AddSeconds(BinanceConstants.KLineTimeGapSec).Should()
                    .Be(j % 2 == 0 ? e : e.Add(0.5 * granularitySpan),
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

                gapIndicator.Should()
                    .NotBeNull("because we need to have some indication on what data to request from server.");

                var existingSubIntervals =
                    new TimeInterval(b, e).Subtract(gapIndicator).Where(x => !x.IsEmpty()).ToArray();

                existingSubIntervals.All(x => grid.Retrieve(x, out _) != null).Should()
                    .BeTrue("because this is how data from the gap-indicator should be interpreted");
            }
        }
    }

    /// <summary>
    /// Returns grid and a pair of non-intersecting blocks the grid is composed of.
    /// The blocks have a non-zero length gap between them. The first block is chronologically
    /// preceding to the last one.
    /// </summary>
    private static (BlockGrid Grid, IKLineBlockReadOnly Block0, IKLineBlockReadOnly Block1)
        CreateStandardGridWithTwoDistinctBlocks()
    {
        var beginTime = new DateTime(2000, 1, 1);
        var granularity = KlineInterval.OneMinute;
        var block0 = KLineGenerator.GenerateBlock(beginTime, granularity, 15);
        var block1 = KLineGenerator.GenerateBlock(block0.End.Add(5 * granularity.ToTimeSpan()), granularity, 22);

        block0.Intersects(block1).Should().BeFalse("because this is how the blocks are designed");
        block0.CanBeMergedWith(block1).Should().BeFalse("because this is how the blocks are designed");

        var grid = new BlockGrid(KLineGenerator.ToTimeGranularity(granularity));
        grid.Append(block0.Data);
        grid.Append(block1.Data);

        return (grid, block0, block1);
    }

    [TestMethod]
    public void GridWithTwoDistinctBlocksTest()
    {
        // Arrange
        var (grid, block0, block1) = CreateStandardGridWithTwoDistinctBlocks();

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

        var grid = new BlockGrid(KLineGenerator.ToTimeGranularity(granularity));
        grid.Append(separateStandingBlockToTheLeft.Data);
        grid.Append(adjacentBlock1.Data);
        grid.Append(adjacentBlock2.Data);
        grid.Append(adjacentBlock0.Data);
        grid.Append(separateStandingBlockToTheRight.Data);

        var mergedAdjacentBlocks = adjacentBlock0.Copy().MergeOverwrite(adjacentBlock1).MergeOverwrite(adjacentBlock2);

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

        var grid = new BlockGrid(KLineGenerator.ToTimeGranularity(granularity));
        grid.Append(block0.Data);
        grid.Append(block1.Data);
        grid.Append(block2.Data);

        RunExtensiveDataRetrievalTest([block0.Copy().MergeOverwrite(block1).MergeOverwrite(block2)], grid);
    }

    /// <summary>
    /// Constructs a grid out of the two given blocks with <param name="block1"/> being added the last
    /// and ensures that data from <param name="block1"/> overrides the corresponding data in the grid.
    /// </summary>
    private static BlockGrid CheckThatAppendedBlockOverridesExistingDataInTheGrid(KLineBlock block0, KLineBlock block1)
    {
        var grid = new BlockGrid(block0.Granularity);
        grid.Append(block0.Data);
        grid.Append(block1.Data);

        var data = grid.Retrieve(block1.Begin, block1.End, out _);
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

    [TestMethod]
    public void GridRefinementTest()
    {
        // Arrange
        var (grid, block0, block1) = CreateStandardGridWithTwoDistinctBlocks();
        grid.Blocks.Should().HaveCount(2, "by design");
        block0.KlineCount.Should().Be(15, "by design");
        block1.KlineCount.Should().Be(22, "by design");

        // Act
        var targetBlockGranularity = 5;
        grid.Refine(targetBlockGranularity);

        grid.Blocks.Should().HaveCount(block0.KlineCount / targetBlockGranularity +
                                       block1.KlineCount / targetBlockGranularity,
            "because this is how refinement procedure is supposed to work");

        // Run the general test to ensure that the grid is not altered
        // in the sense of data that can be retrieved out of it
        RunExtensiveDataRetrievalTest([block0, block1], grid);
    }

    /// <summary>
    /// Returns "middle" time point of the given interval.
    /// </summary>
    private static DateTime MiddlePoint(IKLineBlockReadOnly block) => block.Begin.Add(0.5 * (block.End - block.Begin));

    [TestMethod]
    public void GapIndicatorTest()
    {
        // Arrange
        var (grid, block0, block1) = CreateStandardGridWithTwoDistinctBlocks();
        grid.Refine(3);
        grid.Blocks.Should().HaveCount(12, "because this is how many blocks we should get after the refinement");
        var step = block0.Granularity.Span;
        var pointBetweenBlocks = block0.End.Add(0.5 * (block1.Begin - block0.End));

        // Act/Assert

        // Scenario 0 : the requested interval can't be retrieved
        // and there are multiple gaps that are missing in the grid
        foreach (var interval in new[]
                 {
                     new TimeInterval(block0.Begin.Subtract(step), block1.End.Add(step)),
                     new TimeInterval(block0.Begin.Subtract(step), pointBetweenBlocks),
                     new TimeInterval(pointBetweenBlocks, block1.End.Add(step)),
                     new TimeInterval(block0.End, block1.Begin),
                     new TimeInterval(block0.Begin.Subtract(step), block0.Begin),
                     new TimeInterval(block1.End, block1.End.Add(step)),
                 })
        {
            grid.Retrieve(interval, out var indicator).Should().
                BeNull("because this interval can't be retrieved by design (scenario 0)");
            indicator.Begin.Should().Be(DateTime.MinValue, "because the gap does not have a support from the left");
            indicator.End.Should().Be(DateTime.MaxValue, "because gap does not have a support from the right");
        }

        // Scenario 1 : the requested interval can't be retrieved
        // and there is a single interval with strictly defined right boundary that is missing.
        foreach (var data in new (TimeInterval Interval, DateTime ExpectedIndicatorPoint)[]
                 {
                     (new TimeInterval(block0.Begin.Subtract(10 * step), MiddlePoint(block0)), block0.Begin),
                     (new TimeInterval(pointBetweenBlocks, MiddlePoint(block1)), block1.Begin),
                     (new TimeInterval(block0.End, MiddlePoint(block1)), block1.Begin),
                     (new TimeInterval(block0.End, block1.End), block1.Begin),
                     (new TimeInterval(block0.Begin.Subtract(10 * step), MiddlePoint(block1)), block1.Begin),
                 })
        {
            grid.Retrieve(data.Interval, out var indicator).Should().
                BeNull("because this interval can't be retrieved by design (scenario 1)");
            indicator.Begin.Should().Be(DateTime.MinValue, "because the gap does not have support from the left");
            indicator.End.Should().Be(data.ExpectedIndicatorPoint, "because this is the expected support point from the right");
        }

        // Scenario 2 : the requested interval can't be retrieved
        // and there is a single interval with strictly defined left boundary that is missing.
        foreach (var data in new (TimeInterval Interval, DateTime ExpectedIndicatorPoint)[]
                 {
                     (new TimeInterval(MiddlePoint(block1), block1.End.Add(10 * step)), block1.End),
                     (new TimeInterval(MiddlePoint(block0), block1.End.Add(10 * step)), block0.End),
                     (new TimeInterval(MiddlePoint(block0), pointBetweenBlocks), block0.End),
                     (new TimeInterval(MiddlePoint(block0), block1.Begin), block0.End),
                     (new TimeInterval(block0.Begin, block1.Begin), block0.End),
                 })
        {
            grid.Retrieve(data.Interval, out var indicator).Should().
                BeNull("because this interval can't be retrieved by design (scenario 2)");
            indicator.Begin.Should().Be(data.ExpectedIndicatorPoint, "because this is the expected support point from the left");
            indicator.End.Should().Be(DateTime.MaxValue, "because the gap does not have support from the right");
        }

        // Scenario 3 : the requested interval can't be retrieved
        // and there is a single interval with strictly defined both left and right boundaries that is missing.
        foreach (var data in new (TimeInterval Interval,
                     DateTime ExpectedLeftIndicatorPoint, DateTime ExpectedRightIndicatorPoint)[]
                     {
                         (new TimeInterval(MiddlePoint(block0), MiddlePoint(block1)), block0.End, block1.Begin),
                         (new TimeInterval(block0.Begin, block1.End), block0.End, block1.Begin),
                         (new TimeInterval(MiddlePoint(block0), block1.End), block0.End, block1.Begin),
                         (new TimeInterval(block0.Begin, MiddlePoint(block1)), block0.End, block1.Begin),
                     })
        {
            grid.Retrieve(data.Interval, out var indicator).Should().
                BeNull("because this interval can't be retrieved by design (scenario 3)");
            indicator.Begin.Should().Be(data.ExpectedLeftIndicatorPoint, "because this is the expected support point from the left");
            indicator.End.Should().Be(data.ExpectedRightIndicatorPoint, "because this is the expected support point from the right");
        }
    }
}