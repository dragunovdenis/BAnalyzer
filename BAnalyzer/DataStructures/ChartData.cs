//Copyright (c) 2024 Denys Dragunov, dragunovdenis@gmail.com
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
using Binance.Net.Objects.Models.Spot;
using ScottPlot;

namespace BAnalyzer.DataStructures;

/// <summary>
/// Sticks and price data struct.
/// </summary>
public class ChartData(List<OHLC> sticks, List<double> tradeVolumeData,
    BinancePrice price, DateTime exchangeStamp, DateTime timeFrameStamp)
{

    /// <summary>
    /// Constructor.
    /// </summary>
    public ChartData(IList<IBinanceKline> candleStickData, BinancePrice price, DateTime exchangeStamp, DateTime timeFrameStamp) :
        this(ToOhlc(candleStickData), ToTradeVolumes(candleStickData), price, exchangeStamp, timeFrameStamp)
    {}

    /// <summary>
    /// Converter.
    /// </summary>
    private static List<OHLC> ToOhlc(IList<IBinanceKline> candleStickData)
    {
        return candleStickData.Select(x => new OHLC()
        {
            Close = (double)x.ClosePrice,
            Open = (double)x.OpenPrice,
            High = (double)x.HighPrice,
            Low = (double)x.LowPrice,
            TimeSpan = x.CloseTime - x.OpenTime,
            DateTime = x.OpenTime.Add(0.5 * (x.CloseTime - x.OpenTime)).ToLocalTime()
        }).ToList();
    }

    /// <summary>
    /// Converter.
    /// </summary>
    private static List<double> ToTradeVolumes(IList<IBinanceKline> candleStickData) =>
        candleStickData.Select(x => (double)x.QuoteVolume).ToList();

    /// <summary>
    /// Sticks.
    /// </summary>
    public List<OHLC> Sticks { get; } = sticks;

    /// <summary>
    /// Trade volume data for each stick.
    /// </summary>
    public List<double> TradeVolumeData { get; } = tradeVolumeData;

    /// <summary>
    /// Array of stick times in OLEA format.
    /// </summary>
    private readonly double[] _times = sticks.Select(x => x.DateTime.ToOADate()).ToArray();

    /// <summary>
    /// Price.
    /// </summary>
    public BinancePrice Price { get; } = price;

    /// <summary>
    /// Time stamp of the exchange data the current instance was built from.
    /// </summary>
    public DateTime ExchangeStamp { get; } = exchangeStamp;

    /// <summary>
    /// Time stamp of the time frame data the current instance was built from.
    /// </summary>
    public DateTime TimeFrameStamp { get; } = timeFrameStamp;

    /// <summary>
    /// Returns the time of the opening of the first candle-stick.
    /// </summary>
    public DateTime GetBeginTime()
    {
        if (Sticks == null || Sticks.Count == 0)
            throw new InvalidOperationException("Invalid collection of candle-sticks.");

        var stickSpan = Sticks.First().TimeSpan;
        return Sticks.First().DateTime.Add(-0.5 * stickSpan);
    }

    /// <summary>
    /// Returns the time of the closing of the last candle-stick.
    /// </summary>
    public DateTime GetEndTime()
    {
        if (Sticks == null || Sticks.Count == 0)
            throw new InvalidOperationException("Invalid collection of candle-sticks.");

        var stickSpan = Sticks.First().TimeSpan;
        return Sticks.Last().DateTime.Add(0.5 * stickSpan);
    }

    /// <summary>
    /// Returns "true" if the current instance was qualified as "valid".
    /// </summary>
    /// <returns></returns>
    public bool IsValid() => Sticks is { Count: > 0 };
        
    /// <summary>
    /// Returns "true" if the last candle-stick in the corresponding collection is green.
    /// </summary>
    /// <returns></returns>
    public bool IsPriceUp()
    {
        if (Sticks == null || Sticks.Count == 0)
            throw new InvalidOperationException("Invalid collection of candle-sticks.");

        return Sticks.Last().IsGreen();
    }

    /// <summary>
    /// Returns index of a stick that corresponds to the given time-point
    /// in OLE Automation date equivalent format or "-1" if the given time
    /// point falls outside the time-frame of the chart.
    /// </summary>
    private int GetStickId(double timeOa)
    {
        if (GetBeginTime().ToOADate() > timeOa ||
            GetEndTime().ToOADate() < timeOa)
            return -1;

        var id = Array.BinarySearch(_times, timeOa);

        if (id >= 0)
            return id;

        var idInverted = ~id;

        if (idInverted == 0)
            return 0;

        if (idInverted == _times.Length)
            return _times.Length - 1;

        return _times[idInverted] - timeOa < timeOa - _times[idInverted - 1] ? idInverted : idInverted - 1;
    }

    /// <summary>
    /// Returns index of a stick that corresponds to the given time-point
    /// in OLE Automation date equivalent format and the given price or "-1"
    /// if such a stick can't be found.
    /// </summary>
    public int GetStickId(double timeOa, double price)
    {
        var preliminaryId = GetStickId(timeOa);

        if (preliminaryId < 0)
            return -1;

        var stick = Sticks[preliminaryId];

        return (stick.Low <= price && stick.High >= price) ? preliminaryId : -1;
    }

    /// <summary>
    /// Returns index of trade volume bar (which coincides with the index of the corresponding stick)
    /// that corresponds to the given time-point in OLE Automation date equivalent format and the given price or "-1"
    /// if such a stick can't be found.
    /// </summary>
    public int GetVolumeBarId(double timeOa, double price)
    {
        if (price <= 0)
            return -1;

        var preliminaryId = GetStickId(timeOa);

        if (preliminaryId < 0)
            return -1;

        var volume = TradeVolumeData[preliminaryId];

        return volume >= price ? preliminaryId : -1;
    }
}