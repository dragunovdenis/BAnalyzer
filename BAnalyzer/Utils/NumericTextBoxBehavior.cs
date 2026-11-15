//Copyright (c) 2026 Denys Dragunov, dragunovdenis@gmail.com
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

using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace BAnalyzer.Utils;

/// <summary>
/// Restricts the input of a text box to non-negative decimal numbers
/// ("." as the separator, limited number of decimal places) by reverting the edits that break the format.
/// An empty text (or a lone ".") is allowed and means "no value".
/// </summary>
public sealed class NumericTextBoxBehavior
{
    private const int MaxIntegerDigits = 20;

    private readonly Regex _format;

    /// <summary>
    /// Attaches the behavior to the given text box.
    /// </summary>
    /// <remarks>
    /// Attach before subscribing other "TextChanged" handlers
    /// so that they never observe a text that is about to be reverted.
    /// </remarks>
    public static NumericTextBoxBehavior Attach(TextBox textBox, int maxDecimals) => new(textBox, maxDecimals);

    /// <summary>
    /// Constructor.
    /// </summary>
    private NumericTextBoxBehavior(TextBox textBox, int maxDecimals)
    {
        ArgumentNullException.ThrowIfNull(textBox);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDecimals);

        _format = new Regex(maxDecimals > 0 ? $@"^\d{{0,{MaxIntegerDigits}}}(\.\d{{0,{maxDecimals}}})?$" :
            $@"^\d{{0,{MaxIntegerDigits}}}$", RegexOptions.CultureInvariant);

        textBox.TextChanged += TextBox_TextChanged;
    }

    /// <summary>
    /// Returns "true" if the given text is allowed (which does not mean that it represents a value).
    /// </summary>
    private bool IsAllowed(string text) => _format.IsMatch(text);

    /// <summary>
    /// Parses the text of a text box with this behavior attached; returns "null" if it holds no value.
    /// </summary>
    public static decimal? Parse(string text) =>
        decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ?
            value : null;

    /// <summary>
    /// Reverts the edits that make the text invalid, the rightmost first.
    /// </summary>
    private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var tb = (TextBox)sender;

        if (IsAllowed(tb.Text))
            return;

        using (tb.DeclareChangeBlock())
        {
            foreach (var c in e.Changes.OrderByDescending(x => x.Offset))
            {
                if (c.AddedLength == 0) continue;

                tb.Select(c.Offset, c.AddedLength);
                tb.SelectedText = "";

                if (IsAllowed(tb.Text))
                    break;
            }

            if (!IsAllowed(tb.Text))
                tb.Text = string.Empty;
        }
    }
}
