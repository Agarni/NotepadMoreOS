using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using TextMateSharp.Grammars;
using TextMateSharp.Themes;

namespace NotepadMoreOS.Services;

public sealed record ThemeInfo(ThemeName Name, string DisplayName, bool IsDark)
{    public string PreviewBackground => Name switch
    {
        ThemeName.SolarizedDark => "#002B36",
        ThemeName.Dracula => "#282A36",
        ThemeName.Monokai or ThemeName.DimmedMonokai => "#272822",
        ThemeName.TomorrowNightBlue => "#002451",
        ThemeName.KimbieDark => "#221A0F",
        ThemeName.SolarizedLight => "#FDF6E3",
        ThemeName.QuietLight => "#F5F5F5",
        _ => IsDark ? "#1E1F23" : "#FFFFFF"
    };

    public string PreviewForeground => IsDark
        ? Name == ThemeName.SolarizedDark ? "#93A1A1" : "#D4D4D4"
        : Name == ThemeName.SolarizedLight ? "#657B83" : "#333333";

    public string PreviewKeyword => Name switch
    {
        ThemeName.Dracula => "#FF79C6",
        ThemeName.Monokai or ThemeName.DimmedMonokai => "#F92672",
        ThemeName.OneDark or ThemeName.AtomOneDark => "#C678DD",
        ThemeName.SolarizedDark or ThemeName.SolarizedLight => "#859900",
        ThemeName.KimbieDark => "#DC3958",
        ThemeName.Red => "#F44747",
        _ => IsDark ? "#569CD6" : "#0000FF"
    };

    public string PreviewString => Name switch
    {
        ThemeName.Dracula => "#F1FA8C",
        ThemeName.Monokai or ThemeName.DimmedMonokai => "#E6DB74",
        ThemeName.SolarizedDark or ThemeName.SolarizedLight => "#2AA198",
        ThemeName.OneDark or ThemeName.AtomOneDark => "#98C379",
        _ => IsDark ? "#CE9178" : "#A31515"
    };

    public string PreviewAccent => Name switch
    {
        ThemeName.Dracula => "#BD93F9",
        ThemeName.Monokai or ThemeName.DimmedMonokai => "#A6E22E",
        ThemeName.SolarizedDark or ThemeName.SolarizedLight => "#B58900",
        ThemeName.TomorrowNightBlue => "#81A2BE",
        ThemeName.Red => "#F44747",
        ThemeName.OneDark or ThemeName.AtomOneDark => "#61AFEF",
        _ => IsDark ? "#4FC1FF" : "#2675BF"
    };

    public override string ToString() => DisplayName;
}

public sealed record AccentColorOption(string Name, string Hex);

public sealed class ThemeService
{
    public const string DefaultAccentColor = "#5BD08E";

    public static readonly IReadOnlyList<AccentColorOption> DefaultAccentPalette =
    [
        new("Green", "#5BD08E"),
        new("Mint", "#71DDB8"),
        new("Lime", "#A4CC46"),
        new("Teal", "#19A89A"),
        new("Turquoise", "#28C5B7"),
        new("Cyan", "#42D8D0"),
        new("Sky blue", "#49C3E8"),
        new("Blue", "#4C8DFF"),
        new("Royal blue", "#4263EB"),
        new("Indigo", "#5965D8"),
        new("Violet", "#8256D0"),
        new("Purple", "#A879FF"),
        new("Lavender", "#BD8CFF"),
        new("Magenta", "#D64DBA"),
        new("Pink", "#F16CBA"),
        new("Rose", "#EF6685"),
        new("Red", "#E95A5A"),
        new("Coral", "#F17464"),
        new("Orange", "#F09352"),
        new("Tangerine", "#ED7D31"),
        new("Amber", "#E4BA4E"),
        new("Gold", "#D9A441"),
        new("Yellow", "#D6C94A"),
        new("Slate", "#8793A1")
    ];

    private static readonly HashSet<ThemeName> DarkThemes =
    [
        ThemeName.Abbys,
        ThemeName.Dark,
        ThemeName.DarkPlus,
        ThemeName.DimmedMonokai,
        ThemeName.KimbieDark,
        ThemeName.Monokai,
        ThemeName.OneDark,
        ThemeName.SolarizedDark,
        ThemeName.TomorrowNightBlue,
        ThemeName.Dracula,
        ThemeName.AtomOneDark,
        ThemeName.VisualStudioDark,
        ThemeName.HighContrastDark,
        ThemeName.Red
    ];

    public ThemeService()
    {
        RegistryOptions = new RegistryOptions(ThemeName.DarkPlus);

        Themes = Enum.GetValues<ThemeName>()
            .Select(name => new ThemeInfo(name, GetDisplayName(name), DarkThemes.Contains(name)))
            .OrderBy(theme => theme.IsDark ? 1 : 0)
            .ThenBy(theme => theme.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Current = Themes.First(theme => theme.Name == ThemeName.DarkPlus);
    }

    public RegistryOptions RegistryOptions { get; }

    public IReadOnlyList<ThemeInfo> Themes { get; }

    public IReadOnlyList<AccentColorOption> AccentPalette => DefaultAccentPalette;

    public ThemeInfo Current { get; private set; }

    public string AccentColor { get; private set; } = DefaultAccentColor;

    public event EventHandler? ThemeChanged;

    public ThemeInfo Find(string? name) =>
        Themes.FirstOrDefault(theme => string.Equals(theme.Name.ToString(), name, StringComparison.OrdinalIgnoreCase))
        ?? Themes.First(theme => theme.Name == ThemeName.DarkPlus);

    public void Apply(ThemeInfo theme)
    {
        Current = theme;

        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = theme.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyAccentColor(string? accentColor)
    {
        AccentColor = NormalizeAccentColor(accentColor);
        if (!Color.TryParse(AccentColor, out var accent) || Application.Current is not { } application)
        {
            return;
        }

        foreach (var dictionary in EnumerateDictionaries(application.Resources))
        {
            if (!dictionary.ThemeDictionaries.TryGetValue(ThemeVariant.Dark, out var darkTheme) || darkTheme is not ResourceDictionary dark ||
                !dictionary.ThemeDictionaries.TryGetValue(ThemeVariant.Light, out var lightTheme) || lightTheme is not ResourceDictionary light)
            {
                continue;
            }

            ApplyAccentToPalette(dark, accent, isDark: true);
            ApplyAccentToPalette(light, accent, isDark: false);
        }
    }

    public static bool TryNormalizeAccentColor(string? value, out string normalized)
    {
        normalized = DefaultAccentColor;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        if (candidate.Length != 7 || candidate[0] != '#' || !Color.TryParse(candidate, out var color))
        {
            return false;
        }

        normalized = ToHex(color);
        return true;
    }

    public static string NormalizeAccentColor(string? value) =>
        TryNormalizeAccentColor(value, out var normalized) ? normalized : DefaultAccentColor;

    public IRawTheme? LoadTheme(ThemeInfo theme)
    {
        try
        {
            return RegistryOptions.LoadTheme(theme.Name);
        }
        catch
        {
            return null;
        }
    }

    private static string GetDisplayName(ThemeName name) => name switch
    {
        ThemeName.DarkPlus => "Dark+ (default dark)",
        ThemeName.LightPlus => "Light+ (default light)",
        ThemeName.AtomOneDark => "Atom One Dark",
        ThemeName.AtomOneLight => "Atom One Light",
        ThemeName.DimmedMonokai => "Dimmed Monokai",
        ThemeName.KimbieDark => "Kimbie Dark",
        ThemeName.SolarizedDark => "Solarized Dark",
        ThemeName.SolarizedLight => "Solarized Light",
        ThemeName.TomorrowNightBlue => "Tomorrow Night Blue",
        ThemeName.VisualStudioDark => "Visual Studio Dark",
        ThemeName.VisualStudioLight => "Visual Studio Light",
        ThemeName.HighContrastDark => "High Contrast Dark",
        ThemeName.HighContrastLight => "High Contrast Light",
        ThemeName.QuietLight => "Quiet Light",
        ThemeName.OneDark => "One Dark",
        _ => name.ToString()
    };

    private static IEnumerable<IResourceDictionary> EnumerateDictionaries(IResourceDictionary root)
    {
        yield return root;
        foreach (var merged in root.MergedDictionaries)
        {
            if (merged is not IResourceDictionary child)
            {
                continue;
            }

            foreach (var nested in EnumerateDictionaries(child))
            {
                yield return nested;
            }
        }
    }

    private static void ApplyAccentToPalette(IResourceDictionary resources, Color accent, bool isDark)
    {
        var foreground = IsLight(accent) ? Color.Parse("#101318") : Colors.White;
        var hover = Mix(accent, isDark ? Colors.White : Colors.Black, 0.16);
        var baseSurface = Color.Parse(isDark ? "#17191E" : "#FFFFFF");
        var chrome = Color.Parse(isDark ? "#1A1824" : "#F4F6F8");

        resources["SystemAccentColor"] = accent;
        resources["SystemAccentColorLight1"] = Mix(accent, Colors.White, 0.14);
        resources["SystemAccentColorLight2"] = Mix(accent, Colors.White, 0.28);
        resources["SystemAccentColorLight3"] = Mix(accent, Colors.White, 0.42);
        resources["SystemAccentColorDark1"] = Mix(accent, Colors.Black, 0.14);
        resources["SystemAccentColorDark2"] = Mix(accent, Colors.Black, 0.28);
        resources["SystemAccentColorDark3"] = Mix(accent, Colors.Black, 0.42);
        resources["AccentBrush"] = new SolidColorBrush(accent);
        resources["AccentForegroundBrush"] = new SolidColorBrush(foreground);
        resources["AccentHoverBrush"] = new SolidColorBrush(hover);
        resources["AccentSoftBrush"] = new SolidColorBrush(Mix(baseSurface, accent, isDark ? 0.18 : 0.12));
        resources["SelectedBrush"] = new SolidColorBrush(Mix(Color.Parse(isDark ? "#1D2026" : "#FFFFFF"), accent, isDark ? 0.2 : 0.14));
        resources["EditorSelectionBrush"] = new SolidColorBrush(Mix(baseSurface, accent, isDark ? 0.3 : 0.24));
        resources["AccentGradientBrush"] = new LinearGradientBrush
        {
            StartPoint = RelativePoint.TopLeft,
            EndPoint = RelativePoint.BottomRight,
            GradientStops =
            [
                new GradientStop(Mix(accent, Colors.Black, isDark ? 0.28 : 0.2), 0),
                new GradientStop(Mix(accent, Colors.White, isDark ? 0.15 : 0.08), 1)
            ]
        };
        resources["WelcomeGlowBrush"] = new RadialGradientBrush
        {
            Center = RelativePoint.Center,
            GradientOrigin = RelativePoint.Center,
            RadiusX = new RelativeScalar(0.5, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.5, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.FromArgb(0x33, accent.R, accent.G, accent.B), 0),
                new GradientStop(Color.FromArgb(0, accent.R, accent.G, accent.B), 1)
            ]
        };
        resources["WorkspaceBarBrush"] = new LinearGradientBrush
        {
            StartPoint = RelativePoint.TopLeft,
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Mix(Color.Parse(isDark ? "#1B1829" : "#FFFFFF"), accent, 0.08), 0),
                new GradientStop(Mix(Color.Parse(isDark ? "#171A23" : "#F7F9FC"), accent, 0.04), 0.45),
                new GradientStop(Mix(Color.Parse(isDark ? "#121419" : "#F1F4F8"), accent, 0.02), 1)
            ]
        };
    }

    private static Color Mix(Color first, Color second, double amount) => Color.FromRgb(
        (byte)Math.Round(first.R + (second.R - first.R) * amount),
        (byte)Math.Round(first.G + (second.G - first.G) * amount),
        (byte)Math.Round(first.B + (second.B - first.B) * amount));

    private static bool IsLight(Color color) =>
        (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255 > 0.58;

    private static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
