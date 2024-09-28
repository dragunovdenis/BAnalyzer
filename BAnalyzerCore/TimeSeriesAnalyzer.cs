//Copyright (c) 2024 Denys Dragunov, dragunovdenis@gmail.com
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

using Microsoft.ML;

namespace BAnalyzerCore;

/// <summary>
/// Functionality for statistical analysis of time dependent data.
/// </summary>
public class TimeSeriesAnalyzer
{
    /// <summary>
    /// Input data (time series)
    /// </summary>
    public class Input
    {
        /// <summary>
        /// The data.
        /// </summary>
        public float InData;
    }

    /// <summary>
    /// Output data (prediction)
    /// </summary>
    public class Output
    {
        public double[] Prediction;
    }

    /// <summary>
    /// Creates instance of an output data view.
    /// </summary>
    private static IDataView CreateEmptyDataView(MLContext mlContext) =>
        mlContext.Data.LoadFromEnumerable(new List<Input>());

    /// <summary>
    /// Returns indices of the items in the given time series that correspond to "spikes".
    /// </summary>
    public static int[] DetectSpikes(IList<Input> input, int windowSize = 4)
    {
        var mlContext = new MLContext();
        var dataView = mlContext.Data.LoadFromEnumerable(input);
            
        var iidSpikeEstimator = mlContext.Transforms.DetectIidSpike(outputColumnName: nameof(Output.Prediction),
            inputColumnName: nameof(Input.InData), confidence: 95.0, pvalueHistoryLength: windowSize);

        var iidSpikeTransform = iidSpikeEstimator.Fit(CreateEmptyDataView(mlContext));

        var transformedData = iidSpikeTransform.Transform(dataView);

        var predictions = mlContext.Data.CreateEnumerable<Output>(transformedData, reuseRowObject: false);

        return predictions.Select((x, i) => x.Prediction[0] >= 0.9999 ? i : -1).
            Where(x => x >= 0).ToArray();
    }

    /// <summary>
    /// Returns indices of the items in the given time series that correspond to "spikes" (async version).
    /// </summary>
    public static async Task<int[]> DetectSpikesAsync(IList<Input> input, int windowSize = 4) =>
        await Task.Run(() => DetectSpikes(input, windowSize));

    /// <summary>
    /// Returns indices of the items in the given time series that correspond to "change points".
    /// </summary>
    public static int[] DetectChangePoints(IList<Input> input, int windowSize = 4)
    {
        var mlContext = new MLContext();
        var dataView = mlContext.Data.LoadFromEnumerable(input);

        var iidChangePointEstimator = mlContext.Transforms.DetectIidChangePoint(outputColumnName: nameof(Output.Prediction),
            inputColumnName: nameof(Input.InData), confidence: 95.0, changeHistoryLength: windowSize);

        var iidChangePointTransform = iidChangePointEstimator.Fit(CreateEmptyDataView(mlContext));
        var transformedData = iidChangePointTransform.Transform(dataView);
        var predictions = mlContext.Data.CreateEnumerable<Output>(transformedData, reuseRowObject: false);

        return predictions.Select((x, i) => x.Prediction[0] >= 0.9999 ? i : -1).
            Where(x => x >= 0).ToArray();
    }

    /// <summary>
    /// Returns indices of the items in the given time series that correspond to "change points" (async version).
    /// </summary>
    public static async Task<int[]> DetectChangePointsAsync(IList<Input> input, int windowSize = 4) =>
        await Task.Run(() => DetectChangePoints(input, windowSize));
}