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

using System.Runtime.InteropServices;

namespace BAnalyzerCore;

/// <summary>
/// Wrapper methods for "BAnalyzerNativeDll.dll"
/// </summary>
internal static class NativeDllWrapper
{
    /// <summary>
    /// Returns pointer to an instance of RNN created according to the given set of parameters.
    /// Returns null pointer if something went wrong.
    /// </summary>
    /// <param name="timeDepth">Recursion depth of RNN.</param>
    /// <param name="layerItemSizesCount">Number of items in <param name="layerItemSizes"/> array.</param>
    /// <param name="layerItemSizes">Contains item sizes for all the layers of the neural
    /// net including the input one (which,technically is not actually present in the neural net).</param>
    [DllImport("BAnalyzerNativeDll.dll")]
    public static extern IntPtr RnnConstruct(int timeDepth,
        int layerItemSizesCount, 
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
        int[] layerItemSizes);

    /// <summary>
    /// Returns size of a single input time-point item of the RNN pointed by <paramref name="rnnPtr"/>.
    /// Returns "-1" size in case of failure.
    /// </summary>
    [DllImport("BAnalyzerNativeDll.dll")]
    public static extern int RnnGetInputItemSize(IntPtr rnnPtr);

    /// <summary>
    /// Returns size of a single output time-point item of the RNN pointed by <paramref name="rnnPtr"/>.
    /// Returns "-1" size in case of failure.
    /// </summary>
    [DllImport("BAnalyzerNativeDll.dll")]
    public static extern int RnnGetOutputItemSize(IntPtr rnnPtr);

    /// <summary>
    /// Returns number of layers in the RNN pointed by <paramref name="rnnPtr"/>.
    /// Returns "-1" size in case of failure.
    /// </summary>
    [DllImport("BAnalyzerNativeDll.dll")]
    public static extern int RnnGetLayerCount(IntPtr rnnPtr);

    /// <summary>
    /// Returns recurrence depth of the RNN pointed by the given <param name="rnnPtr"/>.
    /// Returns "-1" size in case of failure.
    /// </summary>
    [DllImport("BAnalyzerNativeDll.dll")]
    public static extern int RnnGetDepth(IntPtr rnnPtr);

    /// <summary>
    /// Delegate to retrieve arrays from the native code.
    /// </summary>
    public delegate void GetArrayCallBack(int size,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
        double[] array);

    /// <summary>
    /// Evaluates the RNN pointed to by the given <param name="rnnPtr"/> at the given <param name="input"/>.
    /// Returns "true" in case of success.
    /// </summary>
    /// <param name="rnnPtr">Pointer to an RNN.</param>
    /// <param name="size">Number of elements in <param name="input"/>.</param>
    /// <param name="input">Array of input elements.</param>
    /// <param name="getResultCallback">instance of a callback function to retrieve the result.</param>
    [DllImport("BAnalyzerNativeDll.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool RnnEvaluate(IntPtr rnnPtr,
        int size,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
        double[] input, GetArrayCallBack getResultCallback);

    /// <summary>
    /// Runs a single batch-training iteration on the given input and reference (labels) data.
    /// Returns "true" if succeeded.
    /// </summary>
    /// <param name="rnnPtr">Pointer to an RNN.</param>
    /// <param name="inAggregateSize">Number of elements in <param name="inputAggregate"/>.</param>
    /// <param name="inputAggregate">Collection representing input data to train the neural net on.</param>
    /// <param name="refAggregateSize">Number of elements in <param name="referenceAggregate"/></param>
    /// <param name="referenceAggregate">Collection representing "labels" to train the neural net on.</param>
    /// <param name="learningRate">Learning rate.</param>
    [DllImport("BAnalyzerNativeDll.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool RnnBatchTrain(IntPtr rnnPtr,
        int inAggregateSize,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
        double[] inputAggregate,
        int refAggregateSize,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
        double[] referenceAggregate, double learningRate);

    /// <summary>
    /// Destroys an instance of RNN pointed by the given <param name="rnnPtr"/>.
    /// Returns "true" if succeeded.
    /// </summary>
    /// <param name="rnnPtr"></param>
    /// <returns></returns>
    [DllImport("BAnalyzerNativeDll.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool RnnFree(IntPtr rnnPtr);

    /// <summary>
    /// Returns "true" if the DLL is compiled against "single" precision arithmetics.
    /// </summary>
    [DllImport("BAnalyzerNativeDll.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool IsSinglePrecision();
}