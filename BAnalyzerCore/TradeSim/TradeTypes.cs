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

namespace BAnalyzerCore.TradeSim;

/// <summary>
/// Direction of a simulated trade.
/// </summary>
public enum TradeSide
{
    Buy = 0,
    Sell = 1,
}

/// <summary>
/// The unit in which the (primary) amount of an order is expressed.
/// </summary>
public enum AmountUnit
{
    /// <summary>
    /// The amount is a quantity of the traded coin.
    /// </summary>
    Coin = 0,

    /// <summary>
    /// The amount is a quantity of the quote asset (USDT).
    /// </summary>
    Quote = 1,
}

/// <summary>
/// Reason why an order can't be (or was not) executed.
/// </summary>
public enum TradeRejection
{
    None = 0,
    InvalidSymbol,
    InvalidAmount,
    InvalidPrice,
    PriceMoved,
    InsufficientFunds,
}

/// <summary>
/// A market order as requested by the user.
/// </summary>
/// <param name="Exchange">Name of the exchange the order is priced on.</param>
/// <param name="Symbol">USDT-quoted symbol, e.g. "BTCUSDT".</param>
/// <param name="Side">Buy or sell.</param>
/// <param name="Amount">The amount the user entered into the primary field.</param>
/// <param name="Unit">The unit of <paramref name="Amount"/>.</param>
public sealed record TradeOrder(string Exchange, string Symbol, TradeSide Side, decimal Amount, AmountUnit Unit);

/// <summary>
/// The result of pricing an order at a given price; no state is changed by producing a quote.
/// </summary>
/// <param name="Order">The priced order.</param>
/// <param name="Asset">The traded coin, e.g. "BTC" (null if the symbol is invalid).</param>
/// <param name="Price">The price the order was evaluated at.</param>
/// <param name="CoinAmount">Gross quantity of the coin.</param>
/// <param name="QuoteAmount">Gross quantity of USDT.</param>
/// <param name="NetReceived">What the user receives after the fee, in <paramref name="ReceivedAsset"/>.</param>
/// <param name="Fee">The fee, in <paramref name="ReceivedAsset"/>.</param>
/// <param name="ReceivedAsset">The asset the user receives and the fee is charged in
/// ("null" if the order could not be priced).</param>
/// <param name="Rejection">Reason why the order can't be executed or <see cref="TradeRejection.None"/>.</param>
/// <param name="Message">Human-readable explanation of the rejection (empty if valid).</param>
public sealed record TradeQuote(TradeOrder Order, string Asset, decimal Price,
    decimal CoinAmount, decimal QuoteAmount, decimal NetReceived, decimal Fee,
    string ReceivedAsset, TradeRejection Rejection, string Message)
{
    /// <summary>
    /// "True" if the order can be executed as quoted.
    /// </summary>
    public bool IsValid => Rejection == TradeRejection.None;
}

/// <summary>
/// Outcome of an attempt to execute an order.
/// </summary>
/// <param name="Trade">The executed trade or "null" if the order was rejected.</param>
/// <param name="Rejection">Reason of the rejection or <see cref="TradeRejection.None"/>.</param>
/// <param name="Message">Human-readable explanation of the rejection (empty on success).</param>
public sealed record TradeResult(TradeRecord Trade, TradeRejection Rejection, string Message)
{
    /// <summary>
    /// "True" if the order was executed.
    /// </summary>
    public bool Success => Trade != null;
}

/// <summary>
/// Value of a coin holding.
/// </summary>
/// <param name="Asset">The coin, e.g. "BTC".</param>
/// <param name="Amount">The held quantity.</param>
/// <param name="Price">Price of the coin in USDT or "null" if it is not known.</param>
/// <param name="Value">Value of the holding in USDT (rounded down) or "null" if the price is not known.</param>
public sealed record HoldingValuation(string Asset, decimal Amount, decimal? Price, decimal? Value);

/// <summary>
/// Value of the whole portfolio at the given prices.
/// </summary>
/// <param name="Cash">The USDT balance.</param>
/// <param name="Holdings">The coin holdings: the valued ones first (most valuable first), then the others by name.</param>
/// <param name="Total">The USDT balance plus the value of all the valued holdings.</param>
public sealed record PortfolioValuation(decimal Cash, IReadOnlyList<HoldingValuation> Holdings, decimal Total)
{
    /// <summary>
    /// "True" if every holding has been valued, i.e. <see cref="Total"/> is not just a lower bound.
    /// </summary>
    public bool IsComplete => Holdings.All(x => x.Value != null);
}
