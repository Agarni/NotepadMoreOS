using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media;
using NotepadMoreOS.Models;
using NotepadMoreOS.Services;
using System.Windows.Input;

namespace NotepadMoreOS.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    public SettingsViewModel(AppSettings settings, ThemeService themeService)
    {
        Themes = themeService.Themes;
        AccentPalette = themeService.AccentPalette
            .Select(option => new AccentColorPreset(option.Name, option.Hex, SetAccentPresetCommand))
            .ToList();
        _theme = themeService.Find(settings.Theme);
        _accentColor = ThemeService.NormalizeAccentColor(settings.AccentColor);
        _tabStyle = Models.TabStyles.All.FirstOrDefault(style => style.Id == settings.TabStyle) ?? Models.TabStyles.All[0];
        _language = LocalizationService.GetLanguages().FirstOrDefault(option => option.Id == LocalizationService.Normalize(settings.Language))
                    ?? LocalizationService.GetLanguages()[0];
        UpdatePaletteSelection(_accentColor);
        _fontFamily = settings.FontFamily;
        _fontSize = Math.Clamp(settings.FontSize, 6, 72);
        _wordWrap = settings.WordWrap;
        _showLineNumbers = settings.ShowLineNumbers;
        _showWhitespace = settings.ShowWhitespace;
        _showEndOfLine = settings.ShowEndOfLine;
        _highlightCurrentLine = settings.HighlightCurrentLine;
        _highlightMatchingBrackets = settings.HighlightMatchingBrackets;
        _tabSize = Math.Clamp(settings.TabSize, 1, 16);
        _convertTabsToSpaces = settings.ConvertTabsToSpaces;
        _enableColumnRuler = settings.EnableColumnRuler;
        _restoreSession = settings.RestoreSession;
        _recoveryEnabled = settings.RecoveryEnabled;
    }

    public IReadOnlyList<ThemeInfo> Themes { get; }
    public IReadOnlyList<TabStyleInfo> TabStyles { get; } = Models.TabStyles.All;
    public IReadOnlyList<LanguageOption> Languages { get; } = LocalizationService.GetLanguages();
    public IReadOnlyList<AccentColorPreset> AccentPalette { get; }

    public event EventHandler? PreviewChanged;

    [ObservableProperty] private ThemeInfo _theme;
    [ObservableProperty] private TabStyleInfo _tabStyle;
    [ObservableProperty] private LanguageOption _language;
    [ObservableProperty] private string _accentColor;
    [ObservableProperty] private string _fontFamily;
    [ObservableProperty] private double _fontSize;
    [ObservableProperty] private bool _wordWrap;
    [ObservableProperty] private bool _showLineNumbers;
    [ObservableProperty] private bool _showWhitespace;
    [ObservableProperty] private bool _showEndOfLine;
    [ObservableProperty] private bool _highlightCurrentLine;
    [ObservableProperty] private bool _highlightMatchingBrackets;
    [ObservableProperty] private int _tabSize;
    [ObservableProperty] private bool _convertTabsToSpaces;
    [ObservableProperty] private bool _enableColumnRuler;
    [ObservableProperty] private bool _restoreSession;
    [ObservableProperty] private bool _recoveryEnabled;

    public bool IsAccentColorValid => ThemeService.TryNormalizeAccentColor(AccentColor, out _);
    public bool IsAccentColorInvalid => !IsAccentColorValid;

    public IBrush AccentPreviewBrush => Color.TryParse(AccentColor, out var color)
        ? new SolidColorBrush(color)
        : Brushes.Transparent;

    [RelayCommand]
    private void SetAccentPreset(string? color)
    {
        if (ThemeService.TryNormalizeAccentColor(color, out var normalized))
        {
            AccentColor = normalized;
        }
    }

    [RelayCommand]
    private void ResetAccentColor() => AccentColor = ThemeService.DefaultAccentColor;

    partial void OnThemeChanged(ThemeInfo value) => PreviewChanged?.Invoke(this, EventArgs.Empty);

    partial void OnTabStyleChanged(TabStyleInfo value) => PreviewChanged?.Invoke(this, EventArgs.Empty);

    partial void OnAccentColorChanged(string value)
    {
        OnPropertyChanged(nameof(IsAccentColorValid));
        OnPropertyChanged(nameof(IsAccentColorInvalid));
        OnPropertyChanged(nameof(AccentPreviewBrush));
        if (ThemeService.TryNormalizeAccentColor(value, out var normalized))
        {
            if (!string.Equals(value, normalized, StringComparison.Ordinal))
            {
                AccentColor = normalized;
                return;
            }

            UpdatePaletteSelection(normalized);
            PreviewChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdatePaletteSelection(string accentColor)
    {
        foreach (var preset in AccentPalette)
        {
            preset.IsSelected = string.Equals(preset.Hex, accentColor, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void ApplyTo(AppSettings settings)
    {
        settings.Theme = Theme.Name.ToString();
        settings.AccentColor = ThemeService.NormalizeAccentColor(AccentColor);
        settings.TabStyle = TabStyle.Id;
        settings.Language = Language.Id;
        settings.FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? "Menlo, Consolas, monospace" : FontFamily;
        settings.FontSize = Math.Clamp(FontSize, 6, 72);
        settings.WordWrap = WordWrap;
        settings.ShowLineNumbers = ShowLineNumbers;
        settings.ShowWhitespace = ShowWhitespace;
        settings.ShowEndOfLine = ShowEndOfLine;
        settings.HighlightCurrentLine = HighlightCurrentLine;
        settings.HighlightMatchingBrackets = HighlightMatchingBrackets;
        settings.TabSize = Math.Clamp(TabSize, 1, 16);
        settings.ConvertTabsToSpaces = ConvertTabsToSpaces;
        settings.EnableColumnRuler = EnableColumnRuler;
        settings.RestoreSession = RestoreSession;
        settings.RecoveryEnabled = RecoveryEnabled;
    }
}

public sealed partial class AccentColorPreset : ObservableObject
{
    public AccentColorPreset(string name, string hex, ICommand selectCommand)
    {
        Name = name;
        Hex = hex;
        SelectCommand = selectCommand;
    }

    public string Name { get; }

    public string Hex { get; }

    public ICommand SelectCommand { get; }

    public IBrush Brush => new SolidColorBrush(Color.Parse(Hex));

    public string ToolTipText => $"{Name} — {Hex}";

    [ObservableProperty] private bool _isSelected;
}
