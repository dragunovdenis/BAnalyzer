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

namespace BAnalyzerCore.DataStructures;

/// <summary>
/// Read-only interface for the order-book.
/// </summary>
public interface IOrderBook
{
    /// <summary>
    /// The symbol of the order book.
    /// </summary>
    string Symbol { get; }

    /// <summary>
    /// The list of bids.
    /// </summary>
    IOrderBookEntry[] Bids { get; }

    /// <summary>
    /// The list of asks.
    /// </summary>
    IOrderBookEntry[] Asks { get; }
}

/// <summary>
/// Implementation of the order book.
/// </summary>
internal record OrderBook(string Symbol, IOrderBookEntry[] Bids, IOrderBookEntry[] Asks) : IOrderBook;