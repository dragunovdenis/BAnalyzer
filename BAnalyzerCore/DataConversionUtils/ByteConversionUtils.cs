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

namespace BAnalyzerCore.DataConversionUtils;

/// <summary>
/// Functionality to marshall byte arrays to arrays of custom structs and back.
/// </summary>
internal static class ByteConversionUtils
{
    /// <summary>
    /// Converts the given array of bytes <paramref name="bytes"/>
    /// into an array of structs of type <typeparamref name="T"/>.
    /// </summary>
    public static T[] ToArray<T>(byte[] bytes)
        where T : struct
    {
        var array = new T[bytes.Length / Marshal.SizeOf(typeof(T))];
        var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
        try
        {
            var pointer = handle.AddrOfPinnedObject();
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
        }
        finally
        {
            handle.Free();
        }

        return array;
    }

    /// <summary>
    /// Converts the given array of structs <typeparamref name="T"/> into an array of bytes.
    /// </summary>
    public static byte[] ToByteArray<T>(T[] array)
        where T : struct
    {
        var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
        var bytes = new byte[array.Length * Marshal.SizeOf(typeof(T))];
        try
        {
            var pointer = handle.AddrOfPinnedObject();
            Marshal.Copy(pointer, bytes, 0, bytes.Length);
        }
        finally
        {
            handle.Free();
        }

        return bytes;
    }
}