using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using CobiHw.App.Interop;
using CobiHw.App.Resources;

namespace CobiHw.App;

/// <summary>
/// The app's message box: the same buttons and results as the Windows one (<see cref="MessageBox"/>), with the app's look, theme and language.
/// Paragraphs of the text are separated by a blank line; lines inside a paragraph (a list) stay together.
/// </summary>
public partial class MessageWindow : Window
{
    private static readonly string[] ParagraphBreaks = ["\r\n\r\n", "\n\n"];

    private readonly MessageBoxImage _image;
    private readonly MessageBoxResult _closeResult;
    private readonly string _copyText;
    private MessageBoxResult? _result;

    private MessageWindow(Window? owner, string caption, string text, MessageBoxButton buttons, MessageBoxImage image, string? note)
    {
        WindowTheme.Apply(this, WindowTheme.Current);
        InitializeComponent();
        Title = caption;
        _image = image;

        // Over its window, like the Windows message box. Without one, it has nothing to stay above, and the widget, always on top
        // and often in the middle of the screen, would cover it while the message blocks the app's windows (the widget can't be
        // dragged away): so it stays on top of everything, like the error window, in the middle of the screen and on the taskbar.
        if (owner is not null)
        {
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
        }

        foreach (string paragraph in text.Split(ParagraphBreaks, StringSplitOptions.RemoveEmptyEntries))
        {
            Body.Children.Add(new TextBlock
            {
                Style = (Style)Resources["Paragraph"],
                Margin = new Thickness(0, Body.Children.Count == 0 ? 0 : 12, 0, 0),
                Text = paragraph.Trim('\r', '\n'),
            });
        }

        if (!string.IsNullOrEmpty(note))
        {
            Body.Children.Add(new TextBlock { Style = (Style)Resources["Note"], Text = note });
        }

        // Esc and the X answer like the Windows message box: Cancel when there is one, otherwise No, otherwise OK.
        (MessageBoxResult Result, string Label)[] choices = buttons switch
        {
            MessageBoxButton.OKCancel => [(MessageBoxResult.OK, Strings.Common_Ok), (MessageBoxResult.Cancel, Strings.Common_Cancel)],
            MessageBoxButton.YesNo => [(MessageBoxResult.Yes, Strings.Common_Yes), (MessageBoxResult.No, Strings.Common_No)],
            MessageBoxButton.YesNoCancel => [(MessageBoxResult.Yes, Strings.Common_Yes), (MessageBoxResult.No, Strings.Common_No), (MessageBoxResult.Cancel, Strings.Common_Cancel)],
            _ => [(MessageBoxResult.OK, Strings.Common_Ok)],
        };
        _closeResult = choices[^1].Result;

        for (int i = 0; i < choices.Length; i++)
        {
            MessageBoxResult result = choices[i].Result;
            var button = new Button
            {
                MinWidth = 80,
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(i == 0 ? 0 : 8, 0, 0, 0),
                Content = choices[i].Label,
                IsDefault = i == 0,
                IsCancel = result == _closeResult,
            };

            // The first button is the primary one: Enter presses it, it has the focus, and a screen reader reads the message with it.
            if (i == 0)
            {
                button.SetResourceReference(StyleProperty, "PrimaryButtonStyle");
                AutomationProperties.SetHelpText(button, string.Join(Environment.NewLine, text, note));
                Loaded += (_, _) => button.Focus();
            }

            button.Click += (_, _) =>
            {
                _result = result;
                Close();
            };
            ButtonPanel.Children.Add(button);
        }

        _copyText = string.Join(Environment.NewLine + Environment.NewLine, new[] { caption, text, note }.Where(part => !string.IsNullOrEmpty(part)));
        Loaded += (_, _) => PlaySound();
    }

    /// <summary>Shows the message and waits for the answer, like <see cref="MessageBox.Show(Window, string, string, MessageBoxButton, MessageBoxImage)"/>.</summary>
    /// <param name="owner">Window it belongs to and opens over; null for messages without one, which open in the middle of the screen, on top of every window.</param>
    /// <param name="caption">Title bar text.</param>
    /// <param name="text">Message; a blank line separates paragraphs.</param>
    /// <param name="buttons">Buttons, with the texts the Windows message box uses, in the app's language.</param>
    /// <param name="image">Kind of message; it picks the Windows sound, as the Windows message box does.</param>
    /// <param name="note">Extra information under the message, in a smaller and muted text.</param>
    /// <returns>The button pressed; Esc and the X give Cancel, otherwise No, otherwise OK.</returns>
    public static MessageBoxResult Show(Window? owner, string caption, string text, MessageBoxButton buttons, MessageBoxImage image, string? note = null)
    {
        var window = new MessageWindow(owner, caption, text, buttons, image, note);
        window.ShowDialog();
        return window._result ?? window._closeResult;
    }

    // Ctrl+C copies the title and the text, as in the Windows message box. Another app may be holding the clipboard; then nothing is copied.
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            try
            {
                Clipboard.SetText(_copyText);
            }
            catch (ExternalException)
            {
            }

            e.Handled = true;
        }
    }

    // The Windows sound of the message kind; the MessageBoxImage values are the MB_ICON flags MessageBeep takes.
    private void PlaySound()
    {
        if (_image != MessageBoxImage.None)
        {
            _ = User32.MessageBeep((uint)_image);
        }
    }
}
