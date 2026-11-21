using AvaloniaEdit.Document;

namespace NotepadMoreOS.Services;

/// <summary>The small surface of the editor that the view model needs to drive.</summary>
public interface IEditorHost
{
    TextDocument Document { get; }

    int CaretOffset { get; set; }
    int SelectionStart { get; }
    int SelectionLength { get; }

    bool CanUndo { get; }
    bool CanRedo { get; }

    void Select(int start, int length);
    void ScrollToLine(int line);
    void Focus();

    void Undo();
    void Redo();
    void Cut();
    void Copy();
    void Paste();
    void Delete();
    void SelectAll();

    void DuplicateLine();
    void DeleteLine();
    void ToggleComment(string prefix);
}
