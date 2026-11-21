namespace NotepadMoreOS.Models;

public enum TextEncodingKind
{
    Utf8,
    Utf8Bom,
    Utf16Le,
    Utf16Be,
    Windows1252,
    Iso88591
}

public sealed record EncodingOption(TextEncodingKind Kind, string Name)
{
    public override string ToString() => Name;
}

public static class EncodingCatalog
{
    public static IReadOnlyList<EncodingOption> All { get; } =
    [
        new(TextEncodingKind.Utf8, "UTF-8"),
        new(TextEncodingKind.Utf8Bom, "UTF-8 with BOM"),
        new(TextEncodingKind.Utf16Le, "UTF-16 LE"),
        new(TextEncodingKind.Utf16Be, "UTF-16 BE"),
        new(TextEncodingKind.Windows1252, "ANSI (Windows-1252)"),
        new(TextEncodingKind.Iso88591, "ISO-8859-1")
    ];

    public static EncodingOption Get(TextEncodingKind kind) =>
        All.First(option => option.Kind == kind);
}
