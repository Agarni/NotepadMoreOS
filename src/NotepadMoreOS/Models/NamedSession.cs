namespace NotepadMoreOS.Models;

public sealed class NamedSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public List<NamedSessionFile> Files { get; set; } = [];
    public int ActiveIndex { get; set; } = -1;
    public DateTime SavedAtUtc { get; set; }
}

public sealed class NamedSessionFile
{
    /// <summary>Empty for documents that were never saved to disk.</summary>
    public string Path { get; set; } = string.Empty;

    public string? Title { get; set; }

    /// <summary>Relative draft file name inside the session's draft folder.</summary>
    public string? DraftFile { get; set; }

    public int CaretOffset { get; set; }
    public bool IsDirty { get; set; }
    public bool IsPinned { get; set; }
    public string? TabColor { get; set; }
    public bool IsReadOnly { get; set; }
    public TextEncodingKind Encoding { get; set; } = TextEncodingKind.Utf8;
    public LineEnding LineEnding { get; set; } = LineEnding.Lf;
    public string LanguageId { get; set; } = "plaintext";
}
