namespace NotepadMoreOS.Models;

public sealed class AppSettings
{
    public string Theme { get; set; } = "DarkPlus";
    public string AccentColor { get; set; } = "#5BD08E";
    public string TabStyle { get; set; } = Models.TabStyles.Rounded;
    public string Language { get; set; } = "system";
    public string FontFamily { get; set; } = "Menlo, Cascadia Code, Consolas, monospace";
    public double FontSize { get; set; } = 14;
    public bool WordWrap { get; set; }
    public bool ShowLineNumbers { get; set; } = true;
    public bool ShowWhitespace { get; set; }
    public bool ShowEndOfLine { get; set; }
    public bool HighlightCurrentLine { get; set; } = true;
    public int TabSize { get; set; } = 4;
    public bool ConvertTabsToSpaces { get; set; }
    public bool EnableColumnRuler { get; set; }
    public bool RestoreSession { get; set; } = true;
    public bool RecoveryEnabled { get; set; } = true;

    public List<string> RecentFiles { get; set; } = [];
    public List<NamedSession> Sessions { get; set; } = [];
    public string? CurrentSessionName { get; set; }
    public SessionState Session { get; set; } = new();
}

public sealed class SessionState
{
    public List<SessionDocument> Documents { get; set; } = [];
    public int ActiveIndex { get; set; } = -1;
}

public sealed class SessionDocument
{
    public string Path { get; set; } = string.Empty;
    public int CaretOffset { get; set; }
    public bool IsPinned { get; set; }
    public string? TabColor { get; set; }
    public bool IsReadOnly { get; set; }
}
