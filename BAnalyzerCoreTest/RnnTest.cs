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
    const int Depth = 10;
    static readonly ImmutableArray<int> _itemSizes = [..new int[] { 4, 5, 7, 11 }];

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

    private static Random _rnd = new Random();

    /// <summary>
    /// Returns a randomly generated colelction that can serve as an input or output of the "standard" RNN.
    /// </summary>
    private static double[] GenerateRandomCollection(int itemSize) => Enumerable.Range(0, Depth)
        .SelectMany(_ => Enumerable.Range(0, itemSize).Select(_ => _rnd.NextDouble())).ToArray();

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

    [TestMethod]
    public void TrainingTest()
    {
        // Arrange
        var trainingItemCount = 10;
        var input = Enumerable.Range(0, trainingItemCount).
            SelectMany(_ => GenerateRandomCollection(_itemSizes.First())).ToArray();
        var reference = Enumerable.Range(0, trainingItemCount).
            SelectMany(_ => GenerateRandomCollection(_itemSizes.Last())).ToArray();

        // Act
        var result = ConstructStandardRnn().Train(input, reference, learningRate: 0.1);

        // Assert
        Assert.IsTrue(result, "Failed to train RNN");
    }
}