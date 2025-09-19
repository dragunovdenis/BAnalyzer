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

using BAnalyzerCore.DataConversionUtils;
using System.Runtime.Serialization;
using BAnalyzer.Utils;

namespace BAnalyzerCore.DataStructures;

/// <summary>
/// Implementation of the corresponding interface.
/// </summary>
[DataContract]
public record struct TimeGranularity
{
    /// <summary>
    /// Human-readable string representation that can be used to display the granularity.
    /// </summary>
    public override string ToString() => Name;

    /// <summary>
    /// Human-readable name.
    /// </summary>
    [DataMember]
    public string Name { get; private set; }

    /// <summary>
    /// Representation of the time granularity in seconds.
    /// </summary>
    [DataMember]
    public int Seconds { get; private set; }

    /// <summary>
    /// TimeSpan representation.
    /// </summary>
    public TimeSpan Span => TimeSpan.FromSeconds(Seconds);

    /// <summary>
    /// Constructor.
    /// </summary>
    public TimeGranularity(string name, int sec)
    {
        Seconds = sec;
        Name = name;
    }

    private const int SecondsInMonth = 60 * 60 * 24 * 30;

    /// <summary>
    /// Returns "true" if the current instance represents a "month" granularity.
    /// </summary>
    public bool IsMonth => Seconds == SecondsInMonth;

    /// <summary>
    /// Returns base-64 string representation of the current instance.
    /// </summary>
    /// <returns></returns>
    public string Encode() => $"{Name}_{Seconds.ToBase64String()}";

    /// <summary>
    /// Returns "true" if the current instance is valid.
    /// </summary>
    public bool IsValid => Seconds > 0 && !Name.IsNullOrEmpty();

    /// <summary>
    /// Returns an invalid instance.
    /// </summary>
    public static TimeGranularity Invalid => new(null, -1);

    /// <summary>
    /// Decodes the given string representation <paramref name="s"/> into a "granularity" instance.
    /// </summary>
    public static TimeGranularity Decode(string s)
    {
        var pieces = s.Split('_');

        if (pieces.Length != 2)
            return Invalid;

        try
        {
            return new TimeGranularity(pieces[0], pieces[1].FromBase64String<int>());
        }
        catch (Exception)
        {
            return Invalid;
        }
    }
}