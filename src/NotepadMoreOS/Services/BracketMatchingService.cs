namespace NotepadMoreOS.Services;

/// <summary>Finds a properly nested bracket pair adjacent to the caret.</summary>
public static class BracketMatchingService
{
    public static (int First, int Second)? Find(string text, int caret)
    {
        if (caret < 0 || caret > text.Length) return null;
        var offset = caret < text.Length && IsBracket(text[caret]) ? caret : caret - 1;
        if (offset < 0 || !IsBracket(text[offset])) return null;
        var forward = IsOpening(text[offset]);
        var stack = new Stack<char>();
        for (var i = offset; i >= 0 && i < text.Length; i += forward ? 1 : -1)
        {
            var c = text[i];
            if (!IsBracket(c)) continue;
            if (IsOpening(c) == forward) stack.Push(c);
            else
            {
                if (stack.Count == 0 || !Matches(forward ? stack.Peek() : c, forward ? c : stack.Peek())) return null;
                stack.Pop();
                if (stack.Count == 0) return (Math.Min(offset, i), Math.Max(offset, i));
            }
        }
        return null;
    }

    private static bool IsBracket(char c) => "()[]{}".Contains(c);
    private static bool IsOpening(char c) => "([{".Contains(c);
    private static bool Matches(char open, char close) => (open, close) is ('(', ')') or ('[', ']') or ('{', '}');
}
