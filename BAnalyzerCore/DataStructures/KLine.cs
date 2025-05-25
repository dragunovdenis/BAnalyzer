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

using Binance.Net.Interfaces;

namespace BAnalyzerCore.DataStructures;

/// <summary>
/// A read-only container to store data from <see cref="IBinanceKline"/>
/// </summary>
public record KLine
{
    /// <summary>
    /// Constructor.
    /// </summary>
    public KLine(IBinanceKline source)
    {
        OpenTime = source.OpenTime;
        CloseTime = source.CloseTime;
        OpenPrice = (double)source.OpenPrice;
        ClosePrice = (double)source.ClosePrice;
        LowPrice = (double)source.LowPrice;
        HighPrice = (double)source.HighPrice;
        Volume = (double)source.Volume;
        QuoteVolume = (double)source.QuoteVolume;
        TakerBuyBaseVolume = (double)source.TakerBuyBaseVolume;
        TakerBuyQuoteVolume = (double)source.TakerBuyQuoteVolume;
        TradeCount = source.TradeCount;
    }

    /// <summary>
    /// Default constructor;
    /// </summary>
    public KLine() {}

    /// <summary>
    /// The time this candlestick opened
    /// </summary>
    public DateTime OpenTime { get; init; }

    /// <summary>
    /// The price at which this candlestick opened
    /// </summary>
    public double OpenPrice { get; init; }

    /// <summary>
    /// The highest price in this candlestick
    /// </summary>
    public double HighPrice { get; init; }

    /// <summary>
    /// The lowest price in this candlestick
    /// </summary>
    public double LowPrice { get; init; }

    /// <summary>
    /// The price at which this candlestick closed
    /// </summary>
    public double ClosePrice { get; init; }

    /// <summary>
    /// The volume traded during this candlestick
    /// </summary>
    public double Volume { get; init; }

    /// <summary>
    /// The close time of this candlestick
    /// </summary>
    public DateTime CloseTime { get; init; }

    /// <summary>
    /// The volume traded during this candlestick in the asset form
    /// </summary>
    public double QuoteVolume { get; init; }

    /// <summary>
    /// The amount of trades in this candlestick
    /// </summary>
    public int TradeCount { get; init; }

    /// <summary>
    /// Taker buy base asset volume
    /// </summary>
    public double TakerBuyBaseVolume { get; init; }

    /// <summary>
    /// Taker buy quote asset volume
    /// </summary>
    public double TakerBuyQuoteVolume { get; init; }
}