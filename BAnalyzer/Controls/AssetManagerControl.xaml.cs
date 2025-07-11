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

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using BAnalyzer.DataStructures;

namespace BAnalyzer.Controls;

/// <summary>
/// Interaction logic for AssetManagerControl.xaml
/// </summary>
public partial class AssetManagerControl : INotifyPropertyChanged
{
    /// <summary>
    /// General method to initialize dependency properties in this class.
    /// </summary>
    private static DependencyProperty InitProperty<T>(string propertyName, T defaultValue)
    {
        return DependencyProperty.Register(propertyName, typeof(T),
            typeof(AssetManagerControl), new PropertyMetadata(defaultValue: defaultValue));
    }

    /// <summary>
    /// Dependency property.
    /// </summary>
    public static readonly DependencyProperty AssetsProperty =
        InitProperty(nameof(Assets), new ObservableCollection<AssetRecord>());

    /// <summary>
    /// Collection of th managed assets.
    /// </summary>
    public ObservableCollection<AssetRecord> Assets
    {
        get => (ObservableCollection<AssetRecord>)GetValue(AssetsProperty);
        set => SetValue(AssetsProperty, value);
    }

    /// <summary>
    /// Dependency property.
    /// </summary>
    public static readonly DependencyProperty SymbolsProperty =
        InitProperty<ObservableCollection<string>>(nameof(Symbols), null);

    /// <summary>
    /// Available exchange symbols.
    /// </summary>
    public ObservableCollection<string> Symbols
    {
        get => (ObservableCollection<string>)GetValue(SymbolsProperty);
        set => SetValue(SymbolsProperty, value);
    }

    private DispatcherTimer _updateTimer;

    /// <summary>
    /// Constructor.
    /// </summary>
    public AssetManagerControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Activates the control.
    /// </summary>
    public void Activate()
    {
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += (_, _) => AssetGrid.Items.Refresh();
        _updateTimer.Start();
    }

    /// <summary>
    /// De-activates the control.
    /// </summary>
    public void Deactivate()
    {
        _updateTimer.Stop();
        _updateTimer = null;
    }

    /// <summary>
    /// Asset record selection handler.
    /// </summary>
    private void OnAssetSelectionChanged(object sender, RoutedEventArgs e) => OnPropertyChanged(nameof(Assets));

    /// <summary>
    /// Returns "true" if the given text can be converted into a double precision number.
    /// </summary>
    private static bool IsNumber(string text) => double.TryParse(text, CultureInfo.InvariantCulture, out _);

    /// <summary>
    /// Converts given text to double precision number.
    /// </summary>
    private static double ToNumber(string text)
    {
        double.TryParse(text, CultureInfo.InvariantCulture, out var result);
        return result;
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var tb = (TextBox)sender;

        if (IsNumber(tb.Text))
            return;

        using (tb.DeclareChangeBlock())
        {
            var orderedChanges = e.Changes.
                OrderByDescending(x => x.Offset);

            // Revert changes one by one starting with the rightmost ones
            // until the text of the constitutes a valid number
            foreach (var c in orderedChanges)
            {
                if (c.AddedLength == 0) continue;
                tb.Select(c.Offset, c.AddedLength);
                tb.SelectedText = "";

                if (IsNumber(tb.Text))
                    break;
            }
        }
    }

    /// <summary>
    /// Returns index of the record that corresponds to the given <param name="assetId"/>
    /// in the collection of assets.
    /// </summary>
    private int IndexOfAssetRecord(string assetId)
    {
        for (var i = 0; i < Assets.Count; i++)
            if (Assets[i].AssetId == assetId) return i;

        return -1;
    }

    /// <summary>
    /// Updates the selected asset.
    /// </summary>
    private void UpdateAsset()
    {
        if (!IsNumber(AssetAmountBox.Text) || !Symbols.Contains(AssetBox.Text))
        {
            AssetAmountBox.Text = "0";
            return;
        }

        var assetId = AssetBox.Text;
        var indexOfExistingRecord = IndexOfAssetRecord(assetId);
        var amount = ToNumber(AssetAmountBox.Text);

        if (amount <= 0)
        {
            if (indexOfExistingRecord >= 0)
                Assets.RemoveAt(indexOfExistingRecord);

            AssetAmountBox.Text = "0";
            return;
        }

        var assetRecord = new AssetRecord()
        {
            Amount = ToNumber(AssetAmountBox.Text),
            AssetId = AssetBox.Text,
            Selected = indexOfExistingRecord < 0 || Assets[indexOfExistingRecord].Selected,
        };

        if (indexOfExistingRecord < 0)
            Assets.Add(assetRecord);
        else
            Assets[indexOfExistingRecord] = assetRecord;
    }

    /// <summary>
    /// Event handler.
    /// </summary>
    private void UpdateButton_OnClick(object sender, RoutedEventArgs e) => UpdateAsset();

    /// <summary>
    /// Event handler.
    /// </summary>
    private void AssetBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0)
        {
            var assetId = (string)e.AddedItems[0];
            var indexOfExistingRecord = IndexOfAssetRecord(assetId);

            if (indexOfExistingRecord < 0)
            {
                AssetGrid.SelectedItem = null;
                AssetAmountBox.Text = "0";
                return;
            }

            AssetGrid.SelectedItem = AssetGrid.Items[indexOfExistingRecord];
        }
    }

    /// <summary>
    /// Event handler.
    /// </summary>
    private void AssetGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is AssetRecord record)
        {
            AssetBox.SelectedItem = record.AssetId;
            AssetAmountBox.Text = record.Amount.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Event handler.
    /// </summary>
    private void AssetAmountBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter) UpdateAsset();
    }

    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Raises a "property-changed" event.
    /// </summary>
    /// <param name="propertyName"></param>
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}