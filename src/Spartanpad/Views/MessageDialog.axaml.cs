using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Spartanpad.Views;

/// <summary>
/// A modal message with a row of buttons, standing in for <c>MessageBoxW</c> and the save-prompt
/// task dialog. The result is the text of the button pressed, or null when the window is closed
/// another way (Escape, the close button), which callers treat as Cancel.
/// </summary>
public partial class MessageDialog : Window
{
    public MessageDialog()
    {
        InitializeComponent();
    }

    public MessageDialog(string title, string message, IReadOnlyList<string> buttons)
        : this()
    {
        Title = title;
        MessageText.Text = message;
        for (int i = 0; i < buttons.Count; i++)
        {
            string label = buttons[i];
            var button = new Button
            {
                Content = label,
                MinWidth = 88,
                IsDefault = i == 0,
                IsCancel = i == buttons.Count - 1 && buttons.Count > 1,
            };
            button.Click += (_, _) => Close(label);
            Buttons.Children.Add(button);
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (Buttons.Children.Count > 0)
        {
            ((Button)Buttons.Children[0]).Focus();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(null);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    public static Task<string?> ShowAsync(Window owner, string title, string message, params string[] buttons)
    {
        var dialog = new MessageDialog(title, message, buttons);
        return dialog.ShowDialog<string?>(owner);
    }

    public static Task ShowInfoAsync(Window owner, string title, string message) => ShowAsync(owner, title, message, "OK");
}
