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

using System.ComponentModel;
using System.Runtime.CompilerServices;
using BAnalyzer.DataStructures;
using BAnalyzer.Utils;
using Binance.Net.Objects.Models;
using Binance.Net.Objects.Models.Spot;

namespace BAnalyzer.Controls;

/// <summary>
/// Interaction logic for ExchangeOrdersControl.xaml
/// </summary>
public partial class ExchangeOrdersControl : INotifyPropertyChanged
{
    /// <summary>
    /// Constructor.
    /// </summary>
    public ExchangeOrdersControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Updates target collection with the elements from the source collection.
    /// </summary>
    private static void UpdateCollectionItemWise(OrderSheetControl control,
        IList<BinanceOrderBookEntry> source)
    {
        if (control.Orders.Count != source.Count)
        {
            control.Orders.Clear();

            foreach (var item in source)
                control.Orders.Add(new OrderItem
                {
                    Price = (double)item.Price,
                    Quantity = (double)item.Quantity,
                });

            return;
        }

        for (var itemId = 0; itemId < source.Count; itemId++)
        {
            control.Orders[itemId].Price = (double)source[itemId].Price;
            control.Orders[itemId].Quantity = (double)source[itemId].Quantity;
        }
    }

    private string _baseAssetAbbreviation;
    
    /// <summary>
    /// Updates visualization according to the content of the given order book.
    /// </summary>
    public void Update(BinanceOrderBook book)
    {
        if (book != null)
        {
            UpdateCollectionItemWise(AskSheet, book.Asks.ToArray());
            UpdateCollectionItemWise(BidSheet, book.Bids.ToArray());
            _baseAssetAbbreviation = book.Symbol.Substring(0, book.Symbol.Length - 4);
        }
        else
        {
            AskSheet.Orders.Clear();
            BidSheet.Orders.Clear();
            _baseAssetAbbreviation = null;
        }

        UpdateBidVolumeInfo();
        UpdateAskVolumeInfo();
    }

    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Raises the "property changed" event.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// General field setter.
    /// </summary>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Calculates volume of selected orders in the given collection
    /// (or of all the orders if none of them are selected)
    /// </summary>
    private static (double VolumeUsdt, double VolumeCurrency)
        CalculateOrderVolume(IReadOnlyList<OrderItem> orders)
    {
        double volumeSelectedUsdt = 0;
        double volumeSelectedCurrency = 0;
        double volumeTotalUsdt = 0;
        double volumeTotalCurrency = 0;
        bool hasSelectedOrders = false;
        
        for (var orderId = 0; orderId < orders.Count; orderId++)
        {
            if (orders[orderId].Selected)
            {
                volumeSelectedUsdt += orders[orderId].Volume;
                volumeSelectedCurrency += orders[orderId].Quantity;
                hasSelectedOrders = true;
            }

            volumeTotalUsdt += orders[orderId].Volume;
            volumeTotalCurrency += orders[orderId].Quantity;
        }
            
        return hasSelectedOrders ? (volumeSelectedUsdt, volumeSelectedCurrency) : (volumeTotalUsdt, volumeTotalCurrency);
    }

    /// <summary>
    /// Build volume info string according to the given data.
    /// </summary>
    private string BuildVolumeInfoString(double volumeUsdt, double volumeCurrency) =>
        $"{DataFormatter.FormatApproxCompact(volumeUsdt)} USDT/{DataFormatter.FormatApproxCompact(volumeCurrency)} {_baseAssetAbbreviation}";
    
    /// <summary>
    /// Updates information about "aggregate" volume of the "bid" orders.
    /// </summary>
    private void UpdateBidVolumeInfo()
    {
        var (volumeUsdt, volumeCurrency) = CalculateOrderVolume(BidSheet.Orders);
        BidText.Text = $"Bid vol: {BuildVolumeInfoString(volumeUsdt, volumeCurrency)}";
    }

    /// <summary>
    /// Margin order change event handler.
    /// </summary>
    private void BidSheet_OnOnMarginOrderChanged() => UpdateBidVolumeInfo();

    /// <summary>
    /// Updates information about "aggregate" volume of the "ask" orders.
    /// </summary>
    private void UpdateAskVolumeInfo()
    {
        var (volumeUsdt, volumeCurrency) = CalculateOrderVolume(AskSheet.Orders);
        AskText.Text = $"Ask vol: {BuildVolumeInfoString(volumeUsdt, volumeCurrency)}";
    }

    /// <summary>
    /// Margin order change event handler.
    /// </summary>
    private void AskSheet_OnOnMarginOrderChanged() => UpdateAskVolumeInfo();
}