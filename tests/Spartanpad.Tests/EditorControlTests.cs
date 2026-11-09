using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AvaloniaEdit;

namespace Spartanpad.Tests;

/// <summary>The editor control on its own under the headless platform: the floor the window tests stand on.</summary>
public class EditorControlTests
{
    [AvaloniaFact]
    public void TakesTypedTextAndHoldsTheFocusInItsTextArea()
    {
        var editor = new TextEditor();
        var window = new Window { Content = editor, Width = 400, Height = 300 };
        window.Show();
        // The text area exists from the start but joins the tree when the template is applied,
        // which happens in the layout pass; focus it after that pass has run.
        Dispatcher.UIThread.RunJobs();
        editor.TextArea.Focus();
        window.KeyTextInput("hi");
        Assert.Equal("hi", editor.Text);
        Assert.True(editor.TextArea.IsFocused);
        Assert.Equal(2, editor.CaretOffset);
        Assert.Equal(1, editor.TextArea.Caret.Line);
        Assert.Equal(3, editor.TextArea.Caret.Column);
        window.Close();
    }
}
