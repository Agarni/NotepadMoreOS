using System.Text.Json.Serialization;

namespace NotepadMoreOS.Models;

/// <summary>Metadata persisted for crash recovery of the whole workspace.</summary>
public sealed class RecoverySnapshot
{
    public List<RecoveryDocument> Documents { get; set; } = [];
    public int ActiveIndex { get; set; } = -1;
    public DateTime SavedAtUtc { get; set; }
}

public sealed class RecoveryDocument
{
    public string Id { get; set; } = string.Empty;
    public string? Path { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? BackupFile { get; set; }

    [JsonIgnore]
    public string? Content { get; set; }

    public int CaretOffset { get; set; }
    public TextEncodingKind Encoding { get; set; } = TextEncodingKind.Utf8;
    public LineEnding LineEnding { get; set; } = LineEnding.Lf;
    public string LanguageId { get; set; } = "plaintext";
    public bool IsDirty { get; set; }
    public bool IsPinned { get; set; }
    public string? TabColor { get; set; }
    public bool IsReadOnly { get; set; }
}
