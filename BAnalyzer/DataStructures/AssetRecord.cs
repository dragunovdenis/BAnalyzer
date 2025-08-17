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
using System.Runtime.Serialization;
using BAnalyzer.Utils;

namespace BAnalyzer.DataStructures;

/// <summary>
/// Asset-related information.
/// </summary>
[DataContract]
public class AssetRecord : INotifyPropertyChanged
{
    private string _assetId;

    /// <summary>
    /// Identifier of the asset.
    /// </summary>
    [DataMember]
    public string AssetId
    {
        get => _assetId;
        set => SetField(ref _assetId, value);
    }

    private double _amount;

    /// <summary>
    /// Amount of the asset.
    /// </summary>
    [DataMember]
    public double Amount
    {
        get => _amount;
        set
        {
            if (SetField(ref _amount, value))
            {
                OnPropertyChanged(nameof(CurrentValue));
                OnPropertyChanged(nameof(CurrentProfit));
            }
        }
    }

    private bool _selected;

    /// <summary>
    /// Selection status of the asset.
    /// </summary>
    [DataMember]
    public bool Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

    private double _price;

    /// <summary>
    /// Current price of the asset.
    /// </summary>
    public double Price
    {
        get => _price;
        set
        {
            if (SetField(ref _price, value))
            {
                OnPropertyChanged(nameof(CurrentValue));
                OnPropertyChanged(nameof(CurrentProfit));
            }
        }
    }

    private double _investment;

    /// <summary>
    /// Investment value of the asset.
    /// </summary>
    [DataMember]
    public double Investment
    {
        get => _investment;
        set
        {
            if (SetField(ref _investment, value))
                OnPropertyChanged(nameof(CurrentProfit));
        }
    }

    /// <summary>
    /// Current profit of the asset.
    /// </summary>
    public double CurrentProfit => Profit(Price);

    /// <summary>
    /// Value of the asset given the price.
    /// </summary>
    public double CurrentValue => Value(Price);

    /// <summary>
    /// Returns deep copy of the current instance.
    /// </summary>
    public AssetRecord Copy() => new()
    {
        Amount = Amount,
        AssetId = AssetId,
        Selected = Selected,
        Price = Price,
        Investment = Investment,
    };

    /// <summary>
    /// Returns value of the asset given the <paramref name="price"/>.
    /// </summary>
    public double Value(double price) => Amount * price;

    /// <summary>
    /// Returns profit of the asset given the <paramref name="price"/>.
    /// </summary>
    public double Profit(double price) => Value(price) - Investment;

    /// <summary>
    /// Returns exchange symbol associated with the asset.
    /// </summary>
    public string Symbol => SymbolUtils.AssetToSymbol(AssetId);

    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Raises "property-changed" event.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Sets a field and raises "property-changed" event.
    /// </summary>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}