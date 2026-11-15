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

using System.Runtime.Serialization;

namespace BAnalyzerCore.TradeSim;

/// <summary>
/// A single executed simulated trade.
/// </summary>
[DataContract(Name = "TradeRecord", Namespace = TradeSimState.XmlNamespace)]
public sealed class TradeRecord
{
    /// <summary>
    /// Identifier of the trade, increasing with every trade.
    /// </summary>
    [DataMember(Order = 0)]
    public long Id { get; init; }

    /// <summary>
    /// Time of execution (UTC).
    /// </summary>
    [DataMember(Order = 1)]
    public DateTime TimeUtc { get; init; }

    /// <summary>
    /// Name of the exchange the trade was priced on.
    /// </summary>
    [DataMember(Order = 2)]
    public string Exchange { get; init; }

    /// <summary>
    /// The traded symbol, e.g. "BTCUSDT".
    /// </summary>
    [DataMember(Order = 3)]
    public string Symbol { get; init; }

    /// <summary>
    /// The traded coin, e.g. "BTC".
    /// </summary>
    [DataMember(Order = 4)]
    public string Asset { get; init; }

    /// <summary>
    /// Buy or sell.
    /// </summary>
    [DataMember(Order = 5)]
    public TradeSide Side { get; init; }

    /// <summary>
    /// Execution price (USDT per coin).
    /// </summary>
    [DataMember(Order = 6)]
    public decimal Price { get; init; }

    /// <summary>
    /// Gross quantity of the coin.
    /// </summary>
    [DataMember(Order = 7)]
    public decimal CoinAmount { get; init; }

    /// <summary>
    /// Gross quantity of USDT.
    /// </summary>
    [DataMember(Order = 8)]
    public decimal QuoteAmount { get; init; }

    /// <summary>
    /// The fee charged, in <see cref="FeeAsset"/>.
    /// </summary>
    [DataMember(Order = 9)]
    public decimal Fee { get; init; }

    /// <summary>
    /// The asset the fee was charged in.
    /// </summary>
    [DataMember(Order = 10)]
    public string FeeAsset { get; init; }

    /// <summary>
    /// The fee rate (fraction) in effect when the trade was executed.
    /// </summary>
    [DataMember(Order = 11)]
    public decimal FeeRate { get; init; }
}

/// <summary>
/// Persisted state of the trade simulator.
/// </summary>
[DataContract(Name = "TradeSimState", Namespace = XmlNamespace)]
public sealed class TradeSimState
{
    /// <summary>
    /// XML namespace of the persisted data.
    /// </summary>
    public const string XmlNamespace = "BAnalyzer.TradeSim";

    /// <summary>
    /// The version of the format written by this build.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// Fee rate of a fresh simulation (0.1 %).
    /// </summary>
    public const decimal DefaultFeeRate = 0.001m;

    /// <summary>
    /// Version of the format.
    /// </summary>
    [DataMember(Order = 0)]
    public int Version { get; set; } = CurrentVersion;

    /// <summary>
    /// Fee rate (fraction, 0.001 = 0.1 %).
    /// </summary>
    [DataMember(Order = 1)]
    public decimal FeeRate { get; set; } = DefaultFeeRate;

    /// <summary>
    /// Balances by asset.
    /// </summary>
    [DataMember(Order = 2)]
    public Dictionary<string, decimal> Balances { get; set; } = new() { [TradeSimulator.QuoteAsset] = 0m };

    /// <summary>
    /// All executed trades in chronological order.
    /// </summary>
    [DataMember(Order = 3)]
    public List<TradeRecord> Trades { get; set; } = new();

    /// <summary>
    /// Identifier to be assigned to the next trade.
    /// </summary>
    [DataMember(Order = 4)]
    public long NextTradeId { get; set; } = 1;
}
