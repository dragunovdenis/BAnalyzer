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

using System.Collections.Immutable;
using BAnalyzerCore;

namespace BAnalyzerCoreTest;

[TestClass]
public class RnnTest
{
    const int Depth = 15;
    private static readonly ImmutableArray<int> _itemSizes = [..new int[] { 4, 5, 7, 11 }];

    private static Rnn ConstructStandardRnn() => new Rnn(Depth, _itemSizes.ToArray());

    [TestMethod]
    public void ConstructionTest()
    {
        // Act
        var rnn = ConstructStandardRnn();

        // Assert
        Assert.AreEqual(rnn.Depth, Depth, "Unexpected depth of the instantiated RNN");
        Assert.AreEqual(_itemSizes.Length - 1, rnn.LayerCount,
            "Unexpected number of layers in the instantiated RNN");
        Assert.AreEqual(_itemSizes.First(), rnn.InputItemSize,
            "Unexpected size of the input item of the instantiated RNN");
        Assert.AreEqual(_itemSizes.Last(), rnn.OutputItemSize,
            "Unexpected size of the output item of the instantiated RNN");
    }

    private static Random _rnd = new();

    /// <summary>
    /// Returns a randomly generated collection that can serve as an input or output of the "standard" RNN.
    /// </summary>
    private static double[] GenerateRandomCollection(int itemSize) => Enumerable.Range(0, Depth)
        .SelectMany(_ => Enumerable.Range(0, itemSize).Select(_ => _rnd.NextDouble())).ToArray();

    /// <summary>
    /// Generates <param name="collectionCount"/> random collections.
    /// </summary>
    private static double[] GenerateRandomMultiCollection(int itemSize, int collectionCount) =>
        Enumerable.Range(0, collectionCount).SelectMany(_ => GenerateRandomCollection(itemSize)).ToArray();

    [TestMethod]
    public void EvaluationTest()
    {
        // Arrange
        var input = GenerateRandomCollection(_itemSizes.First());

        // Act
        var result = ConstructStandardRnn().Evaluate(input);

        // Assert
        Assert.IsNotNull(result, "Failed to evaluate RNN");
        Assert.AreEqual(result.Length, Depth * _itemSizes.Last(), "Unexpected size of evaluation result");
        Assert.IsFalse(result.All(x => x.Equals(0)), "Suspicious evaluation result");
    }

    /// <summary>
    /// Sigmoid function.
    /// </summary>
    private static double Sigmoid(double x) => 1 / (1 + Math.Exp(-x));

    /// <summary>
    /// Evaluates average and maximal absolute deviations between <param name="expectedOutput"/>
    /// and the result of <param name="net"/> evaluated on the given <param name="input"/>
    /// </summary>
    private static (double Average, double Max) CalcAverageAndMaxAbsDeviation(Rnn net, double[] input, double[] expectedOutput)
    {
        var singleItemSize = net.InputItemSize * net.Depth;
        var idx = 0;

        var outputsSplit = new List<double[]>();

        while (idx < input.Length)
        {
            var singleItem = input.Skip(idx).Take(singleItemSize).ToArray();

            var singleOutputItem = net.Evaluate(singleItem);

            Assert.IsNotNull(singleOutputItem, "Failed to evaluate net.");

            outputsSplit.Add(net.Evaluate(singleItem));
            idx += singleItemSize;
        }

        Assert.AreEqual(idx, input.Length, "Unexpected number of items in the input collection");

        var actualOutput = outputsSplit.SelectMany(x => x).ToArray();

        Assert.AreEqual(actualOutput.Length, expectedOutput.Length,
            "The collections 1must have the same length");

        var absDiffs = actualOutput.Zip(expectedOutput, (x, y) => Math.Abs(x - y)).ToArray();

        return (absDiffs.Average(), absDiffs.Max());
    }

    [TestMethod]
    public void IdentityTrainingTest()
    {
        // Arrange
        var itemSize = 5;
        var net = new Rnn(Depth, [5, 5]);
        var trainingIterations = 15000;
        var batchItemCount = 10;

        var inputControl = GenerateRandomMultiCollection(itemSize, batchItemCount);
        var outputControl = inputControl.Select(Sigmoid).ToArray();

        var (initialDeviationAverage, _) = CalcAverageAndMaxAbsDeviation(net, inputControl, outputControl);
        Assert.IsTrue(initialDeviationAverage > 0.2, "Too low initial deviation from reference.");

        for (var iterId = 0; iterId < trainingIterations; iterId++)
        {
            var input = GenerateRandomMultiCollection(itemSize, batchItemCount);
            var reference = input.Select(Sigmoid).ToArray();

            Assert.IsTrue(net.Train(input, reference, learningRate: 0.1),
                "Training iteration has failed.");
        }

        var (_, finalDeviationMax) = CalcAverageAndMaxAbsDeviation(net, inputControl, outputControl);

        Assert.IsTrue(finalDeviationMax < (net.SinglePrecision ? 1e-7 : 1e-10),
            "Too high final deviation from reference.");
    }
}