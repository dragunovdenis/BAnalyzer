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
/// Tests of <see cref="TradeSimFileStore"/>.
/// </summary>
[TestClass]
public class TradeSimFileStoreTest
{
    private string _directory;

    private string FilePath => Path.Combine(_directory, "trade-sim.xml");

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), "BAnalyzerTradeSimTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [TestMethod]
    public void MissingFileGivesFreshStateWithoutWarning()
    {
        var result = new TradeSimFileStore(FilePath).Load();

        result.Warning.Should().BeNull();
        TradeSimulator.Validate(result.State).Should().BeNull();
        result.State.Trades.Should().BeEmpty();
    }

    [TestMethod]
    public void SavedStateIsLoadedBackExactly()
    {
        var sim = new TradeSimulator();
        sim.SetFeeRate(0.00075m, out _);
        sim.Deposit(1234.56789012m, out _);
        var time = new DateTime(2026, 5, 6, 7, 8, 9, 123, DateTimeKind.Utc);
        sim.Execute(new TradeOrder("Bybit", "BTCUSDT", TradeSide.Buy, 100m, AmountUnit.Quote), 43_210.98765m,
            43_210.98765m, 0.005m, time).Success.Should().BeTrue();
        sim.Execute(new TradeOrder("Binance", "ETHUSDT", TradeSide.Buy, 0.25m, AmountUnit.Coin), 2_345.6m,
            2_345.6m, 0.005m, time).Success.Should().BeTrue();
        var store = new TradeSimFileStore(FilePath);

        store.Save(sim.Snapshot());
        var result = store.Load();

        result.Warning.Should().BeNull();
        var restored = new TradeSimulator(result.State);
        restored.Balances.Should().BeEquivalentTo(sim.Balances);
        restored.Trades.Should().BeEquivalentTo(sim.Trades);
        restored.Trades[0].TimeUtc.Kind.Should().Be(DateTimeKind.Utc);
        restored.FeeRate.Should().Be(0.00075m);
        result.State.NextTradeId.Should().Be(3);
        File.Exists(FilePath + ".tmp").Should().BeFalse();
    }

    [TestMethod]
    public void SaveOverwritesExistingFileAndCreatesDirectory()
    {
        var path = Path.Combine(_directory, "sub", "trade-sim.xml");
        var store = new TradeSimFileStore(path);
        var sim = new TradeSimulator();

        store.Save(sim.Snapshot());
        sim.Deposit(10m, out _);
        store.Save(sim.Snapshot());

        store.Load().State.Balances["USDT"].Should().Be(10m);
    }

    [TestMethod]
    public void CorruptFileIsPreservedAndFreshStateIsReturned()
    {
        File.WriteAllText(FilePath, "this is not xml");

        var result = new TradeSimFileStore(FilePath).Load();

        result.Warning.Should().NotBeNullOrEmpty();
        result.State.Balances.Should().BeEquivalentTo(new Dictionary<string, decimal> { ["USDT"] = 0m });
        File.Exists(FilePath).Should().BeFalse();
        var backups = Directory.GetFiles(_directory, "trade-sim.corrupt-*.xml");
        backups.Should().ContainSingle();
        File.ReadAllText(backups[0]).Should().Be("this is not xml");
        result.Warning.Should().Contain(backups[0]);
    }

    [TestMethod]
    public void InvalidStateIsTreatedAsCorrupt()
    {
        var store = new TradeSimFileStore(FilePath);
        store.Save(new TradeSimState { FeeRate = 0.5m });

        var result = store.Load();

        result.Warning.Should().Contain("fee rate");
        result.State.FeeRate.Should().Be(TradeSimState.DefaultFeeRate);
        Directory.GetFiles(_directory, "trade-sim.corrupt-*.xml").Should().ContainSingle();
    }

    [TestMethod]
    public void RepeatedCorruptionsDoNotOverwriteEarlierBackups()
    {
        var store = new TradeSimFileStore(FilePath);

        File.WriteAllText(FilePath, "first");
        store.Load();
        File.WriteAllText(FilePath, "second");
        store.Load();

        Directory.GetFiles(_directory, "trade-sim.corrupt-*.xml").Select(File.ReadAllText)
            .Should().BeEquivalentTo("first", "second");
    }
}
