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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BAnalyzer.Controls;

/// <summary>
/// A "push-to-talk" button: instead of "clicking", it reports being held down
/// (with the left mouse button or the space bar) and being released.
/// </summary>
/// <remarks>
/// The button keeps the mouse captured while it is held down, so a release
/// outside of it (or a loss of the capture, for example because the window
/// has been deactivated or the button disabled) ends the "talk" as well.
/// </remarks>
public class PushToTalkButton : Button
{
    static PushToTalkButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PushToTalkButton),
            new FrameworkPropertyMetadata(typeof(Button)));
    }

    private bool _talking;

    /// <summary>
    /// Raised when the button is pressed.
    /// </summary>
    public event EventHandler TalkStarted;

    /// <summary>
    /// Raised when the button is released (once per <see cref="TalkStarted"/>).
    /// </summary>
    public event EventHandler TalkEnded;

    private void StartTalk()
    {
        if (_talking) return;

        _talking = true;
        TalkStarted?.Invoke(this, EventArgs.Empty);
    }

    private void EndTalk()
    {
        if (!_talking) return;

        _talking = false;
        TalkEnded?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        e.Handled = true;

        if (CaptureMouse())
            StartTalk();
    }

    /// <inheritdoc/>
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        e.Handled = true;

        EndTalk();
        ReleaseMouseCapture();
    }

    /// <inheritdoc/>
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        EndTalk();
    }

    /// <inheritdoc/>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Key != Key.Space) return;

        e.Handled = true;

        if (!e.IsRepeat)
            StartTalk();
    }

    /// <inheritdoc/>
    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        base.OnPreviewKeyUp(e);

        if (e.Key != Key.Space) return;

        e.Handled = true;
        EndTalk();
    }
}
