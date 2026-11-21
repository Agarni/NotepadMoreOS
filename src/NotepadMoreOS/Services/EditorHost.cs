using AvaloniaEdit;
using AvaloniaEdit.Document;

namespace NotepadMoreOS.Services;

public sealed class EditorHost(TextEditor editor) : IEditorHost
{
    public TextDocument Document => editor.Document;

    public int CaretOffset
    {
        get => editor.CaretOffset;
        set => editor.CaretOffset = Math.Clamp(value, 0, editor.Document.TextLength);
    }

    public int SelectionStart => editor.SelectionStart;
    public int SelectionLength => editor.SelectionLength;
    public bool CanUndo => editor.CanUndo;
    public bool CanRedo => editor.CanRedo;

    public void Select(int start, int length)
    {
        start = Math.Clamp(start, 0, editor.Document.TextLength);
        length = Math.Clamp(length, 0, editor.Document.TextLength - start);
        editor.Select(start, length);
        editor.TextArea.Caret.Offset = start;
    }

    public void ScrollToLine(int line) => editor.ScrollToLine(Math.Max(1, line));

    public void Focus() => editor.Focus();

    public void Undo() { if (editor.CanUndo) editor.Undo(); }
    public void Redo() { if (editor.CanRedo) editor.Redo(); }
    public void Cut() { if (editor.CanCut) editor.Cut(); }
    public void Copy() { if (editor.CanCopy) editor.Copy(); }
    public void Paste() { if (editor.CanPaste) editor.Paste(); }
    public void Delete() { if (editor.CanDelete) editor.Delete(); }
    public void SelectAll() => editor.SelectAll();

    public void DuplicateLine()
    {
        var document = editor.Document;
        var line = document.GetLineByOffset(editor.CaretOffset);
        var content = document.GetText(line.Offset, line.Length);
        var delimiter = GetDelimiter(document, line);
        var column = editor.CaretOffset - line.Offset;

        document.Insert(line.EndOffset, delimiter + content);
        editor.CaretOffset = line.EndOffset + delimiter.Length + column;
    }

    public void DeleteLine()
    {
        var document = editor.Document;
        var line = document.GetLineByOffset(editor.CaretOffset);

        if (document.LineCount == 1)
        {
            document.Remove(0, document.TextLength);
            editor.CaretOffset = 0;
            return;
        }

        if (line.LineNumber == document.LineCount && line.DelimiterLength == 0)
        {
            var previous = document.GetLineByNumber(line.LineNumber - 1);
            document.Remove(previous.EndOffset, document.TextLength - previous.EndOffset);
            editor.CaretOffset = previous.EndOffset;
            return;
        }

        document.Remove(line.Offset, line.Length + line.DelimiterLength);
        editor.CaretOffset = Math.Min(line.Offset, document.TextLength);
    }

    public void ToggleComment(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return;
        }

        var document = editor.Document;
        var selectionStart = editor.SelectionStart;
        var selectionEnd = selectionStart + editor.SelectionLength;
        var firstLineNumber = document.GetLineByOffset(selectionStart).LineNumber;
        var lastLineNumber = document.GetLineByOffset(selectionEnd).LineNumber;

        var allCommented = true;
        for (var number = firstLineNumber; number <= lastLineNumber; number++)
        {
            var line = document.GetLineByNumber(number);
            var indent = GetFirstNonWhitespaceOffset(document, line);
            if (!document.GetText(indent, Math.Min(prefix.Length, line.EndOffset - indent))
                    .Equals(prefix, StringComparison.Ordinal))
            {
                allCommented = false;
                break;
            }
        }

        document.UndoStack.StartUndoGroup();
        try
        {
            // Work bottom-up so earlier offsets stay valid while inserting or removing prefixes.
            for (var number = lastLineNumber; number >= firstLineNumber; number--)
            {
                var line = document.GetLineByNumber(number);
                var indent = GetFirstNonWhitespaceOffset(document, line);

                if (allCommented)
                {
                    var removeLength = Math.Min(prefix.Length, line.EndOffset - indent);
                    document.Remove(indent, removeLength);
                    if (indent < document.TextLength && document.GetCharAt(indent) == ' ')
                    {
                        document.Remove(indent, 1);
                    }
                }
                else
                {
                    document.Insert(indent, prefix + " ");
                }
            }
        }
        finally
        {
            document.UndoStack.EndUndoGroup();
        }

        var firstLine = document.GetLineByNumber(firstLineNumber);
        var lastLine = document.GetLineByNumber(lastLineNumber);
        Select(firstLine.Offset, lastLine.EndOffset - firstLine.Offset);
    }

    private static int GetFirstNonWhitespaceOffset(TextDocument document, DocumentLine line)
    {
        var offset = line.Offset;
        while (offset < line.EndOffset)
        {
            var c = document.GetCharAt(offset);
            if (c is not (' ' or '\t'))
            {
                break;
            }

            offset++;
        }

        return offset;
    }

    private static string GetDelimiter(TextDocument document, DocumentLine line)
    {
        if (line.DelimiterLength > 0)
        {
            return document.GetText(line.EndOffset, line.DelimiterLength);
        }

        if (line.LineNumber > 1)
        {
            var previous = document.GetLineByNumber(line.LineNumber - 1);
            if (previous.DelimiterLength > 0)
            {
                return document.GetText(previous.EndOffset, previous.DelimiterLength);
            }
        }

        return Environment.NewLine;
    }
}
