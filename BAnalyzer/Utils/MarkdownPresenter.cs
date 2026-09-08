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

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Markdig;

namespace BAnalyzer.Utils;

/// <summary>
/// Renders the "markdown" the models produce (tables, lists, emphasis, code)
/// into a flow document that can be displayed by a <see cref="RichTextBox"/>.
/// </summary>
/// <remarks>
/// The renderer is attached to the control instead of being a value converter
/// because <see cref="RichTextBox.Document"/> is not a dependency property and
/// thus can not be data-bound directly.
/// </remarks>
public static class MarkdownPresenter
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions().Build();

    /// <summary>
    /// The "markdown" to display in the control it is attached to.
    /// </summary>
    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.RegisterAttached("Markdown", typeof(string), typeof(MarkdownPresenter),
            new PropertyMetadata(null, OnMarkdownChanged));

    /// <summary>
    /// Setter of <see cref="MarkdownProperty"/>.
    /// </summary>
    public static void SetMarkdown(DependencyObject element, string value) =>
        element.SetValue(MarkdownProperty, value);

    /// <summary>
    /// Getter of <see cref="MarkdownProperty"/>.
    /// </summary>
    public static string GetMarkdown(DependencyObject element) =>
        (string)element.GetValue(MarkdownProperty);

    /// <summary>
    /// Rebuilds the document each time the source text changes.
    /// </summary>
    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RichTextBox box) return;

        var text = e.NewValue as string;

        box.Document = string.IsNullOrWhiteSpace(text)
            ? new FlowDocument()
            : Build(text);
    }

    /// <summary>
    /// Converts the given "markdown" into a flow document.
    /// </summary>
    /// <remarks>
    /// A model can emit a malformed "markdown", which must not be able to take
    /// the chat window down, so the text is shown "as is" if the conversion fails.
    /// </remarks>
    private static FlowDocument Build(string markdown)
    {
        FlowDocument document;

        try
        {
            document = Markdig.Wpf.Markdown.ToFlowDocument(markdown, Pipeline);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed to render the markdown: {e.Message}");
            document = new FlowDocument(new Paragraph(new Run(markdown)));
        }

        // The document is displayed inside a themed bubble, so it must not
        // impose the "paged" look and the colors of its own.
        document.PagePadding = new Thickness(0);
        document.Background = null;
        document.FontFamily = SystemFonts.MessageFontFamily;

        ApplyThemeForeground(document.Blocks);

        return document;
    }

    /// <summary>
    /// Key of the brush the rest of the application draws its text with.
    /// </summary>
    private const string ForegroundKey = "ForegroundColor";

    /// <summary>
    /// Re-colors the given blocks with the foreground brush of the current theme.
    /// </summary>
    private static void ApplyThemeForeground(IEnumerable<Block> blocks)
    {
        foreach (var block in blocks)
        {
            block.SetResourceReference(TextElement.ForegroundProperty, ForegroundKey);

            switch (block)
            {
                case Paragraph paragraph:
                    ApplyThemeForeground(paragraph.Inlines);
                    break;
                case Section section:
                    ApplyThemeForeground(section.Blocks);
                    break;
                case List list:
                    foreach (var item in list.ListItems)
                        ApplyThemeForeground(item.Blocks);
                    break;
                case Table table:
                    foreach (var cell in table.RowGroups
                                 .SelectMany(g => g.Rows).SelectMany(r => r.Cells))
                        ApplyThemeForeground(cell.Blocks);
                    break;
            }
        }
    }

    /// <summary>
    /// Re-colors the given inlines with the foreground brush of the current theme.
    /// </summary>
    private static void ApplyThemeForeground(IEnumerable<Inline> inlines)
    {
        foreach (var inline in inlines)
        {
            // A link is left alone: its color is what tells it apart from
            // the ordinary text.
            if (inline is Hyperlink) continue;

            inline.SetResourceReference(TextElement.ForegroundProperty, ForegroundKey);

            if (inline is Span span)
                ApplyThemeForeground(span.Inlines);
        }
    }

    /// <summary>
    /// Indicates that the given "markdown" contains a table, i.e. a construct
    /// that does not fit the width an ordinary message is displayed within.
    /// </summary>
    public static bool ContainsTable(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return false;

        // A table is recognized by its delimiter row (the one that separates
        // the header from the body), because that is the only line whose shape
        // is fixed by the syntax: pipes, dashes, colons and spaces alone.
        foreach (var line in markdown.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.Length < 3 || !trimmed.Contains('-') || !trimmed.Contains('|')) continue;

            if (trimmed.All(c => c is '|' or '-' or ':' or ' ' or '\r')) return true;
        }

        return false;
    }
}
