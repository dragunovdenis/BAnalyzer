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

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BAnalyzer.DataStructures;

namespace BAnalyzer.Controls;

/// <summary>
/// Interaction logic for OrderSheetControl.xaml
/// </summary>
public partial class OrderSheetControl : INotifyPropertyChanged
{
    private ObservableCollection<OrderItem> _orders = new();

    /// <summary>
    /// Collection of orders to be displayed.
    /// </summary>
    public ObservableCollection<OrderItem> Orders
    {
        get => _orders;
        set => SetField(ref _orders, value);
    }

    private Brush _priceColor = Brushes.Cyan;

    /// <summary>
    /// Color of the items in the "Price" column.
    /// </summary>
    public Brush PriceColor
    {
        get => _priceColor;
        set => SetField(ref _priceColor, value);
    }

    private Brush _backgroundColor = Brushes.White;

    /// <summary>
    /// Color of the background.
    /// </summary>
    public Brush BackgroundColor
    {
        get => _backgroundColor;
        set => SetField(ref _backgroundColor, value);
    }

    private Brush _selectionColor = Brushes.LightGray;

    /// <summary>
    /// Color of selection.
    /// </summary>
    public Brush SelectionColor
    {
        get => _selectionColor;
        set => SetField(ref _selectionColor, value);
    }

    /// <summary>
    /// Event that gets raised each time index of the margin order changes.
    /// </summary>
    public event Action OnMarginOrderChanged;

    /// <summary>
    /// Constructor.
    /// </summary>
    public OrderSheetControl() => InitializeComponent();

    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Raises property changed event.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Updates field and raises property changed event.
    /// </summary>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
    /// <summary>
    /// Row mouse-enter event handler.
    /// </summary>
    private void DataGridRow_OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is DataGridRow row)
        {
            int currentRowId = Sheet.ItemContainerGenerator.IndexFromContainer(row);

            if (Sheet.Items[0] is OrderItem firstOrderItem)
                firstOrderItem.Selected = currentRowId > 0;

            for (int rowId = 1; rowId < Sheet.Items.Count; rowId++)
            {
                if (Sheet.Items[rowId] is OrderItem orderItem)
                    orderItem.Selected = rowId <= currentRowId;
            }

            OnMarginOrderChanged?.Invoke();
        }
    }
}