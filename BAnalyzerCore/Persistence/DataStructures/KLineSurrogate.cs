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

using BAnalyzerCore.DataStructures;

namespace BAnalyzerCore.Persistence.DataStructures;

/// <summary>
/// A "surrogate" data type to facilitate persistence of <see cref="BAnalyzerCore.DataStructures.KLine"/>
/// </summary>
internal struct KLineSurrogate
{
    /// <summary>
    /// Constructor.
    /// </summary>
    public KLineSurrogate(KLine source)
    {
        OpenTime = source.OpenTime;
        CloseTime = source.CloseTime;
        OpenPrice = source.OpenPrice;
        ClosePrice = source.ClosePrice;
        LowPrice = source.LowPrice;
        HighPrice = source.HighPrice;
        Volume = source.Volume;
        QuoteVolume = source.QuoteVolume;
        TakerBuyBaseVolume = source.TakerBuyBaseVolume;
        TakerBuyQuoteVolume = source.TakerBuyQuoteVolume;
        TradeCount = source.TradeCount;
    }

    /// <summary>
    /// Conversion method.
    /// </summary>
    public KLine ToKLine() => new() {
        OpenTime = OpenTime,
        CloseTime = CloseTime,
        OpenPrice = OpenPrice,
        ClosePrice = ClosePrice,
        LowPrice = LowPrice,
        HighPrice = HighPrice,
        Volume = Volume,
        QuoteVolume = QuoteVolume,
        TakerBuyBaseVolume = TakerBuyBaseVolume,
        TakerBuyQuoteVolume = TakerBuyQuoteVolume,
        TradeCount = TradeCount
    };

    /// <summary>
    /// The time this candlestick opened
    /// </summary>
    public DateTime OpenTime;

    /// <summary>
    /// The price at which this candlestick opened
    /// </summary>
    public double OpenPrice;

    /// <summary>
    /// The highest price in this candlestick
    /// </summary>
    public double HighPrice;

    /// <summary>
    /// The lowest price in this candlestick
    /// </summary>
    public double LowPrice;

    /// <summary>
    /// The price at which this candlestick closed
    /// </summary>
    public double ClosePrice;

    /// <summary>
    /// The volume traded during this candlestick
    /// </summary>
    public double Volume;

    /// <summary>
    /// The close time of this candlestick
    /// </summary>
    public DateTime CloseTime;

    /// <summary>
    /// The volume traded during this candlestick in the asset form
    /// </summary>
    public double QuoteVolume;

    /// <summary>
    /// The amount of trades in this candlestick
    /// </summary>
    public int TradeCount;

    /// <summary>
    /// Taker buy base asset volume
    /// </summary>
    public double TakerBuyBaseVolume;

    /// <summary>
    /// Taker buy quote asset volume
    /// </summary>
    public double TakerBuyQuoteVolume;
}