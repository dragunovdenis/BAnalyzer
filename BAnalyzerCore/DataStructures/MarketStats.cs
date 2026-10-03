//Copyright (c) 2026 Denys Dragunov, dragunovdenis@gmail.com
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

namespace BAnalyzerCore.DataStructures;

/// <summary>
/// Read-only interface of the rolling 24-hour statistics of a symbol,
/// including the current top of the order book.
/// </summary>
/// <remarks>
/// The fields that only some of the exchanges report are nullable: a
/// "null" means "this exchange does not provide the value" and must be
/// omitted when the data is presented rather than shown as a zero.
/// </remarks>
public interface IMarketStats
{
    /// <summary>
    /// Symbol the statistics correspond to.
    /// </summary>
    string Symbol { get; }

    /// <summary>
    /// The latest traded price.
    /// </summary>
    double LastPrice { get; }

    /// <summary>
    /// The price 24 hours ago.
    /// </summary>
    double OpenPrice24H { get; }

    /// <summary>
    /// The highest price of the last 24 hours.
    /// </summary>
    double HighPrice24H { get; }

    /// <summary>
    /// The lowest price of the last 24 hours.
    /// </summary>
    double LowPrice24H { get; }

    /// <summary>
    /// Relative price change over the last 24 hours expressed as a
    /// percentage (i.e. 2.5 means "plus 2.5 percent").
    /// </summary>
    double PriceChangePercentage24H { get; }

    /// <summary>
    /// Volume traded over the last 24 hours in the base asset.
    /// </summary>
    double BaseVolume24H { get; }

    /// <summary>
    /// Volume traded over the last 24 hours in the quote asset (turnover).
    /// </summary>
    double QuoteVolume24H { get; }

    /// <summary>
    /// The highest price a buyer is currently willing to pay.
    /// </summary>
    double BestBidPrice { get; }

    /// <summary>
    /// The quantity offered at <see cref="BestBidPrice"/>.
    /// </summary>
    double BestBidQuantity { get; }

    /// <summary>
    /// The lowest price a seller is currently willing to accept.
    /// </summary>
    double BestAskPrice { get; }

    /// <summary>
    /// The quantity offered at <see cref="BestAskPrice"/>.
    /// </summary>
    double BestAskQuantity { get; }

    /// <summary>
    /// Volume-weighted average price of the last 24 hours or "null"
    /// if the exchange does not report it.
    /// </summary>
    double? WeightedAveragePrice24H { get; }

    /// <summary>
    /// Number of trades over the last 24 hours or "null" if the
    /// exchange does not report it.
    /// </summary>
    long? TradeCount24H { get; }

    /// <summary>
    /// Time stamp (UTC) of the moment the statistics were retrieved.
    /// </summary>
    DateTime TimeStamp { get; }
}

/// <summary>
/// Implementation of the corresponding interface.
/// </summary>
internal record MarketStats(string Symbol, double LastPrice, double OpenPrice24H,
    double HighPrice24H, double LowPrice24H, double PriceChangePercentage24H,
    double BaseVolume24H, double QuoteVolume24H, double BestBidPrice, double BestBidQuantity,
    double BestAskPrice, double BestAskQuantity, double? WeightedAveragePrice24H,
    long? TradeCount24H, DateTime TimeStamp) : IMarketStats;