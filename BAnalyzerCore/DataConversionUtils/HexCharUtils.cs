﻿//Copyright (c) 2025 Denys Dragunov, dragunovdenis@gmail.com
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

namespace BAnalyzerCore.DataConversionUtils;

/// <summary>
/// Utility methods for HEX string encoding/decoding operations.
/// </summary>
internal static class HexCharUtils
{
    /// <summary>
    /// Decodes the given HEX-char string into a custom array.
    /// </summary>
    public static T[] HexStringToArray<T>(this string hexStr)
        where T : struct =>
        string.IsNullOrEmpty(hexStr) ? [] : ByteConversionUtils.ToArray<T>(HexStringToByteArray(hexStr));

    /// <summary>
    /// Converts the given <paramref name="array"/> into a HEX-char string.
    /// </summary>
    public static string ToHexString<T>(this T[] array)
        where T : struct =>
        array == null ? string.Empty : ToHexString(ByteConversionUtils.ToByteArray(array));

    /// <summary>
    /// Converts given struct into a HEX-char string.
    /// </summary>
    public static string ToHexString<T>(this T value)
        where T : struct => ToHexString([value]);

    /// <summary>
    /// Converts the given HEX-char string into an array of bytes.
    /// </summary>
    public static byte[] HexStringToByteArray(this string hexString) => Convert.FromHexString(hexString);

    /// <summary>
    /// Converts the given array of bytes <paramref name="ba"/> into a HEX-char string.
    /// </summary>
    public static string ToHexString(this byte[] ba) => Convert.ToHexString(ba);
}