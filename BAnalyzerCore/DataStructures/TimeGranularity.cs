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

namespace BAnalyzerCore.DataStructures;

/// <summary>
/// General interface to represent time granularity of k-line charts.
/// </summary>
public interface ITimeGranularity
{
    /// <summary>
    /// Human-readable string representation that can be used to display the granularity.
    /// </summary>
    string ToString();

    /// <summary>
    /// Name of the current instance.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Nominal time-span the granularity amounts to.
    /// </summary>
    TimeSpan Span { get; }

    /// <summary>
    /// Ordering number (which is assumed to be equal to the amount of seconds in the time interval)
    /// </summary>
    int Seconds { get; }

    /// <summary>
    /// If "true" indicates that the instance represents the "month"
    /// time interval whose actual span can deviate from the nominal
    /// one for up to 2 days.
    /// </summary>
    bool IsMonth { get; }

    /// <summary>
    /// Encodes the current instance into its string representation.
    /// </summary>
    string Encode();
}

/// <summary>
/// Implementation of the corresponding interface.
/// </summary>
[DataContract]
public class TimeGranularity : ITimeGranularity
{
    /// <summary>
    /// Human-readable string representation that can be used to display the granularity.
    /// </summary>
    public override string ToString() => Name;

    /// <inheritdoc/>
    [DataMember]
    public string Name { get; private set; }

    /// <inheritdoc/>
    [DataMember]
    public int Seconds { get; private set; }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public bool IsMonth => Seconds == SecondsInMonth;

    /// <inheritdoc/>
    public string Encode() => $"{Name}_{Seconds.ToBase64String()}";

    /// <summary>
    /// Decodes the given string representation <paramref name="s"/> into a "granularity" instance.
    /// </summary>
    public static TimeGranularity Decode(string s)
    {
        var pieces = s.Split('_');

        if (pieces.Length != 2)
            return null;

        try
        {
            return new TimeGranularity(pieces[0], pieces[1].FromBase64String<int>());
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public override bool Equals(object other)
    {
        if (other is not TimeGranularity otherGranularity)
            return false;

        return Name == otherGranularity.Name && Seconds == otherGranularity.Seconds;
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Seconds);
}