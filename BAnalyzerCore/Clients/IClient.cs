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

using BAnalyzerCore.DataStructures;

namespace BAnalyzerCore.Clients;

/// <summary>
/// General interface of exchange clients.
/// </summary>
public interface IClient : IDisposable
{
    /// <summary>
    /// Returns all the exchange symbols.
    /// </summary>
    Task<IList<string>> GetSymbolsAsync();

    /// <summary>
    /// Returns collection of supported granularities ordered in
    /// ascending order with respect to the time-spans they represent.
    /// </summary>
    IReadOnlyList<TimeGranularity> Granularities { get; }

    /// <summary>
    /// Returns ordered collection of k-lines retrieved from
    /// the client according to the given set of parameters.
    /// </summary>
    Task<(IList<KLine> KLines, bool Success)> GetKLinesAsync(string symbol, TimeGranularity granularity,
        DateTime? startTime, DateTime? endTime, int? limit);

    /// <summary>
    /// Returns current spot-price and the related information for the given <paramref name="symbol"/>.
    /// </summary>
    Task<IPriceData> GetPriceAsync(string symbol);

    /// <summary>
    /// Returns the current order-book for the given <paramref name="symbol"/>.
    /// </summary>
    Task<IOrderBook> GetOrderBookAsync(string symbol);

    /// <summary>
    /// Minimal time (UTC) that the given client can process.
    /// </summary>
    DateTime MinTime { get; }

    /// <summary>
    /// Maximal number of k-lines the client can retrieve in a single request.
    /// </summary>
    int MaxKLinesPerTime { get; }
}