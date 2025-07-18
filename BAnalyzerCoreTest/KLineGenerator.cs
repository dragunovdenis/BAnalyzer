﻿//Copyright (c) 2025 Denys Dragunov, dragunovdenis@gmail.com
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

using BAnalyzerCore;
using BAnalyzerCore.Cache;
using BAnalyzerCore.DataStructures;
using Binance.Net.Enums;

namespace BAnalyzerCoreTest;

/// <summary>
/// Functionality to generate k-line series
/// with properties needed to conduct tests.
/// </summary>
internal class KLineGenerator
{
    /// <summary>
    /// Returns a "k-line" block containing <param name="itemCount"/> number of "k-line" items
    /// of the given <param name="granularity"/> which starts at the given <param name="beginTime"/>.
    /// </summary>
    public static KLineBlock GenerateBlock(DateTime beginTime, KlineInterval granularity, int itemCount)
    {
        var result = new KLine[itemCount];
        var time = beginTime;
        var kLineInterval = granularity.ToTimeSpan();
        var rnd = new Random();

        for (var itemId = 0; itemId < itemCount; itemId++)
        {
            var closeTime = time.Add(kLineInterval);
            result[itemId] = new KLine()
            {
                OpenTime = time,
                CloseTime = closeTime.AddSeconds(-BinanceConstants.KLineTimeGapSec),

                // The assignments below are needed to test serialization functionality
                OpenPrice = rnd.NextDouble(),
                ClosePrice = rnd.NextDouble(),
                HighPrice = rnd.NextDouble(),
                LowPrice = rnd.NextDouble(),
                QuoteVolume = rnd.NextDouble(),
                TakerBuyBaseVolume = rnd.NextDouble(),
                TakerBuyQuoteVolume = rnd.NextDouble(),
                TradeCount = rnd.Next(),
                Volume = rnd.NextDouble(),
            };

            time = closeTime;
        }

        return new KLineBlock(granularity, result);
    }
}