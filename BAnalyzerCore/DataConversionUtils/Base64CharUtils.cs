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

namespace BAnalyzerCore.DataConversionUtils;

/// <summary>
/// Utility methods for base-64 char string encoding/decoding operations.
/// </summary>
internal static class Base64CharUtils
{
    /// <summary>
    /// Decodes the given base-64 char string into a custom array.
    /// </summary>
    public static T[] Base64StringToArray<T>(this string base64Str)
        where T : struct =>
        string.IsNullOrEmpty(base64Str) ? [] : ByteConversionUtils.ToArray<T>(Base64StringToByteArray(base64Str));

    /// <summary>
    /// Converts given base-64 char string into an instance of the given type <typeparamref name="T"/>.
    /// </summary>
    public static T FromBase64String<T>(this string base64Str) where T : struct => Base64StringToArray<T>(base64Str)[0];

    /// <summary>
    /// Converts the given base-64 char string into an array of bytes.
    /// </summary>
    public static byte[] Base64StringToByteArray(this string base64CharString) => Convert.FromBase64String(base64CharString);

    /// <summary>
    /// Converts the given array of bytes <paramref name="ba"/> into a base-64 char string.
    /// </summary>
    public static string ToBase64String(this byte[] ba) => Convert.ToBase64String(ba);

    /// <summary>
    /// Converts the given <paramref name="array"/> into a base-64 char string.
    /// </summary>
    public static string ToBase64String<T>(this T[] array)
        where T : struct =>
        array == null ? string.Empty : ToBase64String(ByteConversionUtils.ToByteArray(array));

    /// <summary>
    /// Converts given struct into a base-64 char string.
    /// </summary>
    public static string ToBase64String<T>(this T value)
        where T : struct => ToBase64String([value]);
}