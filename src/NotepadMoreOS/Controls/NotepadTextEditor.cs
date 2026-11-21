using AvaloniaEdit;
using AvaloniaEdit.Document;

namespace NotepadMoreOS.Controls;

/// <summary>
/// A <see cref="TextEditor"/> that starts with a document so internal template bindings
/// never observe a null document while the view is being realized.
/// </summary>
public sealed class NotepadTextEditor : TextEditor
{
    public NotepadTextEditor()
    {
        Document = new TextDocument();
    }

    protected override Type StyleKeyOverride => typeof(TextEditor);
}
