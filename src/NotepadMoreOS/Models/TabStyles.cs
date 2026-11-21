using NotepadMoreOS.Resources;

namespace NotepadMoreOS.Models;

/// <summary>Identifiers for the tab strip appearance options.</summary>
public static class TabStyles
{
    public const string Rounded = "Rounded";
    public const string Floating = "Floating";
    public const string Connected = "Connected";

    public static IReadOnlyList<TabStyleInfo> All { get; } =
    [
        new(Rounded, "TabStyle_Rounded"),
        new(Floating, "TabStyle_Floating"),
        new(Connected, "TabStyle_Connected")
    ];
}

public sealed record TabStyleInfo(string Id, string NameKey)
{
    public string DisplayName => Strings.Get(NameKey);

    public override string ToString() => DisplayName;
}
