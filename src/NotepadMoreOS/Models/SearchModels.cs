namespace NotepadMoreOS.Models;

public sealed record SearchOptions(
    string Pattern,
    bool MatchCase = false,
    bool WholeWord = false,
    bool UseRegex = false,
    int SelectionStart = -1,
    int SelectionLength = 0,
    bool UseExtendedEscapes = false)
{
    public bool ScopeToSelection => SelectionStart >= 0 && SelectionLength > 0;

    public bool HasPattern => !string.IsNullOrEmpty(Pattern);
}

public sealed record SearchMatch(int Offset, int Length, int Line, int Column, string Preview);
