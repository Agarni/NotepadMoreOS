namespace NotepadMoreOS.ViewModels;

public sealed record SearchResultItem(int Offset, int Length, int Line, int Column, string Preview)
{
    public string Location => $"Ln {Line}, Col {Column}";
}
