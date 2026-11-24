using Avalonia.Media;
using AvaloniaEdit;
using CommunityToolkit.Mvvm.ComponentModel;
using NotepadMoreOS.Models;

namespace NotepadMoreOS.ViewModels;

public partial class EditorSettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private bool _suspendNotifications;
    private FontFamily _editorFontFamily;

    public EditorSettingsViewModel(AppSettings settings)
    {
        _settings = settings;

        EditorOptions = new TextEditorOptions
        {
            AllowToggleOverstrikeMode = true,
            EnableTextDragDrop = true,
            ShowBoxForControlCharacters = true,
            EnableHyperlinks = false,
            EnableEmailHyperlinks = false,
            EnableRectangularSelection = true,
            EnableVirtualSpace = false,
            CutCopyWholeLine = true,
            AllowScrollBelowDocument = true
        };

        _fontFamily = settings.FontFamily;
        _editorFontFamily = ParseFontFamily(settings.FontFamily);
        _fontSize = settings.FontSize;
        _wordWrap = settings.WordWrap;
        _showLineNumbers = settings.ShowLineNumbers;
        _showWhitespace = settings.ShowWhitespace;
        _showEndOfLine = settings.ShowEndOfLine;
        _highlightCurrentLine = settings.HighlightCurrentLine;
        _highlightMatchingBrackets = settings.HighlightMatchingBrackets;
        _tabSize = settings.TabSize;
        _convertTabsToSpaces = settings.ConvertTabsToSpaces;
        _enableColumnRuler = settings.EnableColumnRuler;

        ApplyToOptions();
    }

    public TextEditorOptions EditorOptions { get; }

    public FontFamily EditorFontFamily => _editorFontFamily;

    public event EventHandler? Changed;

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

    public void ApplyFrom(AppSettings settings)
    {
        _suspendNotifications = true;
        try
        {
            FontFamily = settings.FontFamily;
            FontSize = settings.FontSize;
            WordWrap = settings.WordWrap;
            ShowLineNumbers = settings.ShowLineNumbers;
            ShowWhitespace = settings.ShowWhitespace;
            ShowEndOfLine = settings.ShowEndOfLine;
            HighlightCurrentLine = settings.HighlightCurrentLine;
            HighlightMatchingBrackets = settings.HighlightMatchingBrackets;
            TabSize = settings.TabSize;
            ConvertTabsToSpaces = settings.ConvertTabsToSpaces;
            EnableColumnRuler = settings.EnableColumnRuler;
        }
        finally
        {
            _suspendNotifications = false;
        }

        ApplyToOptions();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    partial void OnFontFamilyChanged(string value)
    {
        _editorFontFamily = ParseFontFamily(value);
        OnPropertyChanged(nameof(EditorFontFamily));
        Persist(s => s.FontFamily = value);
    }

    partial void OnFontSizeChanged(double value) => Persist(s => s.FontSize = Math.Clamp(value, 6, 72));
    partial void OnWordWrapChanged(bool value) => Persist(s => s.WordWrap = value);
    partial void OnShowLineNumbersChanged(bool value) => Persist(s => s.ShowLineNumbers = value);
    partial void OnShowWhitespaceChanged(bool value) => Persist(s => s.ShowWhitespace = value);
    partial void OnShowEndOfLineChanged(bool value) => Persist(s => s.ShowEndOfLine = value);
    partial void OnHighlightCurrentLineChanged(bool value) => Persist(s => s.HighlightCurrentLine = value);
    partial void OnHighlightMatchingBracketsChanged(bool value) => Persist(s => s.HighlightMatchingBrackets = value);
    partial void OnTabSizeChanged(int value) => Persist(s => s.TabSize = Math.Clamp(value, 1, 16));
    partial void OnConvertTabsToSpacesChanged(bool value) => Persist(s => s.ConvertTabsToSpaces = value);
    partial void OnEnableColumnRulerChanged(bool value) => Persist(s => s.EnableColumnRuler = value);

    private void Persist(Action<AppSettings> update)
    {
        ApplyToOptions();
        if (_suspendNotifications)
        {
            return;
        }

        update(_settings);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyToOptions()
    {
        EditorOptions.ShowSpaces = ShowWhitespace;
        EditorOptions.ShowTabs = ShowWhitespace;
        EditorOptions.ShowEndOfLine = ShowEndOfLine;
        EditorOptions.HighlightCurrentLine = HighlightCurrentLine;
        EditorOptions.IndentationSize = TabSize;
        EditorOptions.ConvertTabsToSpaces = ConvertTabsToSpaces;
        EditorOptions.ShowColumnRulers = EnableColumnRuler;
        EditorOptions.ColumnRulerPositions = EnableColumnRuler ? new List<int> { 80, 100, 120 } : null;
    }

    private static FontFamily ParseFontFamily(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            try
            {
                return new FontFamily(value);
            }
            catch (ArgumentException)
            {
                // Fall through to the default.
            }
        }

        return new FontFamily("Menlo, Cascadia Code, Consolas, monospace");
    }
}
