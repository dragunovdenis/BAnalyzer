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

using BAnalyzerCore.TradeSim;
using FluentAssertions;

namespace BAnalyzerCoreTest;

/// <summary>
/// Tests of <see cref="TradeSimulator"/>.
/// </summary>
[TestClass]
public class TradeSimulatorTest
{
    private static readonly DateTime Now = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private const decimal Tolerance = TradeSimulator.DefaultPriceTolerance;

    /// <summary>
    /// Returns a simulator with the given amount of USDT and the given fee rate.
    /// </summary>
    private static TradeSimulator CreateFunded(decimal usdt, decimal feeRate = 0.001m)
    {
        var sim = new TradeSimulator();
        sim.SetFeeRate(feeRate, out _).Should().BeTrue();

        if (usdt > 0)
            sim.Deposit(usdt, out _).Should().BeTrue();

        return sim;
    }

    private static TradeOrder Buy(decimal amount, AmountUnit unit, string symbol = "BTCUSDT") =>
        new("Binance", symbol, TradeSide.Buy, amount, unit);

    private static TradeOrder Sell(decimal amount, AmountUnit unit, string symbol = "BTCUSDT") =>
        new("Binance", symbol, TradeSide.Sell, amount, unit);

    private static TradeResult Execute(TradeSimulator sim, TradeOrder order, decimal price) =>
        sim.Execute(order, price, price, Tolerance, Now);

    [TestMethod]
    public void FreshSimulationHasOnlyEmptyUsdtBalance()
    {
        var sim = new TradeSimulator();

        sim.Balances.Should().BeEquivalentTo(new Dictionary<string, decimal> { ["USDT"] = 0m });
        sim.Trades.Should().BeEmpty();
        sim.FeeRate.Should().Be(TradeSimState.DefaultFeeRate);
    }

    [TestMethod]
    public void BuyForQuoteAmountChargesFeeInCoin()
    {
        var sim = CreateFunded(1000m);

        var result = Execute(sim, Buy(100m, AmountUnit.Quote), 50_000m);

        result.Success.Should().BeTrue();
        result.Trade.CoinAmount.Should().Be(0.002m);
        result.Trade.QuoteAmount.Should().Be(100m);
        result.Trade.Fee.Should().Be(0.000002m);
        result.Trade.FeeAsset.Should().Be("BTC");
        result.Trade.Asset.Should().Be("BTC");
        result.Trade.Id.Should().Be(1);
        sim.GetBalance("USDT").Should().Be(900m);
        sim.GetBalance("BTC").Should().Be(0.001998m);
    }

    [TestMethod]
    public void BuyForCoinAmountRoundsSpentUsdtUp()
    {
        var sim = CreateFunded(1000m, 0m);

        // 0.00000001 * 3 = 0.00000003 exactly; 0.00000001 * 3.3 = 0.000000033 -> rounded up to 0.00000004
        var result = Execute(sim, Buy(0.00000001m, AmountUnit.Coin), 3.3m);

        result.Success.Should().BeTrue();
        result.Trade.QuoteAmount.Should().Be(0.00000004m);
        sim.GetBalance("USDT").Should().Be(1000m - 0.00000004m);
        sim.GetBalance("BTC").Should().Be(0.00000001m);
    }

    [TestMethod]
    public void BuyForQuoteAmountRoundsReceivedCoinDown()
    {
        var sim = CreateFunded(1000m, 0m);

        var result = Execute(sim, Buy(10m, AmountUnit.Quote), 3m);

        result.Success.Should().BeTrue();
        result.Trade.CoinAmount.Should().Be(3.33333333m);
        sim.GetBalance("BTC").Should().Be(3.33333333m);
    }

    [TestMethod]
    public void SellForCoinAmountChargesFeeInUsdt()
    {
        var sim = CreateFunded(1000m);
        Execute(sim, Buy(0.01m, AmountUnit.Coin), 50_000m).Success.Should().BeTrue();
        var btc = sim.GetBalance("BTC");
        var usdt = sim.GetBalance("USDT");

        var result = Execute(sim, Sell(btc, AmountUnit.Coin), 60_000m);

        result.Success.Should().BeTrue();
        var gross = btc * 60_000m;
        result.Trade.QuoteAmount.Should().Be(gross);
        result.Trade.FeeAsset.Should().Be("USDT");
        result.Trade.Fee.Should().Be(gross * 0.001m);
        sim.GetBalance("USDT").Should().Be(usdt + gross * 0.999m);
        sim.Balances.Should().NotContainKey("BTC", "zero balances of coins are removed");
    }

    [TestMethod]
    public void SellForQuoteAmountRoundsSoldCoinUp()
    {
        var sim = CreateFunded(1000m, 0m);
        Execute(sim, Buy(30m, AmountUnit.Quote), 3m).Success.Should().BeTrue();

        var result = Execute(sim, Sell(10m, AmountUnit.Quote), 3m);

        result.Success.Should().BeTrue();
        result.Trade.CoinAmount.Should().Be(3.33333334m);
        sim.GetBalance("BTC").Should().Be(10m - 3.33333334m);
    }

    [TestMethod]
    public void CanBuyForExactlyTheWholeBalance()
    {
        var sim = CreateFunded(123.45m);

        Execute(sim, Buy(123.45m, AmountUnit.Quote), 17.3m).Success.Should().BeTrue();

        sim.GetBalance("USDT").Should().Be(0m);
        sim.Balances.Should().ContainKey("USDT", "the quote asset is always present");
    }

    [TestMethod]
    public void CanSellExactlyTheWholeHolding()
    {
        var sim = CreateFunded(100m);
        Execute(sim, Buy(100m, AmountUnit.Quote), 7m).Success.Should().BeTrue();

        Execute(sim, Sell(sim.GetBalance("BTC"), AmountUnit.Coin), 7.01m).Success.Should().BeTrue();

        sim.GetBalance("BTC").Should().Be(0m);
    }

    [TestMethod]
    public void BuyingMoreThanBalanceIsRejected()
    {
        var sim = CreateFunded(100m);
        var changes = 0;
        sim.Changed += (_, _) => changes++;

        var result = Execute(sim, Buy(100.00000001m, AmountUnit.Quote), 10m);

        result.Success.Should().BeFalse();
        result.Rejection.Should().Be(TradeRejection.InsufficientFunds);
        result.Message.Should().Contain("USDT");
        sim.GetBalance("USDT").Should().Be(100m);
        sim.Trades.Should().BeEmpty();
        changes.Should().Be(0);
    }

    [TestMethod]
    public void BuyingForCoinAmountWhoseRoundedUpCostExceedsBalanceIsRejected()
    {
        var sim = CreateFunded(0.00000003m, 0m);

        // cost 0.000000033 rounds up to 0.00000004 which exceeds the balance
        Execute(sim, Buy(0.00000001m, AmountUnit.Coin), 3.3m).Rejection
            .Should().Be(TradeRejection.InsufficientFunds);
    }

    [TestMethod]
    public void SellingMoreThanHoldingIsRejected()
    {
        var sim = CreateFunded(100m);
        Execute(sim, Buy(50m, AmountUnit.Quote), 10m).Success.Should().BeTrue();

        var result = Execute(sim, Sell(sim.GetBalance("BTC") + 0.00000001m, AmountUnit.Coin), 10m);

        result.Rejection.Should().Be(TradeRejection.InsufficientFunds);
        result.Message.Should().Contain("BTC");
    }

    [TestMethod]
    public void SellingCoinNotHeldIsRejected()
    {
        var sim = CreateFunded(100m);

        Execute(sim, Sell(1m, AmountUnit.Coin, "ETHUSDT"), 10m).Rejection
            .Should().Be(TradeRejection.InsufficientFunds);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("0.000000001")]
    public void InvalidAmountsAreRejected(string amount)
    {
        var sim = CreateFunded(100m);

        Execute(sim, Buy(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture),
            AmountUnit.Quote), 10m).Rejection.Should().Be(TradeRejection.InvalidAmount);
    }

    [TestMethod]
    public void AmountTooSmallToBuyAnythingIsRejected()
    {
        var sim = CreateFunded(100m);

        var result = Execute(sim, Buy(0.00000001m, AmountUnit.Quote), 50_000m);

        result.Rejection.Should().Be(TradeRejection.InvalidAmount);
        result.Message.Should().Contain("too small");
    }

    [TestMethod]
    public void HugeAmountIsRejectedRatherThanOverflowing()
    {
        var sim = CreateFunded(100m);

        Execute(sim, Buy(decimal.MaxValue / 10, AmountUnit.Coin), 1000m).Rejection
            .Should().Be(TradeRejection.InvalidAmount);
    }

    [TestMethod]
    [DataRow("USDT")]
    [DataRow("BTCEUR")]
    [DataRow("")]
    [DataRow(null)]
    public void NonUsdtSymbolsAreRejected(string symbol)
    {
        var sim = CreateFunded(100m);

        Execute(sim, Buy(1m, AmountUnit.Quote, symbol), 10m).Rejection
            .Should().Be(TradeRejection.InvalidSymbol);
    }

    [TestMethod]
    public void NonPositivePriceIsRejected()
    {
        var sim = CreateFunded(100m);

        Execute(sim, Buy(1m, AmountUnit.Quote), 0m).Rejection.Should().Be(TradeRejection.InvalidPrice);
    }

    [TestMethod]
    public void PriceMovedBeyondToleranceIsRejected()
    {
        var sim = CreateFunded(1000m);

        var result = sim.Execute(Buy(100m, AmountUnit.Quote), 100.6m, 100m, 0.005m, Now);

        result.Rejection.Should().Be(TradeRejection.PriceMoved);
        sim.GetBalance("USDT").Should().Be(1000m);
    }

    [TestMethod]
    public void PriceMovedWithinToleranceExecutesAtExecutionPrice()
    {
        var sim = CreateFunded(1000m, 0m);

        var result = sim.Execute(Buy(100m, AmountUnit.Quote), 100.5m, 100m, 0.005m, Now);

        result.Success.Should().BeTrue();
        result.Trade.Price.Should().Be(100.5m);
        result.Trade.CoinAmount.Should().Be(TradeSimulator.RoundDown(100m / 100.5m));
    }

    [TestMethod]
    public void PriceMovedTakesPrecedenceOverInsufficientFunds()
    {
        var sim = CreateFunded(10m);

        sim.Execute(Buy(100m, AmountUnit.Quote), 120m, 100m, 0.005m, Now).Rejection
            .Should().Be(TradeRejection.PriceMoved);
    }

    [TestMethod]
    public void QuoteMatchesExecution()
    {
        var sim = CreateFunded(1000m);
        var order = Buy(0.0123m, AmountUnit.Coin);

        var quote = sim.Quote(order, 43_210.987m);
        var result = Execute(sim, order, 43_210.987m);

        quote.IsValid.Should().BeTrue();
        result.Trade.CoinAmount.Should().Be(quote.CoinAmount);
        result.Trade.QuoteAmount.Should().Be(quote.QuoteAmount);
        result.Trade.Fee.Should().Be(quote.Fee);
        sim.GetBalance("BTC").Should().Be(quote.NetReceived);
    }

    [TestMethod]
    public void BalancesAreConservedThroughRoundTrip()
    {
        var sim = CreateFunded(1000m, 0.0025m);

        Execute(sim, Buy(333.33m, AmountUnit.Quote), 1.2345678m).Success.Should().BeTrue();
        Execute(sim, Sell(sim.GetBalance("BTC"), AmountUnit.Coin), 1.2345678m).Success.Should().BeTrue();

        var totalFeesInUsdt = sim.Trades.Sum(x => x.FeeAsset == "USDT" ? x.Fee : x.Fee * x.Price);
        sim.GetBalance("USDT").Should().BeLessThan(1000m);
        (1000m - sim.GetBalance("USDT")).Should().BeApproximately(totalFeesInUsdt, 0.0001m);
    }

    [TestMethod]
    public void PortfolioIsValuedAtGivenPrices()
    {
        var sim = CreateFunded(1000m, 0m);
        Execute(sim, Buy(100m, AmountUnit.Quote), 50_000m).Success.Should().BeTrue();
        Execute(sim, Buy(300m, AmountUnit.Quote, "ETHUSDT"), 3_000m).Success.Should().BeTrue();
        Execute(sim, Buy(50m, AmountUnit.Quote, "XRPUSDT"), 0.5m).Success.Should().BeTrue();

        var valuation = sim.Valuate(new Dictionary<string, decimal>
            { ["BTC"] = 60_000m, ["ETH"] = 3_333.333333333m, ["XRP"] = 0m, ["DOGE"] = 1m });

        valuation.Holdings.Select(x => x.Asset).Should().Equal("ETH", "BTC", "XRP");
        valuation.Holdings[0].Value.Should().Be(333.33333333m);
        valuation.Holdings[1].Should().Be(new HoldingValuation("BTC", 0.002m, 60_000m, 120m));
        valuation.Holdings[2].Should().Be(new HoldingValuation("XRP", 100m, null, null));
        valuation.Cash.Should().Be(550m);
        valuation.Total.Should().Be(550m + 120m + 333.33333333m);
        valuation.IsComplete.Should().BeFalse();
    }

    [TestMethod]
    public void PortfolioWithAllPricesKnownIsCompletelyValued()
    {
        var sim = CreateFunded(100m, 0m);
        sim.Valuate(new Dictionary<string, decimal>()).Should()
            .BeEquivalentTo(new { Cash = 100m, Total = 100m, IsComplete = true, Holdings = Array.Empty<HoldingValuation>() });

        Execute(sim, Buy(100m, AmountUnit.Quote), 50_000m).Success.Should().BeTrue();
        var valuation = sim.Valuate(new Dictionary<string, decimal> { ["BTC"] = 40_000m });

        valuation.Total.Should().Be(80m);
        valuation.IsComplete.Should().BeTrue();
    }

    [TestMethod]
    public void DepositAndWithdraw()
    {
        var sim = new TradeSimulator();
        var changes = 0;
        sim.Changed += (_, _) => changes++;

        sim.Deposit(100m, out _).Should().BeTrue();
        sim.Withdraw(40m, out _).Should().BeTrue();
        sim.Withdraw(60.00000001m, out var error).Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
        sim.Deposit(0m, out _).Should().BeFalse();
        sim.Deposit(TradeSimulator.MaxDeposit + 1, out _).Should().BeFalse();

        sim.GetBalance("USDT").Should().Be(60m);
        changes.Should().Be(2);
    }

    [TestMethod]
    public void FeeRateIsValidated()
    {
        var sim = new TradeSimulator();

        sim.SetFeeRate(-0.001m, out _).Should().BeFalse();
        sim.SetFeeRate(0.1001m, out _).Should().BeFalse();
        sim.SetFeeRate(0.1m, out _).Should().BeTrue();
        sim.FeeRate.Should().Be(0.1m);
    }

    [TestMethod]
    public void ResetClearsEverythingButFeeRate()
    {
        var sim = CreateFunded(1000m, 0.002m);
        Execute(sim, Buy(100m, AmountUnit.Quote), 10m).Success.Should().BeTrue();

        sim.Reset();

        sim.Balances.Should().BeEquivalentTo(new Dictionary<string, decimal> { ["USDT"] = 0m });
        sim.Trades.Should().BeEmpty();
        sim.FeeRate.Should().Be(0.002m);
        sim.Deposit(100m, out _);
        Execute(sim, Buy(10m, AmountUnit.Quote), 10m).Trade.Id.Should().Be(1);
    }

    [TestMethod]
    public void TradesOfAssetAreReturnedNewestFirst()
    {
        var sim = CreateFunded(1000m);
        Execute(sim, Buy(10m, AmountUnit.Quote), 10m);
        Execute(sim, Buy(10m, AmountUnit.Quote, "ETHUSDT"), 10m);
        Execute(sim, Buy(20m, AmountUnit.Quote), 10m);

        sim.GetTrades("BTC").Select(x => x.Id).Should().Equal(3, 1);
        sim.GetTrades("ETH").Select(x => x.Id).Should().Equal(2);
        sim.GetTrades("XRP").Should().BeEmpty();
    }

    [TestMethod]
    public void SnapshotRestoresEquivalentSimulator()
    {
        var sim = CreateFunded(1000m, 0.002m);
        Execute(sim, Buy(100m, AmountUnit.Quote), 10m);
        Execute(sim, Buy(50m, AmountUnit.Quote, "ETHUSDT"), 2m);

        var restored = new TradeSimulator(sim.Snapshot());

        restored.Balances.Should().BeEquivalentTo(sim.Balances);
        restored.Trades.Should().BeEquivalentTo(sim.Trades);
        restored.FeeRate.Should().Be(0.002m);
        Execute(restored, Buy(10m, AmountUnit.Quote), 10m).Trade.Id.Should().Be(3);
    }

    [TestMethod]
    public void SnapshotIsIndependentOfSimulator()
    {
        var sim = CreateFunded(1000m);
        var snapshot = sim.Snapshot();

        sim.Withdraw(500m, out _);

        snapshot.Balances["USDT"].Should().Be(1000m);
    }

    [TestMethod]
    public void ConstructorRepairsNextTradeIdAndMissingUsdt()
    {
        var state = new TradeSimState
        {
            Balances = new Dictionary<string, decimal> { ["BTC"] = 1m, ["ETH"] = 0m },
            Trades = [new TradeRecord { Id = 7, Exchange = "Binance", Symbol = "BTCUSDT", Asset = "BTC", FeeAsset = "BTC" }],
            NextTradeId = 2,
        };

        var sim = new TradeSimulator(state);

        sim.Balances.Should().BeEquivalentTo(new Dictionary<string, decimal> { ["USDT"] = 0m, ["BTC"] = 1m });
        sim.Snapshot().NextTradeId.Should().Be(8);
    }

    [TestMethod]
    public void InvalidStatesAreDetected()
    {
        TradeSimulator.Validate(null).Should().NotBeNull();
        TradeSimulator.Validate(new TradeSimState()).Should().BeNull();
        TradeSimulator.Validate(new TradeSimState { Version = 2 }).Should().Contain("newer");
        TradeSimulator.Validate(new TradeSimState { Version = 0 }).Should().NotBeNull();
        TradeSimulator.Validate(new TradeSimState { FeeRate = 0.5m }).Should().NotBeNull();
        TradeSimulator.Validate(new TradeSimState { Balances = null }).Should().NotBeNull();
        TradeSimulator.Validate(new TradeSimState { Balances = new() { ["BTC"] = -1m } }).Should().NotBeNull();
        TradeSimulator.Validate(new TradeSimState { Trades = null }).Should().NotBeNull();
        TradeSimulator.Validate(new TradeSimState { Trades = [null] }).Should().NotBeNull();
        TradeSimulator.Validate(new TradeSimState { NextTradeId = 0 }).Should().NotBeNull();

        var act = () => new TradeSimulator(new TradeSimState { Balances = null });
        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(0.0)]
    [DataRow(-1.0)]
    [DataRow(1e30)]
    [DataRow(1e-30)]
    public void UnusablePricesAreNotConverted(double price)
    {
        TradeSimulator.TryToDecimalPrice(price, out _).Should().BeFalse();
    }

    [TestMethod]
    public void UsablePriceIsConverted()
    {
        TradeSimulator.TryToDecimalPrice(43_210.5, out var price).Should().BeTrue();
        price.Should().Be(43_210.5m);
    }

    [TestMethod]
    public void OtherAmountIsDerivedAndTruncated()
    {
        TradeSimulator.DeriveOtherAmount(10m, AmountUnit.Quote, 3m).Should().Be(3.33333333m);
        TradeSimulator.DeriveOtherAmount(2m, AmountUnit.Coin, 1.5m).Should().Be(3m);
        TradeSimulator.DeriveOtherAmount(0m, AmountUnit.Coin, 1.5m).Should().BeNull();
        TradeSimulator.DeriveOtherAmount(1m, AmountUnit.Coin, 0m).Should().BeNull();
        TradeSimulator.DeriveOtherAmount(decimal.MaxValue, AmountUnit.Coin, 2m).Should().BeNull();
    }
}
