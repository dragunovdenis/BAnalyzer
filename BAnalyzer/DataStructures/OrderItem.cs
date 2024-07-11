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

namespace BAnalyzer.DataStructures;

/// <summary>
/// Data struct to represent a single order item.
/// </summary>
public class OrderItem: INotifyPropertyChanged
{
    private double _price;

    /// <summary>
    /// Price of the order;
    /// </summary>
    public double Price
    {
        get => _price;
        set
        {
            if (SetField(ref _price, value))
                OnPropertyChanged(nameof(Volume));
        }
    }

    /// <summary>
    /// Volume of the order in USDT;
    /// </summary>
    public double Volume => _price * _quantity;

    private double _quantity;

    /// <summary>
    /// Quantity of the order;
    /// </summary>
    public double Quantity
    {
        get => _quantity;
        set
        {
            if (SetField(ref _quantity, value))
                OnPropertyChanged(nameof(Volume));
        }
    }

    private bool _selected;

    /// <summary>
    /// Selected property.
    /// </summary>
    public bool Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }
        
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Raises property changed event.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Sets field with the property changed notification.
    /// </summary>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}