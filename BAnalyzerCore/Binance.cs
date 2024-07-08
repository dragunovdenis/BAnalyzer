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

using Binance.Net.Clients;
using Binance.Net.Interfaces;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;

namespace BAnalyzerCore
{
    /// <summary>
    /// Wrapper of the Binance client.
    /// </summary>
    public class Binance : IDisposable
    {
        private readonly BinanceRestClient _client = new();

        /// <summary>
        /// Constructor.
        /// </summary>
        public void Dispose()
        {
            _client.Dispose();
        }

        /// <summary>
        /// Returns all the available coin pairs (in form of strings, called symbols)
        /// </summary>
        public async Task<IList<string>> GetSymbols()
        {
            var exchangeInfo = await _client.SpotApi.ExchangeData.GetExchangeInfoAsync();
            if (!exchangeInfo.Success)
                throw new Exception("Failed to get exchange info");

            return exchangeInfo.Data.Symbols.Select(x => x.Name).ToArray();
        }

        /// <summary>
        /// Gets candle-stick data
        /// </summary>
        public async Task<IList<IBinanceKline>> GetCandleSticks(DateTime startTime, DateTime endTime, KlineInterval granularity, string symbol)
        {
            var klines = await _client.SpotApi.ExchangeData.
                GetKlinesAsync(symbol, granularity, startTime: startTime, endTime: endTime);
            if (!klines.Success)
                throw new Exception("Failed to get exchange info");


            return klines.Data.ToArray();
        }

        /// <summary>
        /// Returns current price for the given symbol.
        /// </summary>
        public async Task<BinancePrice> GetCurrentPrice(string symbol)
        {
            if (symbol is null or "")
                return null;

            var price = await _client.SpotApi.ExchangeData.GetPriceAsync(symbol);

            if (price.Success)
                return price.Data;

            return null!;
        }
    }
}
