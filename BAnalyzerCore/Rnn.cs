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

namespace BAnalyzerCore;

/// <summary>
/// Wrapper of an instance of a native recurrent neural net.
/// </summary>
public class Rnn : IDisposable
{
    private IntPtr _rnnPtr;

    /// <summary>
    /// Constructor.
    /// </summary>
    public Rnn(int timeDepth, int[] layerItemSizes)
    {
        _rnnPtr = NativeDllWrapper.RnnConstruct(timeDepth, layerItemSizes.Length, layerItemSizes);

        if (_rnnPtr == IntPtr.Zero)
            throw new Exception("Failed to instantiate an RNN");
    }

    /// <summary>
    /// Finalizer.
    /// </summary>
    ~Rnn() => Dispose();

    /// <summary>
    /// Returns size of an input item of the RNN.
    /// </summary>
    public int InputItemSize => NativeDllWrapper.RnnGetInputItemSize(_rnnPtr);

    /// <summary>
    /// Returns size of an output item of the RNN.
    /// </summary>
    public int OutputItemSize => NativeDllWrapper.RnnGetOutputItemSize(_rnnPtr);

    /// <summary>
    /// Returns number of (actual) layer in the RNN.
    /// </summary>
    public int LayerCount => NativeDllWrapper.RnnGetLayerCount(_rnnPtr);

    /// <summary>
    /// Returns recurrence depth of the RNN.
    /// </summary>
    public int Depth => NativeDllWrapper.RnnGetDepth(_rnnPtr);

    /// <summary>
    /// Returns result of evaluation of the RNN at the given <param name="input"/>.
    /// </summary>
    public double[] Evaluate(double[] input)
    {
        double[] result = null;

        if (!NativeDllWrapper.RnnEvaluate(_rnnPtr, input.Length, input,
                (_, r) => { result = r; }))
            return null;

        return result;
    }

    /// <summary>
    /// Runs a single batch-training iteration on the given data.
    /// </summary>
    public bool Train(double[] input, double[] reference, double learningRate)
    {
        return NativeDllWrapper.RnnBatchTrain(_rnnPtr, input.Length,
            input, reference.Length, reference, learningRate);
    }

    /// <summary>
    /// Returns "true" if the native DLL is compiled against "single" precision arithmetics.
    /// </summary>
    public bool SinglePrecision => NativeDllWrapper.IsSinglePrecision();

    /// <summary>
    /// Disposes the current instance.
    /// </summary>
    public void Dispose()
    {
        if (_rnnPtr == IntPtr.Zero) return;

        if (!NativeDllWrapper.RnnFree(_rnnPtr))
            throw new Exception("Failed to dispose an RNN");

        _rnnPtr = IntPtr.Zero;
        GC.SuppressFinalize(this);
    }
}