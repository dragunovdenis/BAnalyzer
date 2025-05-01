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

using BAnalyzerCore.Cache;
using Binance.Net.Enums;
using FluentAssertions;
using BAnalyzerCore;

namespace BAnalyzerCoreTest;

[TestClass]
public class KLineBlockTest
{
    /// <summary>
    /// Different types of block data used in the tests.
    /// </summary>
    public enum MergeDataType : int
    {
        DistinctBlocks = 0,
        AdjacentBlocks = 1,
        IntersectingBlocks = 2,
        ContainingBlocks = 3,
        CoincidingBlocks = 4,
        EmptyAndNonEmptyBlocks = 5,
        TwoEmptyBlocks = 6,
    }

    /// <summary>
    /// A data factory method;
    /// </summary>
    private static (KLineBlock Block0, KLineBlock Block1) CreateData(MergeDataType dataType)
    {
        switch (dataType)
        {
            case MergeDataType.DistinctBlocks : return CreateDistinctBlocks();
            case MergeDataType.AdjacentBlocks : return CreateAdjacentBlocks();
            case MergeDataType.IntersectingBlocks : return CreateIntersectingBlocks();
            case MergeDataType.ContainingBlocks : return CreateContainingBlocks();
            case MergeDataType.CoincidingBlocks: return CreateCoincidingBlocks();
            case MergeDataType.EmptyAndNonEmptyBlocks : return CreateEmptyAndNonEmptyBlocks();
            case MergeDataType.TwoEmptyBlocks : return CreateEmptyBlocks();
        }

        throw new ArgumentException("Unknown data type");
    }

    /// <summary>
    /// Returns a pair of non-intersecting, non-adjacent blocks of the same granularity.
    /// The first block is chronologically preceding to the second one.
    /// </summary>
    private static (KLineBlock Block0, KLineBlock Block1) CreateDistinctBlocks()
    {
        var beginTime = new DateTime(2000, 1, 1);
        var granularity = KlineInterval.OneMinute;
        var block0 = KLineGenerator.GenerateBlock(beginTime, granularity, 100);
        var block1 = KLineGenerator.GenerateBlock(block0.End.AddMinutes(2), granularity, 150);

        return (block0, block1);
    }

    /// <summary>
    /// Returns a pair of non-intersecting, non-adjacent blocks of the same granularity.
    /// The first block is chronologically preceding to the second one.
    /// </summary>
    private static (KLineBlock Block0, KLineBlock Block1) CreateAdjacentBlocks()
    {
        var beginTime = new DateTime(2000, 1, 1);
        var granularity = KlineInterval.OneMinute;
        var block0 = KLineGenerator.GenerateBlock(beginTime, granularity, 100);
        var block1 = KLineGenerator.GenerateBlock(block0.End, granularity, 150);

        return (block0, block1);
    }

    /// <summary>
    /// Returns a pair of non-coinciding, non-containing, intersecting blocks.
    /// The first block is chronologically preceding to the second one.
    /// </summary>
    private static (KLineBlock Block0, KLineBlock Block1) CreateIntersectingBlocks()
    {
        var beginTime = new DateTime(2000, 1, 1);
        var granularity = KlineInterval.OneMinute;
        var block0 = KLineGenerator.GenerateBlock(beginTime, granularity, 100);
        var block1 = KLineGenerator.GenerateBlock(beginTime.AddMinutes(50), granularity, 150);

        return (block0, block1);
    }

    /// <summary>
    /// Returns a pair of blocks with the first one containing the second one but not vice versa.
    /// </summary>
    private static (KLineBlock Block0, KLineBlock Block1) CreateContainingBlocks()
    {
        var beginTime = new DateTime(2000, 1, 1);
        var granularity = KlineInterval.OneMinute;
        var block0 = KLineGenerator.GenerateBlock(beginTime, granularity, 100);
        var block1 = KLineGenerator.GenerateBlock(beginTime.AddMinutes(10), granularity, 80);

        return (block0, block1);
    }

    /// <summary>
    /// Returns a pair of blocks that coincide.
    /// </summary>
    private static (KLineBlock Block0, KLineBlock Block1) CreateCoincidingBlocks()
    {
        var beginTime = new DateTime(2000, 1, 1);
        var granularity = KlineInterval.OneMinute;
        var block0 = KLineGenerator.GenerateBlock(beginTime, granularity, 100);
        var block1 = KLineGenerator.GenerateBlock(beginTime, granularity, 100);

        return (block0, block1);
    }

    /// <summary>
    /// Returns a pair of blocks one of which is empty and another one is non-empty.
    /// </summary>
    private static (KLineBlock Block0, KLineBlock Block1) CreateEmptyAndNonEmptyBlocks()
    {
        var beginTime = new DateTime(2000, 1, 1);
        var granularity = KlineInterval.OneMinute;
        var block0 = new KLineBlock(granularity);
        var block1 = KLineGenerator.GenerateBlock(beginTime, granularity, 100);

        return (block0, block1);
    }

    /// <summary>
    /// Returns a pair of empty blocks of the same granularity.
    /// </summary>
    private static (KLineBlock Block0, KLineBlock Block1) CreateEmptyBlocks()
    {
        var beginTime = new DateTime(2000, 1, 1);
        var granularity = KlineInterval.OneMinute;
        var block0 = new KLineBlock(granularity);
        var block1 = new KLineBlock(granularity);

        return (block0, block1);
    }

    [TestMethod]
    public void BlockContainsRelationTest()
    {
        // Arrange
        var coincidingBlocks = CreateCoincidingBlocks();
        var containingBlocks = CreateContainingBlocks();
        var adjacentBlocks = CreateAdjacentBlocks();
        var createDistinctBlocks = CreateDistinctBlocks();
        var intersectingBlocks = CreateIntersectingBlocks();

        // Assert
        coincidingBlocks.Block0.Contains(coincidingBlocks.Block1).Should()
            .BeTrue("because blocks that coincide contain each other");

        coincidingBlocks.Block1.Contains(coincidingBlocks.Block0).Should()
            .BeTrue("because blocks that coincide contain each other");

        containingBlocks.Block0.Contains(containingBlocks.Block1).Should()
            .BeTrue("because the first block contains the second one (by design)");

        containingBlocks.Block1.Contains(containingBlocks.Block0).Should()
            .BeFalse("because the second block is the right part of the first one (by design)");

        adjacentBlocks.Block0.Contains(adjacentBlocks.Block1).Should()
            .BeFalse("because adjacent blocks can't contain each other");

        adjacentBlocks.Block1.Contains(adjacentBlocks.Block0).Should()
            .BeFalse("because adjacent blocks can't contain each other");

        createDistinctBlocks.Block0.Contains(createDistinctBlocks.Block1).Should()
            .BeFalse("because distinct blocks can't contain each other");

        createDistinctBlocks.Block1.Contains(createDistinctBlocks.Block0).Should()
            .BeFalse("because distinct blocks can't contain each other");

        intersectingBlocks.Block0.Contains(intersectingBlocks.Block1).Should()
            .BeFalse("because both intersecting blocks have not-shared parts");

        intersectingBlocks.Block1.Contains(intersectingBlocks.Block0).Should()
            .BeFalse("because both intersecting blocks have not-shared parts");
    }

    [TestMethod]
    public void BlockIntersectRelationTest()
    {
        // Arrange
        var coincidingBlocks = CreateCoincidingBlocks();
        var containingBlocks = CreateContainingBlocks();
        var adjacentBlocks = CreateAdjacentBlocks();
        var createDistinctBlocks = CreateDistinctBlocks();
        var intersectingBlocks = CreateIntersectingBlocks();

        // Assert
        coincidingBlocks.Block0.Intersects(coincidingBlocks.Block1).Should()
            .BeTrue("because blocks that coincide intersect each other");

        coincidingBlocks.Block1.Intersects(coincidingBlocks.Block0).Should()
            .BeTrue("because blocks that coincide intersect each other");

        containingBlocks.Block0.Intersects(containingBlocks.Block1).Should()
            .BeTrue("because the first block contains the second one (by design)");

        containingBlocks.Block1.Intersects(containingBlocks.Block0).Should()
            .BeTrue("because the second block is the right part of the first one (by design)");

        adjacentBlocks.Block0.Intersects(adjacentBlocks.Block1).Should()
            .BeFalse("because adjacent blocks can't intersect each other");

        adjacentBlocks.Block1.Intersects(adjacentBlocks.Block0).Should()
            .BeFalse("because adjacent blocks can't intersect each other");

        createDistinctBlocks.Block0.Intersects(createDistinctBlocks.Block1).Should()
            .BeFalse("because distinct blocks can't intersect each other");

        createDistinctBlocks.Block1.Intersects(createDistinctBlocks.Block0).Should()
            .BeFalse("because distinct blocks can't intersect each other");

        intersectingBlocks.Block0.Intersects(intersectingBlocks.Block1).Should()
            .BeTrue("because the two blocks intersect by design");

        intersectingBlocks.Block1.Intersects(intersectingBlocks.Block0).Should()
            .BeTrue("because the two blocks intersect by design");
    }

    [TestMethod]
    public void BlockAdjacentRelationTest()
    {
        // Arrange
        var coincidingBlocks = CreateCoincidingBlocks();
        var containingBlocks = CreateContainingBlocks();
        var adjacentBlocks = CreateAdjacentBlocks();
        var createDistinctBlocks = CreateDistinctBlocks();
        var intersectingBlocks = CreateIntersectingBlocks();

        // Assert
        coincidingBlocks.Block0.IsAdjacentTo(coincidingBlocks.Block1).Should()
            .BeFalse("because blocks that coincide can't be adjacent");

        coincidingBlocks.Block1.IsAdjacentTo(coincidingBlocks.Block0).Should()
            .BeFalse("because blocks that coincide can't be adjacent");

        containingBlocks.Block0.IsAdjacentTo(containingBlocks.Block1).Should()
            .BeFalse("because the first block contains the second one (by design) and they can't be adjacent");

        containingBlocks.Block1.IsAdjacentTo(containingBlocks.Block0).Should()
            .BeFalse("because the second block is the right part of the first one (by design) and they can't be adjacent");

        adjacentBlocks.Block0.IsAdjacentTo(adjacentBlocks.Block1).Should()
            .BeTrue("because the two blocks are adjacent by design");

        adjacentBlocks.Block1.IsAdjacentTo(adjacentBlocks.Block0).Should()
            .BeTrue("because the two blocks are adjacent by design");

        adjacentBlocks.Block0.IsAdjacentAndFollowingTo(adjacentBlocks.Block1).Should()
            .BeFalse("because the first block is chronologically preceding to the second one");

        adjacentBlocks.Block0.IsAdjacentAndPrecedingTo(adjacentBlocks.Block1).Should()
            .BeTrue("because the first block is chronologically preceding to the second one");

        adjacentBlocks.Block1.IsAdjacentAndFollowingTo(adjacentBlocks.Block0).Should()
            .BeTrue("because the first block is chronologically preceding to the second one");

        adjacentBlocks.Block1.IsAdjacentAndPrecedingTo(adjacentBlocks.Block0).Should()
            .BeFalse("because the first block is chronologically preceding to the second one");

        createDistinctBlocks.Block0.IsAdjacentTo(createDistinctBlocks.Block1).Should()
            .BeFalse("because distinct blocks can't be adjacent");

        createDistinctBlocks.Block1.IsAdjacentTo(createDistinctBlocks.Block0).Should()
            .BeFalse("because distinct blocks can't be adjacent");

        intersectingBlocks.Block0.IsAdjacentTo(intersectingBlocks.Block1).Should()
            .BeFalse("because intersecting blocks can't be adjacent");

        intersectingBlocks.Block1.IsAdjacentTo(intersectingBlocks.Block0).Should()
            .BeFalse("because intersecting blocks can't be adjacent");
    }

    /// <summary>
    /// Runs general merging tests for the given pair of non-distinct sets.
    /// </summary>
    private static void RunMergeOverwriteTest(KLineBlock block0, KLineBlock block1)
    {
        // Act
        var mergedBlock = block0.Copy().MergeOverwrite(block1);

        // Assert
        mergedBlock.IsValid().Should().BeTrue("because the merged block should be valid");

        mergedBlock.Contains(block0).Should()
            .BeTrue("because the result of merging operation is supposed to contain each of the operands");

        mergedBlock.Contains(block1).Should()
            .BeTrue("because the result of merging operation is supposed to contain each of the operands");

        // Check that all the items from "block1" (i.e., the one which was merged)
        // are present in the merging result. This is the contract obligation
        // of the merging procedure.
        var block1StartId = mergedBlock.FindBeginKLineId(block1.Begin);
        for (var itemId = 0; itemId < block1.Data.Count; itemId++)
            mergedBlock.Data[itemId + block1StartId].Equals(block1.Data[itemId]).Should()
                .BeTrue("because this is the requirement of the merge contract");
    }

    [DataRow(MergeDataType.AdjacentBlocks)]
    [DataRow(MergeDataType.CoincidingBlocks)]
    [DataRow(MergeDataType.IntersectingBlocks)]
    [DataRow(MergeDataType.ContainingBlocks)]
    [DataRow(MergeDataType.EmptyAndNonEmptyBlocks)]
    [DataRow(MergeDataType.TwoEmptyBlocks)]
    [DataTestMethod]
    public void MergeOverwriteTest(MergeDataType dataType)
    {
        // Arrange
        var (block0, block1) = CreateData(dataType);

        // Act
        RunMergeOverwriteTest(block0, block1);
        RunMergeOverwriteTest(block1, block0);
    }

    [TestMethod]
    public void MergeOverwriteOfDistinctBlocksTest()
    {
        // Arrange
        var (block0, block1) = CreateDistinctBlocks();

        // Act/assert
        block0.Invoking(x => x.MergeOverwrite(block1)).Should().
            ThrowExactly<InvalidOperationException>("because distinct blocks can't be merged");

        block1.Invoking(x => x.MergeOverwrite(block0)).Should().
            ThrowExactly<InvalidOperationException>("because distinct blocks can't be merged");
    }

    /// <summary>
    /// Runs general merging tests for the given pair of non-distinct sets.
    /// </summary>
    private static void RunMergePreserveTest(KLineBlock block0, KLineBlock block1)
    {
        // Act
        var mergedBlock = block0.Copy().MergePreserve(block1);

        // Assert
        mergedBlock.IsValid().Should().BeTrue("because the merged block should be valid");

        mergedBlock.Contains(block0).Should()
            .BeTrue("because the result of merging operation is supposed to contain each of the operands");

        mergedBlock.Contains(block1).Should()
            .BeTrue("because the result of merging operation is supposed to contain each of the operands");

        // Check that all the items from "block0" (i.e., the one which was merged)
        // are present in the merging result. This is the contract obligation
        // of the merging procedure.
        var block0StartId = mergedBlock.FindBeginKLineId(block0.Begin);
        for (var itemId = 0; itemId < block0.Data.Count; itemId++)
            mergedBlock.Data[itemId + block0StartId].Equals(block0.Data[itemId]).Should()
                .BeTrue("because this is the requirement of the merge contract");
    }

    [DataRow(MergeDataType.AdjacentBlocks)]
    [DataRow(MergeDataType.CoincidingBlocks)]
    [DataRow(MergeDataType.IntersectingBlocks)]
    [DataRow(MergeDataType.ContainingBlocks)]
    [DataRow(MergeDataType.EmptyAndNonEmptyBlocks)]
    [DataRow(MergeDataType.TwoEmptyBlocks)]
    [DataTestMethod]
    public void MergePreserveTest(MergeDataType dataType)
    {
        // Arrange
        var (block0, block1) = CreateData(dataType);

        // Act
        RunMergePreserveTest(block0, block1);
        RunMergePreserveTest(block1, block0);
    }

    [TestMethod]
    public void MergePreserveOfDistinctBlocksTest()
    {
        // Arrange
        var (block0, block1) = CreateDistinctBlocks();

        // Act/assert
        block0.Invoking(x => x.MergePreserve(block1)).Should().
            ThrowExactly<InvalidOperationException>("because distinct blocks can't be merged");

        block1.Invoking(x => x.MergePreserve(block0)).Should().
            ThrowExactly<InvalidOperationException>("because distinct blocks can't be merged");
    }

    [TestMethod]
    public void SubtractionOfCoincidingBlocks()
    {
        // Arrange
        var (block0, block1) = CreateCoincidingBlocks();

        // Act 
        var block0WithoutBlock1 = block0.Copy().Subtract(block1);
        var block1WithoutBlock0 = block1.Copy().Subtract(block0);

        // Assert
        block0WithoutBlock1.IsValid().Should().
            BeTrue("because result of subtraction should be a valid block");

        block1WithoutBlock0.IsValid().Should().
            BeTrue("because result of subtraction should be a valid block");

        block0WithoutBlock1.IsEmpty().Should().BeTrue("because the two blocks coincide");
        block1WithoutBlock0.IsEmpty().Should().BeTrue("because the two blocks coincide");
    }

    [TestMethod]
    public void SubtractionOfContainingBlocks()
    {
        // Arrange
        var (block0, block1) = CreateContainingBlocks();

        // Act 
        block0.Copy().Invoking(x =>x.Subtract(block1)).
            Should().ThrowExactly<InvalidOperationException>("because such a subtraction would result in a discontinuous block");

        var block1WithoutBlock0 = block1.Copy().Subtract(block0);

        // Assert
        block1WithoutBlock0.IsValid().Should().
            BeTrue("because result of subtraction should be a valid block");

        block1WithoutBlock0.IsEmpty().Should().BeTrue("because the first block contains the second one");
    }

    [TestMethod]
    public void SubtractionOfIntersectingBlocks()
    {
        // Arrange
        var (block0, block1) = CreateIntersectingBlocks();

        // Act 
        var block0WithoutBlock1 = block0.Copy().Subtract(block1);
        var block1WithoutBlock0 = block1.Copy().Subtract(block0);

        // Assert
        block0WithoutBlock1.IsValid().Should().
            BeTrue("because result of subtraction should be a valid block");

        block1WithoutBlock0.IsValid().Should().
            BeTrue("because result of subtraction should be a valid block");

        block0WithoutBlock1.IsAdjacentAndPrecedingTo(block1).Should()
            .BeTrue("because the two blocks are non-distinct and ordered chronologically");

        block0WithoutBlock1.Begin.Should().Be(block0.Begin,
            "because the leftmost part of block \"0\" is not intersected by block \"1\" (by design)");

        block1WithoutBlock0.IsAdjacentAndFollowingTo(block0).Should()
            .BeTrue("because the two blocks are non-distinct and ordered chronologically");

        block1WithoutBlock0.End.Should().Be(block1.End,
            "because the rightmost part of block \"1\" is not intersected by block \"0\"");
    }

    /// <summary>
    /// Runs trivial subtraction (no subtraction) tests of the given pair of
    /// non-intersecting
    /// </summary>
    private static void RunTrivialSubtractionTest(KLineBlock block0, KLineBlock block1)
    {
        // Act 
        var block0WithoutBlock1 = block0.Copy().Subtract(block1);
        var block1WithoutBlock0 = block1.Copy().Subtract(block0);

        // Assert
        block0WithoutBlock1.IsValid().Should().
            BeTrue("because result of subtraction should be a valid block");

        block1WithoutBlock0.IsValid().Should().
            BeTrue("because result of subtraction should be a valid block");

        block0WithoutBlock1.Coincide(block0).Should()
            .BeTrue("because the two blocks do not intersect");

        block1WithoutBlock0.Coincide(block1).Should()
            .BeTrue("because the two blocks do not intersect");
    }

    [DataRow(MergeDataType.AdjacentBlocks)]
    [DataRow(MergeDataType.DistinctBlocks)]
    [DataRow(MergeDataType.EmptyAndNonEmptyBlocks)]
    [DataRow(MergeDataType.TwoEmptyBlocks)]
    [DataTestMethod]
    public void BlockSubtractionTest(MergeDataType dataType)
    {
        // Arrange
        var (block0, block1) = CreateData(dataType);

        // Act/assert
        RunTrivialSubtractionTest(block0, block1);
        RunTrivialSubtractionTest(block1, block0);
    }
}