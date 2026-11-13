using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Spartanpad.Core;
using EditorDocument = AvaloniaEdit.Document.TextDocument;

namespace Spartanpad;

/// <summary>
/// One open document as the window holds it: the file side (<see cref="TextDocument"/>), the text
/// with its own undo history (the editor's document), and where the caret, selection and scroll
/// position were when the editor last showed it. The window has one editor control and hands it
/// the active tab's text, so a window with tabs turned off is simply a window with one of these.
/// </summary>
internal sealed class DocumentTab(TextDocument document, EditorDocument text)
{
    /// <summary>File, encoding, line ending and dirty state.</summary>
    public TextDocument Document { get; } = document;

    /// <summary>The text and its undo stack. Replaced whenever a file is loaded into the tab.</summary>
    public EditorDocument Text { get; set; } = text;

    public int CaretOffset { get; set; }

    public int SelectionStart { get; set; }

    public int SelectionLength { get; set; }

    public Vector ScrollOffset { get; set; }

    /// <summary>The tab's entry in the strip and the label inside it; null until the tab is added to the window.</summary>
    public TabStripItem? Header { get; set; }

    public TextBlock? HeaderText { get; set; }

    /// <summary>An Untitled document nobody has typed in: Open may put a file here instead of in a new tab.</summary>
    public bool IsPristine => Document.IsUntitled && Text.TextLength == 0 && !Text.UndoStack.CanUndo && !Text.UndoStack.CanRedo;

    /// <summary>What the strip shows: the file name, with the title bar's <c>*</c> while there are unsaved changes.</summary>
    public string Caption => (Document.IsDirty ? "*" : string.Empty) + Document.DisplayName;

    public void ResetView(int caretOffset = 0)
    {
        CaretOffset = caretOffset;
        SelectionStart = caretOffset;
        SelectionLength = 0;
        ScrollOffset = default;
    }
}
