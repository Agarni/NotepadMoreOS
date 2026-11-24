using AvaloniaEdit.Folding;

namespace NotepadMoreOS.Services;

public static class JsonFoldingService
{
    public static IReadOnlyList<NewFolding> Find(string text)
    {
        var stack = new Stack<(char Character, int Offset, int Line)>();
        var folds = new List<NewFolding>();
        var line = 0;
        var inString = false;
        var escaped = false;
        var lineComment = false;
        var blockComment = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\r' || (c == '\n' && (i == 0 || text[i - 1] != '\r'))) { line++; lineComment = false; }
            if (lineComment) continue;
            if (blockComment)
            {
                if (c == '*' && i + 1 < text.Length && text[i + 1] == '/') { blockComment = false; i++; }
                continue;
            }
            if (inString)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
                continue;
            }
            if (c == '"') { inString = true; continue; }
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/') { lineComment = true; i++; continue; }
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*') { blockComment = true; i++; continue; }
            if (c is '{' or '[') stack.Push((c, i, line));
            else if (c is '}' or ']')
            {
                if (stack.Count == 0 || (c == '}' ? stack.Peek().Character != '{' : stack.Peek().Character != '['))
                {
                    stack.Clear();
                    continue;
                }
                var start = stack.Pop();
                if (line > start.Line) folds.Add(new NewFolding(start.Offset, i + 1));
            }
        }
        return folds.OrderBy(f => f.StartOffset).ToArray();
    }
}
