using AvaloniaEdit.Document;
using NotepadMoreOS.Models;
using NotepadMoreOS.Services;
using NotepadMoreOS.ViewModels;

namespace NotepadMoreOS.Tests;

internal sealed class TestEditorHost(TextDocument document) : IEditorHost
{
    public TextDocument Document { get; } = document;
    public int CaretOffset { get; set; }
    public int SelectionStart { get; private set; }
    public int SelectionLength { get; private set; }
    public int LastScrollLine { get; private set; }
    public bool CanUndo => Document.UndoStack.CanUndo;
    public bool CanRedo => Document.UndoStack.CanRedo;

    public void Select(int start, int length)
    {
        SelectionStart = start;
        SelectionLength = length;
        CaretOffset = start;
    }

    public void ScrollToLine(int line) => LastScrollLine = line;
    public void Focus() { }
    public void Undo() => Document.UndoStack.Undo();
    public void Redo() => Document.UndoStack.Redo();
    public void Cut() { }
    public void Copy() { }
    public void Paste() { }
    public void Delete() { }
    public void SelectAll() => Select(0, Document.TextLength);
    public void DuplicateLine() { }
    public void DeleteLine() { }
    public void ToggleComment(string prefix) { }
}


internal static class TestEnvironment
{
    public static readonly FileService FileService = new();
    public static readonly LanguageService LanguageService = new();
    public static readonly ThemeService ThemeService = new();
    public static readonly AppSettings Settings = new();
    public static readonly EditorSettingsViewModel EditorSettings = new(Settings);

    public static DocumentViewModel CreateDocument(int number = 1) =>
        new(FileService, LanguageService, ThemeService, EditorSettings, number);

    public static MainWindowViewModel CreateViewModel(
        SettingsService settingsService,
        AppSettings settings,
        SessionService? sessionService = null,
        FileWatchService? fileWatcher = null)
    {
        var settingsDirectory = Path.GetDirectoryName(settingsService.SettingsFile) ?? Path.GetTempPath();

        return new MainWindowViewModel(
            settingsService,
            FileService,
            LanguageService,
            ThemeService,
            new SearchService(),
            new DialogService(),
            sessionService ?? new SessionService(Path.Combine(settingsDirectory, "recovery")),
            fileWatcher ?? new FileWatchService(),
            settings);
    }

    public static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "notepadmoreos-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
