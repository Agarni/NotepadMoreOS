namespace NotepadMoreOS.Models;

public sealed record LanguageInfo(string Id, string Name, string Scope, bool IsPlainText = false)
{
    public static LanguageInfo PlainText { get; } = new("plaintext", "Plain Text", string.Empty, true);

    public override string ToString() => Name;
}
