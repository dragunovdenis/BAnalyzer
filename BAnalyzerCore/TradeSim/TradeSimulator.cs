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

using System.Globalization;
using BAnalyzer.Utils;

namespace BAnalyzerCore.TradeSim;

/// <summary>
/// The rules of the trade simulator: a portfolio of balances that can be funded with USDT
/// and traded with market orders at prices supplied by the caller.
/// </summary>
/// <remarks>
/// Not thread-safe: all the calls are expected to be made from one (UI) thread.
/// </remarks>
public sealed class TradeSimulator
{
    /// <summary>
    /// The asset all the symbols are quoted in.
    /// </summary>
    public const string QuoteAsset = "USDT";

    /// <summary>
    /// Maximal number of decimal places of all the amounts.
    /// </summary>
    public const int AmountDecimals = 8;

    /// <summary>
    /// Default maximal relative deviation of the execution price from the displayed one (0.5 %).
    /// </summary>
    public const decimal DefaultPriceTolerance = 0.005m;

    /// <summary>
    /// Maximal fee rate (10 %).
    /// </summary>
    public const decimal MaxFeeRate = 0.10m;

    /// <summary>
    /// Maximal amount of a single deposit (USDT).
    /// </summary>
    public const decimal MaxDeposit = 1_000_000_000m;

    private readonly Dictionary<string, decimal> _balances = new();

    private readonly List<TradeRecord> _trades = new();

    private long _nextTradeId;

    /// <summary>
    /// Raised after every successful change of the state.
    /// </summary>
    public event EventHandler Changed;

    /// <summary>
    /// Current fee rate (fraction, 0.001 = 0.1 %).
    /// </summary>
    public decimal FeeRate { get; private set; }

    /// <summary>
    /// Non-zero balances by asset; the quote asset is always present.
    /// </summary>
    public IReadOnlyDictionary<string, decimal> Balances => _balances;

    /// <summary>
    /// All the executed trades in chronological order.
    /// </summary>
    public IReadOnlyList<TradeRecord> Trades => _trades;

    /// <summary>
    /// Constructs a fresh simulation.
    /// </summary>
    public TradeSimulator() : this(new TradeSimState()) {}

    /// <summary>
    /// Constructs a simulation from the given state (which is copied).
    /// </summary>
    /// <exception cref="ArgumentException">If the state is not valid (see <see cref="Validate"/>).</exception>
    public TradeSimulator(TradeSimState state)
    {
        var error = Validate(state);

        if (error != null)
            throw new ArgumentException(error, nameof(state));

        FeeRate = state.FeeRate;

        foreach (var (asset, amount) in state.Balances)
            if (amount > 0)
                _balances[asset] = amount;

        _balances.TryAdd(QuoteAsset, 0m);
        _trades.AddRange(state.Trades.OrderBy(x => x.Id));
        _nextTradeId = Math.Max(state.NextTradeId, _trades.Count == 0 ? 1 : _trades[^1].Id + 1);
    }

    /// <summary>
    /// Returns a description of the first problem found in the given state or "null" if the state is valid.
    /// </summary>
    public static string Validate(TradeSimState state)
    {
        if (state == null)
            return "The state is missing.";

        if (state.Version > TradeSimState.CurrentVersion)
            return $"It was written by a newer version of the application (format version {state.Version}).";

        if (state.Version < 1)
            return $"Unknown format version {state.Version}.";

        if (state.FeeRate < 0 || state.FeeRate > MaxFeeRate)
            return $"The fee rate {Format(state.FeeRate)} is out of range.";

        if (state.Balances == null)
            return "The balances are missing.";

        foreach (var (asset, amount) in state.Balances)
        {
            if (string.IsNullOrWhiteSpace(asset))
                return "A balance has no asset name.";

            if (amount < 0)
                return $"The balance of {asset} is negative.";
        }

        if (state.Trades == null)
            return "The trade history is missing.";

        if (state.Trades.Any(x => x == null || string.IsNullOrEmpty(x.Asset) || string.IsNullOrEmpty(x.Symbol) ||
                                  x.Exchange == null || string.IsNullOrEmpty(x.FeeAsset)))
            return "The trade history contains incomplete records.";

        if (state.NextTradeId < 1)
            return "The trade identifier counter is invalid.";

        return null;
    }

    /// <summary>
    /// Returns the balance of the given asset (zero if there is none).
    /// </summary>
    public decimal GetBalance(string asset) => asset != null ? _balances.GetValueOrDefault(asset) : 0m;

    /// <summary>
    /// Returns the trades of the given asset (on all exchanges), newest first.
    /// </summary>
    public IReadOnlyList<TradeRecord> GetTrades(string asset) =>
        _trades.Where(x => x.Asset == asset).OrderByDescending(x => x.Id).ToArray();

    /// <summary>
    /// Extracts the traded coin from a USDT-quoted symbol; returns "false" if the symbol is not such.
    /// </summary>
    public static bool TryGetAsset(string symbol, out string asset)
    {
        asset = null;

        if (symbol == null || symbol.Length <= QuoteAsset.Length || !symbol.EndsWith(QuoteAsset, StringComparison.Ordinal))
            return false;

        asset = SymbolUtils.SymbolToAsset(symbol);
        return true;
    }

    /// <summary>
    /// Converts a price received from an exchange; returns "false" if it is not a usable (finite, positive) price.
    /// </summary>
    public static bool TryToDecimalPrice(double price, out decimal result)
    {
        result = 0m;

        if (!double.IsFinite(price) || price <= 0)
            return false;

        try
        {
            result = (decimal)price;
        }
        catch (OverflowException)
        {
            return false;
        }

        return result > 0;
    }

    /// <summary>
    /// Returns the value of the "other" amount field for the given primary amount at the given price,
    /// rounded toward zero, or "null" if it can't be calculated.
    /// </summary>
    public static decimal? DeriveOtherAmount(decimal amount, AmountUnit unit, decimal price)
    {
        if (amount <= 0 || price <= 0)
            return null;

        try
        {
            return RoundDown(unit == AmountUnit.Coin ? amount * price : amount / price);
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    /// <summary>
    /// Truncates the given (non-negative) amount to <see cref="AmountDecimals"/> decimal places.
    /// </summary>
    public static decimal RoundDown(decimal value) =>
        decimal.Round(value, AmountDecimals, MidpointRounding.ToZero);

    /// <summary>
    /// Rounds the given (non-negative) amount up to <see cref="AmountDecimals"/> decimal places.
    /// </summary>
    private static decimal RoundUp(decimal value) =>
        decimal.Round(value, AmountDecimals, MidpointRounding.ToPositiveInfinity);

    /// <summary>
    /// Formats the given amount for messages.
    /// </summary>
    public static string Format(decimal value) => value.ToString("0.############", CultureInfo.InvariantCulture);

    /// <summary>
    /// Returns a description of what's wrong with the given amount or "null" if it is fine.
    /// </summary>
    private static string ValidateAmount(decimal amount)
    {
        if (amount <= 0)
            return "The amount must be positive.";

        if (decimal.Round(amount, AmountDecimals) != amount)
            return $"The amount can't have more than {AmountDecimals} decimal places.";

        return null;
    }

    /// <summary>
    /// Evaluates the given order at the given price without changing the state.
    /// </summary>
    /// <remarks>
    /// The primary amount of the order is taken as is; the other gross amount is derived from it
    /// and rounded in the direction that never favors the user (what the user gives is rounded up,
    /// what the user gets is rounded down).
    /// </remarks>
    public TradeQuote Quote(TradeOrder order, decimal price)
    {
        ArgumentNullException.ThrowIfNull(order);

        var isBuy = order.Side == TradeSide.Buy;

        if (!TryGetAsset(order.Symbol, out var asset))
            return Reject(TradeRejection.InvalidSymbol, $"\"{order.Symbol}\" is not a {QuoteAsset}-quoted symbol.");

        var amountError = ValidateAmount(order.Amount);

        if (amountError != null)
            return Reject(TradeRejection.InvalidAmount, amountError);

        if (price <= 0)
            return Reject(TradeRejection.InvalidPrice, "The price is not valid.");

        decimal coin, quote, net, fee;

        try
        {
            if (order.Unit == AmountUnit.Coin)
            {
                coin = order.Amount;
                quote = isBuy ? RoundUp(coin * price) : RoundDown(coin * price);
            }
            else
            {
                quote = order.Amount;
                coin = isBuy ? RoundDown(quote / price) : RoundUp(quote / price);
            }

            var gross = isBuy ? coin : quote;
            net = RoundDown(gross * (1 - FeeRate));
            fee = gross - net;
        }
        catch (OverflowException)
        {
            return Reject(TradeRejection.InvalidAmount, "The amount is too large.");
        }

        var receivedAsset = isBuy ? asset : QuoteAsset;

        TradeQuote Result(TradeRejection rejection, string message) => new(order, asset, price, coin, quote,
            net, fee, receivedAsset, rejection, message);

        if (coin == 0 || quote == 0 || net == 0)
            return Result(TradeRejection.InvalidAmount, "The amount is too small.");

        if (isBuy && quote > GetBalance(QuoteAsset))
            return Result(TradeRejection.InsufficientFunds,
                $"Not enough {QuoteAsset}: {Format(quote)} needed, {Format(GetBalance(QuoteAsset))} available.");

        if (!isBuy && coin > GetBalance(asset))
            return Result(TradeRejection.InsufficientFunds,
                $"Not enough {asset}: {Format(coin)} needed, {Format(GetBalance(asset))} available.");

        return Result(TradeRejection.None, string.Empty);

        TradeQuote Reject(TradeRejection rejection, string message) =>
            new(order, asset, price, 0m, 0m, 0m, 0m, null, rejection, message);
    }

    /// <summary>
    /// Executes the given order at <paramref name="executionPrice"/> unless it deviates
    /// from <paramref name="referencePrice"/> (the one the user saw) by more than <paramref name="tolerance"/>
    /// (a fraction) or the order is not valid; a rejected order changes nothing.
    /// </summary>
    public TradeResult Execute(TradeOrder order, decimal executionPrice, decimal referencePrice,
        decimal tolerance, DateTime utcNow)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tolerance);

        var quote = Quote(order, executionPrice);

        if (quote.Rejection is TradeRejection.None or TradeRejection.InsufficientFunds)
        {
            if (referencePrice <= 0)
                return new TradeResult(null, TradeRejection.InvalidPrice, "The displayed price is not valid.");

            var deviation = Math.Abs(executionPrice - referencePrice) / referencePrice;

            if (deviation > tolerance)
                return new TradeResult(null, TradeRejection.PriceMoved,
                    $"The price moved from {Format(referencePrice)} to {Format(executionPrice)} " +
                    $"({FormatPercent(deviation)}), more than the {FormatPercent(tolerance)} tolerance. " +
                    "Check the new price and try again.");
        }

        if (!quote.IsValid)
            return new TradeResult(null, quote.Rejection, quote.Message);

        var isBuy = order.Side == TradeSide.Buy;
        var (spentAsset, spent) = isBuy ? (QuoteAsset, quote.QuoteAmount) : (quote.Asset, quote.CoinAmount);
        decimal spentBalance, receivedBalance;

        try
        {
            spentBalance = GetBalance(spentAsset) - spent;
            receivedBalance = GetBalance(quote.ReceivedAsset) + quote.NetReceived;
        }
        catch (OverflowException)
        {
            return new TradeResult(null, TradeRejection.InvalidAmount, "The amount is too large.");
        }

        SetBalance(spentAsset, spentBalance);
        SetBalance(quote.ReceivedAsset, receivedBalance);

        var trade = new TradeRecord
        {
            Id = _nextTradeId++,
            TimeUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
            Exchange = order.Exchange ?? string.Empty,
            Symbol = order.Symbol,
            Asset = quote.Asset,
            Side = order.Side,
            Price = executionPrice,
            CoinAmount = quote.CoinAmount,
            QuoteAmount = quote.QuoteAmount,
            Fee = quote.Fee,
            FeeAsset = quote.ReceivedAsset,
            FeeRate = FeeRate,
        };

        _trades.Add(trade);
        OnChanged();

        return new TradeResult(trade, TradeRejection.None, string.Empty);
    }

    /// <summary>
    /// Adds the given amount of USDT to the balance.
    /// </summary>
    public bool Deposit(decimal amount, out string error)
    {
        error = ValidateAmount(amount);

        if (error == null && amount > MaxDeposit)
            error = $"A single deposit can't exceed {Format(MaxDeposit)} {QuoteAsset}.";

        if (error != null)
            return false;

        try
        {
            _balances[QuoteAsset] = GetBalance(QuoteAsset) + amount;
        }
        catch (OverflowException)
        {
            error = "The balance would become too large.";
            return false;
        }

        OnChanged();
        return true;
    }

    /// <summary>
    /// Removes the given amount of USDT from the balance.
    /// </summary>
    public bool Withdraw(decimal amount, out string error)
    {
        error = ValidateAmount(amount);

        if (error == null && amount > GetBalance(QuoteAsset))
            error = $"Not enough {QuoteAsset}: {Format(amount)} requested, {Format(GetBalance(QuoteAsset))} available.";

        if (error != null)
            return false;

        _balances[QuoteAsset] = GetBalance(QuoteAsset) - amount;
        OnChanged();
        return true;
    }

    /// <summary>
    /// Sets the fee rate (a fraction) applied to the future trades.
    /// </summary>
    public bool SetFeeRate(decimal rate, out string error)
    {
        error = null;

        if (rate < 0 || rate > MaxFeeRate)
        {
            error = $"The fee must be between 0 % and {FormatPercent(MaxFeeRate)}.";
            return false;
        }

        if (rate == FeeRate)
            return true;

        FeeRate = rate;
        OnChanged();
        return true;
    }

    /// <summary>
    /// Clears all the balances and the trade history; the fee rate is kept.
    /// </summary>
    public void Reset()
    {
        _balances.Clear();
        _balances[QuoteAsset] = 0m;
        _trades.Clear();
        _nextTradeId = 1;
        OnChanged();
    }

    /// <summary>
    /// Returns a copy of the current state suitable for persisting.
    /// </summary>
    public TradeSimState Snapshot() => new()
    {
        Version = TradeSimState.CurrentVersion,
        FeeRate = FeeRate,
        Balances = new Dictionary<string, decimal>(_balances),
        Trades = new List<TradeRecord>(_trades),
        NextTradeId = _nextTradeId,
    };

    /// <summary>
    /// Values the portfolio at the given prices (USDT per coin); the coins without a (positive) price
    /// are reported as not valued.
    /// </summary>
    public PortfolioValuation Valuate(IReadOnlyDictionary<string, decimal> prices)
    {
        ArgumentNullException.ThrowIfNull(prices);

        var holdings = _balances.Where(x => x.Key != QuoteAsset).Select(x =>
            {
                decimal? price = prices.TryGetValue(x.Key, out var p) && p > 0 ? p : null;
                return new HoldingValuation(x.Key, x.Value, price, price != null ? TryValue(x.Value, price.Value) : null);
            })
            .OrderBy(x => x.Value == null).ThenByDescending(x => x.Value).ThenBy(x => x.Asset, StringComparer.Ordinal)
            .ToArray();

        var cash = GetBalance(QuoteAsset);
        var total = cash;

        try
        {
            foreach (var holding in holdings)
                total += holding.Value ?? 0m;
        }
        catch (OverflowException)
        {
            total = decimal.MaxValue;
        }

        return new PortfolioValuation(cash, holdings, total);

        static decimal? TryValue(decimal amount, decimal price)
        {
            try
            {
                return RoundDown(amount * price);
            }
            catch (OverflowException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Formats the given fraction as percentage.
    /// </summary>
    private static string FormatPercent(decimal fraction) =>
        (fraction * 100).ToString("0.##", CultureInfo.InvariantCulture) + " %";

    /// <summary>
    /// Sets the balance of the given asset; zero balances of coins are removed.
    /// </summary>
    private void SetBalance(string asset, decimal amount)
    {
        if (amount == 0 && asset != QuoteAsset)
            _balances.Remove(asset);
        else
            _balances[asset] = amount;
    }

    /// <summary>
    /// Raises the <see cref="Changed"/> event.
    /// </summary>
    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
