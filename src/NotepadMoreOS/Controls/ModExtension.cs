using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace NotepadMoreOS.Controls;

/// <summary>
/// Creates a platform-appropriate primary shortcut: Cmd on macOS, Ctrl elsewhere.
/// Usage in XAML: <c>{controls:Mod Key=N}</c> or <c>{controls:Mod Key=S, Modifiers=Shift}</c>.
/// </summary>
public sealed class ModExtension : MarkupExtension
{
    public string Key { get; set; } = "A";

    public string? Modifiers { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var key = Enum.Parse<Key>(Key, ignoreCase: true);
        var modifiers = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

        if (!string.IsNullOrWhiteSpace(Modifiers))
        {
            foreach (var part in Modifiers.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                modifiers |= Enum.Parse<KeyModifiers>(part, ignoreCase: true);
            }
        }

        return new KeyGesture(key, modifiers);
    }
}
