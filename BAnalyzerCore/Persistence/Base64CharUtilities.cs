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

namespace BAnalyzerCore.Persistence;

/// <summary>
/// Utility methods for converting byte arrays to the arrays of different types and vice versa
/// </summary>
internal static class Base64CharUtilities
{
    /// <summary>
    /// Decodes the given base-64 char string into a custom array.
    /// </summary>
    public static T[] ToArray<T>(string str)
        where T : struct =>
        string.IsNullOrEmpty(str) ? [] : ToArray<T>(ToByteArray(str));

    /// <summary>
    /// Converts the given <paramref name="array"/> into a base-64 char string.
    /// </summary>
    public static string ToString<T>(T[] array)
        where T : struct =>
        array == null ? string.Empty : ToString(ToByteArray(array));

    /// <summary>
    /// Converts given struct into a base-64 char string.
    /// </summary>
    public static string ToString<T>(T value)
        where T : struct =>
        ToString([value]);

    /// <summary>
    /// Converts given string into an instance of the given type <typeparamref name="T"/>.
    /// </summary>
    public static T FromString<T>(string str)
        where T : struct => ToArray<T>(str)[0];

    /// <summary>
    /// Converts the given base-64 char string into an array of bytes.
    /// </summary>
    public static byte[] ToByteArray(string base64CharString)
    {
        var charArray = base64CharString.ToCharArray();
        return Convert.FromBase64CharArray(charArray, 0, charArray.Length);
    }

    /// <summary>
    /// Converts the given array of bytes <paramref name="ba"/> into a base-64 char string.
    /// </summary>
    public static string ToString(byte[] ba) => Convert.ToBase64String(ba);

    /// <summary>
    /// Converts the given array of bytes <paramref name="bytes"/>
    /// into an array of given type <typeparamref name="T"/>.
    /// </summary>
    public static T[] ToArray<T>(byte[] bytes)
        where T : struct
    {
        var array = new T[bytes.Length / Marshal.SizeOf(typeof(T))];
        var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
        try
        {
            IntPtr pointer = handle.AddrOfPinnedObject();
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
            IntPtr pointer = handle.AddrOfPinnedObject();
            Marshal.Copy(pointer, bytes, 0, bytes.Length);
        }
        finally
        {
            handle.Free();
        }

        return bytes;
    }
}