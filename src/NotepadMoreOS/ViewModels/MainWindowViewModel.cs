using NotepadMoreOS.Resources;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotepadMoreOS.Models;
using NotepadMoreOS.Services;

namespace NotepadMoreOS.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly FileService _fileService;
    private readonly LanguageService _languageService;
    private readonly SearchService _searchService;
    private readonly DialogService _dialogs;
    private readonly SessionService _sessionService;
    private readonly FileWatchService _fileWatcher;
    private readonly string _sessionDraftsDirectory;
    private readonly HashSet<Guid> _declinedExternalChanges = [];
    private readonly HashSet<Guid> _externalChangeInProgress = [];
    private static readonly IBrush FallbackLanguageAccent = new SolidColorBrush(Color.Parse("#8A93A0"));
    private static readonly IBrush FallbackLanguageSoft = new SolidColorBrush(Color.FromArgb(0x28, 0x5B, 0xD0, 0x8E));

    private int _untitledCounter;
    private string? _tabStylePreview;
    private DispatcherTimer? _statusTimer;
    private DispatcherTimer? _settingsTimer;
    private DispatcherTimer? _recoveryTimer;
    private DispatcherTimer? _recoveryDebounce;
    private bool _recoverySaveInProgress;
    private bool _recoverySuppressed;
    private bool _disposed;

    public MainWindowViewModel(
        SettingsService settingsService,
        FileService fileService,
        LanguageService languageService,
        ThemeService themeService,
        SearchService searchService,
        DialogService dialogs,
        SessionService sessionService,
        FileWatchService fileWatcher,
        AppSettings settings)
    {
        _settingsService = settingsService;
        _fileService = fileService;
        _languageService = languageService;
        _searchService = searchService;
        _dialogs = dialogs;
        _sessionService = sessionService;
        _fileWatcher = fileWatcher;
        _fileWatcher.FileChanged += OnFileChangedOnDisk;

        var settingsDirectory = Path.GetDirectoryName(settingsService.SettingsFile);
        _sessionDraftsDirectory = Path.Combine(
            string.IsNullOrEmpty(settingsDirectory) ? AppPaths.ConfigDirectory : settingsDirectory,
            "session-drafts");

        Settings = settings;
        ThemeService = themeService;

        EditorSettings = new EditorSettingsViewModel(settings);
        EditorSettings.Changed += OnEditorSettingsChanged;

        FindReplace = new FindReplaceViewModel(searchService);
        FindReplace.CloseRequested += OnFindCloseRequested;
        FindReplace.FocusRequested += OnFindFocusRequested;

        RebuildRecentFiles();
        RebuildSessions();
    }

    public AppSettings Settings { get; }

    public ThemeService ThemeService { get; }

    public EditorSettingsViewModel EditorSettings { get; }

    public FindReplaceViewModel FindReplace { get; }

    public ObservableCollection<DocumentViewModel> Documents { get; } = [];

    public ObservableCollection<RecentFileViewModel> RecentFiles { get; } = [];

    public ObservableCollection<SessionItemViewModel> Sessions { get; } = [];

    public IReadOnlyList<EncodingOption> EncodingOptions => EncodingCatalog.All;

    public IReadOnlyList<LineEndingOption> LineEndingOptions => LineEndingCatalog.All;

    public IReadOnlyList<LanguageInfo> Languages => _languageService.Languages;

    public IReadOnlyList<ThemeInfo> Themes => ThemeService.Themes;

    public IReadOnlyList<AccentColorOption> AccentPalette => ThemeService.AccentPalette;

    public string EffectiveTabStyle => _tabStylePreview ?? Settings.TabStyle;

    public bool TabsRounded => EffectiveTabStyle == TabStyles.Rounded;

    public bool TabsFloating => EffectiveTabStyle == TabStyles.Floating;

    public bool TabsConnected => EffectiveTabStyle == TabStyles.Connected;

    /// <summary>Applies a tab style while the settings dialog is open; null returns to the saved value.</summary>
    public void PreviewTabStyle(string? styleId)
    {
        _tabStylePreview = styleId;
        OnPropertyChanged(nameof(EffectiveTabStyle));
        OnPropertyChanged(nameof(TabsRounded));
        OnPropertyChanged(nameof(TabsFloating));
        OnPropertyChanged(nameof(TabsConnected));
    }

    public bool HasDocuments => Documents.Count > 0;

    public bool IsWelcomeVisible => Documents.Count == 0;

    public bool HasRecentFiles => RecentFiles.Count > 0;

    public bool HasNoRecentFiles => RecentFiles.Count == 0;

    public string CurrentSessionDisplay => string.IsNullOrWhiteSpace(CurrentSessionName)
        ? Strings.Session_NoSession
        : CurrentSessionName;

    public bool CanManageCurrentSession => !string.IsNullOrWhiteSpace(CurrentSessionName);

    public bool HasSessions => Sessions.Count > 0;

    public bool HasNoSessions => Sessions.Count == 0;

    public string ActiveFolderName
    {
        get
        {
            if (ActiveDocument?.FilePath is not { } path)
            {
                return Strings.Session_Label;
            }

            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
            {
                return Strings.Session_Label;
            }

            var name = Path.GetFileName(directory);
            return string.IsNullOrEmpty(name) ? directory : name;
        }
    }

    public string ActiveFolderPath => ActiveDocument?.FilePath is { } path
        ? Path.GetDirectoryName(path) ?? path
        : Strings.Session_Unsaved;

    public string ActiveLanguageName => ActiveDocument?.Language.Name ?? Strings.Language_PlainText;

    public IBrush ActiveLanguageAccentBrush => ActiveDocument?.LanguageAccentBrush ?? FallbackLanguageAccent;

    public IBrush ActiveLanguageSoftBrush => ActiveDocument?.LanguageSoftBrush ?? FallbackLanguageSoft;

    public int StatusCaretLine => ActiveDocument?.CaretLine ?? 1;

    public int StatusCaretColumn => ActiveDocument?.CaretColumn ?? 1;

    public int StatusSelectionLength => ActiveDocument?.SelectionLength ?? 0;

    public int StatusLineCount => ActiveDocument?.LineCount ?? 1;

    public string StatusLinesText => string.Format(Strings.Status_Lines, StatusLineCount);

    public int StatusLength => ActiveDocument?.Length ?? 0;

    public string StatusLengthText => string.Format(Strings.Status_Length, StatusLength);

    public string StatusZoom => ActiveDocument?.ZoomPercent ?? "100%";

    public EncodingOption? StatusEncoding
    {
        get => ActiveDocument?.Encoding;
        set
        {
            if (ActiveDocument is not null && value is not null)
            {
                ActiveDocument.Encoding = value;
            }
        }
    }

    public LineEndingOption? StatusLineEnding
    {
        get => ActiveDocument?.LineEnding;
        set
        {
            if (ActiveDocument is not null && value is not null)
            {
                ActiveDocument.LineEnding = value;
            }
        }
    }

    public LanguageInfo? StatusLanguage
    {
        get => ActiveDocument?.Language;
        set
        {
            if (ActiveDocument is not null && value is not null)
            {
                ActiveDocument.Language = value;
            }
        }
    }

    public string WindowTitle => ActiveDocument is { } document
        ? $"{document.Title}{(document.IsDirty ? " *" : string.Empty)} - NotepadMoreOS"
        : "NotepadMoreOS";

    public event EventHandler? FindFocusRequested;
    public event EventHandler? ExitRequested;

    [ObservableProperty] private DocumentViewModel? _activeDocument;
    [ObservableProperty] private bool _isFindBarOpen;
    [ObservableProperty] private string? _currentSessionName;
    [ObservableProperty] private string _statusMessage = Strings.Status_Ready;

    public async Task InitializeAsync(IReadOnlyList<string> startupFiles)
    {
        // A recovery snapshot means the previous run did not shut down cleanly.
        // It takes precedence over the normal session file.
        var recovered = await TryRestoreRecoveryAsync();

        if (startupFiles.Count > 0)
        {
            await OpenPathsAsync(startupFiles);
        }
        else if (!recovered && Settings.RestoreSession && Settings.Session.Documents.Count > 0)
        {
            await RestoreSessionAsync();
        }

        StartRecoveryTimer();

        if (ActiveDocument is not null)
        {
            FindReplace.SetTarget(ActiveDocument);
        }
    }

    public async Task<bool> RequestCloseApplicationAsync()
    {
        if (IsCloseApproved)
        {
            return true;
        }

        // Notepad++-style persistence: keep unsaved drafts without prompting and
        // without writing to the original files. The recovery snapshot becomes
        // the source of truth for the next startup.
        if (Settings.RecoveryEnabled && Documents.Count > 0)
        {
            var saved = await SaveRecoverySnapshotAsync();
            if (saved)
            {
                SaveSession();
                SaveSettingsNow();
                StopRecoveryTimers();
                IsCloseApproved = true;
                return true;
            }
        }

        foreach (var document in Documents.ToList())
        {
            if (document.IsDirty)
            {
                var choice = await _dialogs.AskSaveChangesAsync(document.Title);
                if (choice == SaveChangesChoice.Cancel)
                {
                    return false;
                }

                if (choice == SaveChangesChoice.Save && !await SaveDocumentAsync(document, saveAs: false))
                {
                    return false;
                }
            }
        }

        SaveSession();
        SaveSettingsNow();

        // Prompt-based close: recovery data must not resurrect discarded documents.
        StopRecoveryTimers();
        await _sessionService.ClearAsync();
        IsCloseApproved = true;
        return true;
    }

    public bool IsCloseApproved { get; private set; }

    public async Task OpenPathsAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(path);
            var existing = Documents.FirstOrDefault(document =>
                document.FilePath is not null && PathsEqual(document.FilePath, fullPath));

            if (existing is not null)
            {
                AddRecentFile(fullPath);
                ActiveDocument = existing;
                continue;
            }

            var document = CreateDocument();
            try
            {
                await document.LoadAsync(fullPath);
                AddDocument(document);
                AddRecentFile(fullPath);
                ShowStatus(string.Format(Strings.Status_Opened, Path.GetFileName(fullPath)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                document.Dispose();
                await _dialogs.ShowErrorAsync("Could not open file", $"{fullPath}\n\n{ex.Message}");
            }
        }
    }

    [RelayCommand]
    private void NewDocument()
    {
        AddDocument(CreateDocument());
        ShowStatus(Strings.Status_NewDocument);
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        var paths = await _dialogs.PickOpenFilesAsync();
        await OpenPathsAsync(paths);
    }

    [RelayCommand]
    private async Task OpenRecentAsync(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            await OpenPathsAsync([path]);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await SaveDocumentAsync(ActiveDocument, saveAs: false);
    }

    [RelayCommand]
    private async Task SaveAsAsync()
    {
        await SaveDocumentAsync(ActiveDocument, saveAs: true);
    }

    [RelayCommand]
    private async Task SaveAllAsync()
    {
        var dirtyDocuments = Documents.Where(document => document.IsDirty).ToList();
        if (dirtyDocuments.Count == 0)
        {
            ShowStatus(Strings.Status_NoChangesToSave);
            return;
        }

        foreach (var document in dirtyDocuments)
        {
            if (!await SaveDocumentAsync(document, saveAs: false))
            {
                return;
            }
        }

        ShowStatus(dirtyDocuments.Count == 1
            ? Strings.Status_SavedOne
            : string.Format(Strings.Status_SavedMany, dirtyDocuments.Count));
    }

    [RelayCommand]
    private async Task CloseTabAsync(DocumentViewModel? document)
    {
        await CloseDocumentAsync(document ?? ActiveDocument);
    }

    [RelayCommand]
    private async Task CloseAllAsync()
    {
        foreach (var document in Documents.ToList())
        {
            if (!await ConfirmCloseDocumentAsync(document))
            {
                return;
            }

            RemoveDocument(document);
        }
    }

    [RelayCommand]
    private void SelectDocument(DocumentViewModel? document)
    {
        if (document is not null && Documents.Contains(document))
        {
            ActiveDocument = document;
        }
    }

    [RelayCommand]
    private void NextDocument() => CycleDocument(forward: true);

    [RelayCommand]
    private void PreviousDocument() => CycleDocument(forward: false);

    [RelayCommand]
    private Task ReloadAsync() => ReloadDocumentAsync(ActiveDocument);

    private async Task ReloadDocumentAsync(DocumentViewModel? document)
    {
        if (document?.FilePath is null)
        {
            return;
        }

        if (document.IsDirty &&
            !await _dialogs.ConfirmAsync(
                Strings.Dialog_ReloadTitle,
                string.Format(Strings.Dialog_DiscardReload, document.Title),
                Strings.Tab_Reload))
        {
            return;
        }

        try
        {
            await document.ReloadAsync();
            _declinedExternalChanges.Remove(document.Id);
            ShowStatus(string.Format(Strings.Status_Reloaded, document.Title));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _dialogs.ShowErrorAsync(Strings.Status_CouldNotReload, ex.Message);
        }
    }

    [RelayCommand]
    private async Task ReopenWithEncodingAsync(EncodingOption? encoding)
    {
        if (ActiveDocument is not { FilePath: not null } document || encoding is null)
        {
            return;
        }

        if (document.IsDirty &&
            !await _dialogs.ConfirmAsync(
                Strings.Dialog_ReopenTitle,
                string.Format(Strings.Dialog_DiscardReopen, document.Title, encoding.Name),
                Strings.Dialog_ReopenButton))
        {
            return;
        }

        try
        {
            await document.ReopenWithEncodingAsync(encoding);
            _declinedExternalChanges.Remove(document.Id);
            ShowStatus(string.Format(Strings.Status_Reopened, document.Title, encoding.Name));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            await _dialogs.ShowErrorAsync(Strings.Status_CouldNotReopen, ex.Message);
        }
    }

    [RelayCommand]
    private async Task ReopenWithDetectedEncodingAsync()
    {
        await ReloadAsync();
    }

    [RelayCommand]
    private void SaveWithEncoding(EncodingOption? encoding)
    {
        if (ActiveDocument is null || encoding is null)
        {
            return;
        }

        ActiveDocument.Encoding = encoding;
        ShowStatus(string.Format(Strings.Status_SavedAsEncoding, encoding.Name));
    }

    [RelayCommand]
    private Task CopyFilePathAsync() => CopyFilePathAsync(ActiveDocument);

    private async Task CopyFilePathAsync(DocumentViewModel? document)
    {
        if (document?.FilePath is { } path)
        {
            await _dialogs.SetClipboardTextAsync(path);
            ShowStatus(Strings.Status_FilePathCopied);
        }
    }

    [RelayCommand]
    private Task OpenContainingFolderAsync() => OpenContainingFolderAsync(ActiveDocument);

    private async Task OpenContainingFolderAsync(DocumentViewModel? document)
    {
        if (document?.FilePath is not { } path)
        {
            return;
        }

        try
        {
            ProcessStartInfo startInfo;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                startInfo = new ProcessStartInfo("open");
                startInfo.ArgumentList.Add("-R");
                startInfo.ArgumentList.Add(path);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                startInfo = new ProcessStartInfo("explorer.exe");
                startInfo.ArgumentList.Add("/select,");
                startInfo.ArgumentList.Add(path);
            }
            else
            {
                startInfo = new ProcessStartInfo("xdg-open");
                startInfo.ArgumentList.Add(Path.GetDirectoryName(path) ?? path);
            }

            startInfo.UseShellExecute = false;
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync(Strings.Status_CouldNotOpenFolder, ex.Message);
        }
    }

    private async Task OpenTerminalAsync(DocumentViewModel document)
    {
        if (document.FilePath is not { } path)
        {
            return;
        }

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        try
        {
            ProcessStartInfo startInfo;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                startInfo = new ProcessStartInfo("open") { UseShellExecute = false };
                startInfo.ArgumentList.Add("-a");
                startInfo.ArgumentList.Add("Terminal");
                startInfo.ArgumentList.Add(directory);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                startInfo = new ProcessStartInfo("wt.exe")
                {
                    UseShellExecute = false,
                    WorkingDirectory = directory
                };
                startInfo.ArgumentList.Add("-d");
                startInfo.ArgumentList.Add(directory);
            }
            else
            {
                startInfo = new ProcessStartInfo("xdg-terminal-exec")
                {
                    UseShellExecute = false,
                    WorkingDirectory = directory
                };
            }

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync(Strings.Status_CouldNotOpenTerminal, ex.Message);
        }
    }

    private async Task RenameDocumentAsync(DocumentViewModel document)
    {
        if (document.FilePath is not { } oldPath)
        {
            await SaveDocumentAsync(document, saveAs: true);
            return;
        }

        var currentName = Path.GetFileName(oldPath);
        var newName = await _dialogs.PromptTextAsync(
            Strings.Dialog_RenameFileTitle,
            Strings.Dialog_NewFileName,
            currentName);

        if (string.IsNullOrWhiteSpace(newName) ||
            string.Equals(newName, currentName, StringComparison.Ordinal))
        {
            return;
        }

        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            newName.Contains(Path.DirectorySeparatorChar) ||
            newName.Contains(Path.AltDirectorySeparatorChar))
        {
            await _dialogs.ShowErrorAsync(Strings.Dialog_InvalidFileNameTitle, Strings.Dialog_InvalidFileNameMessage);
            return;
        }

        var directory = Path.GetDirectoryName(oldPath);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        var targetPath = Path.Combine(directory, newName);
        if (File.Exists(targetPath))
        {
            await _dialogs.ShowErrorAsync(Strings.Dialog_FileExistsTitle, string.Format(Strings.Dialog_FileExistsMessage, newName));
            return;
        }

        try
        {
            _fileWatcher.Untrack(oldPath);
            File.Move(oldPath, targetPath);
            document.SetFilePath(targetPath);
            _fileWatcher.Suppress(targetPath);
            TrackDocument(document);
            AddRecentFile(targetPath);
            UpdateStoredPaths(oldPath, targetPath);
            ScheduleSettingsSave();
            ShowStatus(string.Format(Strings.Status_RenamedTo, newName));
        }
        catch (Exception ex)
        {
            TrackDocument(document);
            await _dialogs.ShowErrorAsync(Strings.Status_CouldNotRename, ex.Message);
        }
    }

    private async Task MoveDocumentToTrashAsync(DocumentViewModel document)
    {
        if (document.FilePath is not { } path)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            Strings.Tab_MoveToTrash,
            string.Format(Strings.Dialog_TrashMessage, document.Title),
            Strings.Tab_MoveToTrash);

        if (!confirmed)
        {
            return;
        }

        try
        {
            _fileWatcher.Untrack(path);
            await _fileService.MoveToTrashAsync(path);
            RemoveDocument(document);
            ShowStatus($"{document.Title} moved to Trash");
        }
        catch (Exception ex)
        {
            TrackDocument(document);
            await _dialogs.ShowErrorAsync(Strings.Status_CouldNotTrash, ex.Message);
        }
    }

    private void UpdateStoredPaths(string oldPath, string newPath)
    {
        foreach (var session in Settings.Sessions)
        {
            foreach (var file in session.Files)
            {
                if (PathsEqual(file.Path, oldPath))
                {
                    file.Path = newPath;
                }
            }
        }

        foreach (var file in Settings.Session.Documents)
        {
            if (PathsEqual(file.Path, oldPath))
            {
                file.Path = newPath;
            }
        }
    }

    [RelayCommand]
    private void Undo() => ActiveDocument?.Host?.Undo();

    [RelayCommand]
    private void Redo() => ActiveDocument?.Host?.Redo();

    [RelayCommand]
    private void Cut() => ActiveDocument?.Host?.Cut();

    [RelayCommand]
    private void Copy() => ActiveDocument?.Host?.Copy();

    [RelayCommand]
    private void Paste() => ActiveDocument?.Host?.Paste();

    [RelayCommand]
    private void Delete() => ActiveDocument?.Host?.Delete();

    [RelayCommand]
    private void SelectAll() => ActiveDocument?.Host?.SelectAll();

    [RelayCommand]
    private void DuplicateLine() => ActiveDocument?.Host?.DuplicateLine();

    [RelayCommand]
    private void DeleteLine() => ActiveDocument?.Host?.DeleteLine();

    [RelayCommand]
    private void ToggleComment()
    {
        if (ActiveDocument is not { } document)
        {
            return;
        }

        if (document.CommentPrefix is not { } prefix)
        {
            ShowStatus(Strings.Status_CommentUnsupported);
            return;
        }

        document.Host?.ToggleComment(prefix);
    }

    [RelayCommand]
    private void InsertDateTime()
    {
        if (ActiveDocument?.Host is not { } host)
        {
            return;
        }

        host.Document.Insert(host.CaretOffset, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    [RelayCommand]
    private void OpenFind() => BeginFind(replaceMode: false);

    [RelayCommand]
    private void OpenReplace() => BeginFind(replaceMode: true);

    [RelayCommand]
    private void FindNext()
    {
        if (!IsFindBarOpen && string.IsNullOrWhiteSpace(FindReplace.SearchText))
        {
            BeginFind(replaceMode: false);
            return;
        }

        var wasOpen = IsFindBarOpen;
        FindReplace.FindNextCommand.Execute(null);

        if (!wasOpen)
        {
            ActiveDocument?.Host?.Focus();
        }
    }

    [RelayCommand]
    private void FindPrevious()
    {
        if (!IsFindBarOpen && string.IsNullOrWhiteSpace(FindReplace.SearchText))
        {
            BeginFind(replaceMode: false);
            return;
        }

        var wasOpen = IsFindBarOpen;
        FindReplace.FindPreviousCommand.Execute(null);

        if (!wasOpen)
        {
            ActiveDocument?.Host?.Focus();
        }
    }

    [RelayCommand]
    private void FindAll()
    {
        if (!IsFindBarOpen)
        {
            BeginFind(replaceMode: false);
        }

        FindReplace.FindAllCommand.Execute(null);
    }

    [RelayCommand]
    private async Task FindInFilesAsync()
    {
        var pattern = FindReplace.SearchText;
        var startFolder = ActiveDocument?.FilePath is { } path ? Path.GetDirectoryName(path) : null;

        if (ActiveDocument is { Host: { SelectionLength: > 0 and < 300 } host } document)
        {
            var selected = document.Document.GetText(host.SelectionStart, host.SelectionLength);
            if (!selected.Contains('\n') && !selected.Contains('\r'))
            {
                pattern = selected;
            }
        }

        var result = await _dialogs.ShowFindInFilesAsync(startFolder, pattern);
        if (result is not null)
        {
            await OpenFileSearchResultAsync(result);
        }
    }

    [RelayCommand]
    private void CloseFind()
    {
        IsFindBarOpen = false;
        ActiveDocument?.Host?.Focus();
    }

    [RelayCommand]
    private async Task GoToLineAsync()
    {
        if (ActiveDocument is not { } document || document.Host is not { } host)
        {
            return;
        }

        var line = await _dialogs.ShowGoToLineAsync(document.CaretLine, document.Document.LineCount);
        if (line is not { } target)
        {
            return;
        }

        target = Math.Clamp(target, 1, document.Document.LineCount);
        var documentLine = document.Document.GetLineByNumber(target);
        host.Select(documentLine.Offset, 0);
        host.ScrollToLine(target);
        host.Focus();
    }

    [RelayCommand]
    private void ToggleWordWrap()
    {
        EditorSettings.WordWrap = !EditorSettings.WordWrap;
        ShowStatus(EditorSettings.WordWrap ? Strings.Status_WordWrapOn : Strings.Status_WordWrapOff);
    }

    [RelayCommand]
    private void ZoomIn()
    {
        if (ActiveDocument is { } document)
        {
            document.Zoom = Math.Min(5.0, document.Zoom + 0.1);
            ShowStatus(document.ZoomPercent);
        }
    }

    [RelayCommand]
    private void ZoomOut()
    {
        if (ActiveDocument is { } document)
        {
            document.Zoom = Math.Max(0.5, document.Zoom - 0.1);
            ShowStatus(document.ZoomPercent);
        }
    }

    [RelayCommand]
    private void ZoomReset()
    {
        if (ActiveDocument is { } document)
        {
            document.Zoom = 1.0;
            ShowStatus("100%");
        }
    }

    [RelayCommand]
    private void SetTheme(ThemeInfo? theme)
    {
        if (theme is null)
        {
            return;
        }

        Settings.Theme = theme.Name.ToString();
        ThemeService.Apply(theme);
        ScheduleSettingsSave();
    }

    [RelayCommand]
    private void SetAccentColor(string? accentColor)
    {
        if (!ThemeService.TryNormalizeAccentColor(accentColor, out var normalized))
        {
            return;
        }

        Settings.AccentColor = normalized;
        ThemeService.ApplyAccentColor(normalized);
        ScheduleSettingsSave();
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var viewModel = new SettingsViewModel(Settings, ThemeService);
        EventHandler previewHandler = (_, _) =>
        {
            ThemeService.Apply(viewModel.Theme);
            ThemeService.ApplyAccentColor(viewModel.AccentColor);
            PreviewTabStyle(viewModel.TabStyle.Id);
        };

        viewModel.PreviewChanged += previewHandler;
        bool saveSettings;
        try
        {
            saveSettings = await _dialogs.ShowSettingsAsync(viewModel);
        }
        finally
        {
            viewModel.PreviewChanged -= previewHandler;
        }

        if (!saveSettings)
        {
            ThemeService.Apply(ThemeService.Find(Settings.Theme));
            ThemeService.ApplyAccentColor(Settings.AccentColor);
            PreviewTabStyle(null);
            return;
        }

        viewModel.ApplyTo(Settings);
        EditorSettings.ApplyFrom(Settings);
        ThemeService.Apply(ThemeService.Find(Settings.Theme));
        ThemeService.ApplyAccentColor(Settings.AccentColor);
        PreviewTabStyle(null);
        RebuildRecentFiles();
        SaveSettingsNow();

        if (Settings.RecoveryEnabled)
        {
            StartRecoveryTimer();
        }
        else
        {
            StopRecoveryTimers();
            await _sessionService.ClearAsync();
        }

        ShowStatus(Strings.Status_SettingsUpdated);
    }

    [RelayCommand]
    private async Task NewEmptySessionAsync()
    {
        var name = await _dialogs.PromptTextAsync(
            Strings.Dialog_NewSessionTitle,
            Strings.Dialog_SessionName,
            SuggestedNewSessionName());

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        name = name.Trim();
        if (SessionNameExists(name))
        {
            await _dialogs.ShowErrorAsync(
                Strings.Dialog_SessionExistsTitle,
                string.Format(Strings.Dialog_SessionExistsMessage, name));
            return;
        }

        await CreateEmptySessionAsync(name);
    }

    [RelayCommand]
    private async Task SaveCurrentSessionAsync()
    {
        if (Documents.Count == 0)
        {
            ShowStatus(Strings.Dialog_NeedTab);
            return;
        }

        var hadName = !string.IsNullOrWhiteSpace(CurrentSessionName);
        var name = CurrentSessionName;
        if (!hadName)
        {
            name = await _dialogs.PromptTextAsync(
                Strings.Dialog_SaveSessionTitle,
                Strings.Dialog_GiveSessionName,
                SuggestedSessionName());
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        await SaveSessionWithNameAsync(name, replaceExisting: hadName);
    }

    [RelayCommand]
    private async Task SaveSessionAsAsync()
    {
        if (Documents.Count == 0)
        {
            ShowStatus(Strings.Dialog_NeedTab);
            return;
        }

        var name = await _dialogs.PromptTextAsync(
            Strings.Dialog_SaveSessionAsTitle,
            Strings.Dialog_SessionName,
            SuggestedSessionName());

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        name = name.Trim();
        if (SessionNameExists(name))
        {
            await _dialogs.ShowErrorAsync(Strings.Dialog_SessionExistsTitle, string.Format(Strings.Dialog_SessionExistsMessage, name));
            return;
        }

        await SaveSessionWithNameAsync(name, replaceExisting: false);
    }

    [RelayCommand]
    private async Task RenameCurrentSessionAsync()
    {
        if (!CanManageCurrentSession)
        {
            return;
        }

        var newName = await _dialogs.PromptTextAsync(
            Strings.Dialog_RenameSessionTitle,
            Strings.Dialog_NewSessionName,
            CurrentSessionName);

        if (string.IsNullOrWhiteSpace(newName) ||
            string.Equals(newName, CurrentSessionName, StringComparison.Ordinal))
        {
            return;
        }

        await RenameSessionAsync(CurrentSessionName!, newName);
    }

    [RelayCommand]
    private async Task DeleteCurrentSessionAsync()
    {
        if (!CanManageCurrentSession)
        {
            return;
        }

        if (!await _dialogs.ConfirmAsync(
                Strings.Dialog_DeleteSessionTitle,
                string.Format(Strings.Dialog_DeleteSessionDetail, CurrentSessionName),
                Strings.Menu_Delete))
        {
            return;
        }

        DeleteSession(CurrentSessionName!);
    }

    [RelayCommand]
    private async Task OpenSessionAsync(NamedSession? session)
    {
        if (session is not null)
        {
            await OpenNamedSessionAsync(session);
        }
    }

    [RelayCommand]
    private async Task RenameSessionItemAsync(SessionItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var newName = await _dialogs.PromptTextAsync(Strings.Dialog_RenameSessionTitle, Strings.Dialog_NewSessionName, item.Name);
        if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, item.Name, StringComparison.Ordinal))
        {
            return;
        }

        await RenameSessionAsync(item.Name, newName);
    }

    [RelayCommand]
    private async Task DeleteSessionItemAsync(SessionItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (await _dialogs.ConfirmAsync(
                Strings.Dialog_DeleteSessionTitle,
                string.Format(Strings.Dialog_DeleteSessionDetail, item.Name),
                Strings.Menu_Delete))
        {
            DeleteSession(item.Name);
        }
    }

    /// <summary>
    /// Creates a named session with no files and switches the workspace to it.
    /// Public for tests; the command adds the naming prompt.
    /// </summary>
    public async Task<bool> CreateEmptySessionAsync(string name)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (SessionNameExists(name))
        {
            ShowStatus(string.Format(Strings.Status_SessionExists, name));
            return false;
        }

        if (!await CloseWorkspaceForSessionSwitchAsync())
        {
            return false;
        }

        var session = new NamedSession
        {
            Name = name,
            Files = [],
            ActiveIndex = -1,
            SavedAtUtc = DateTime.UtcNow
        };

        Settings.Sessions.Add(session);
        Settings.CurrentSessionName = name;
        CurrentSessionName = name;
        RebuildSessions();
        SaveSettingsNow();
        ShowStatus(string.Format(Strings.Status_EmptySessionCreated, name));
        return true;
    }

    private bool SessionNameExists(string name) =>
        Settings.Sessions.Any(session =>
            string.Equals(session.Name, name, StringComparison.OrdinalIgnoreCase));

    private string SuggestedWorkspaceSnapshotName()
    {
        const string baseName = "Unsaved workspace";
        if (!SessionNameExists(baseName))
        {
            return baseName;
        }

        for (var index = 2; index < 1000; index++)
        {
            var candidate = $"{baseName} {index}";
            if (!SessionNameExists(candidate))
            {
                return candidate;
            }
        }

        return $"{baseName} {DateTime.Now:HHmmss}";
    }

    private string SuggestedNewSessionName()
    {
        const string baseName = "My session";
        if (!SessionNameExists(baseName))
        {
            return baseName;
        }

        for (var index = 2; index < 1000; index++)
        {
            var candidate = $"{baseName} {index}";
            if (!SessionNameExists(candidate))
            {
                return candidate;
            }
        }

        return $"{baseName} {DateTime.Now:HHmmss}";
    }

    public async Task<bool> SaveSessionWithNameAsync(string name, bool replaceExisting)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (Documents.Count == 0)
        {
            ShowStatus(Strings.Dialog_NeedTab);
            return false;
        }

        var existing = Settings.Sessions.FirstOrDefault(session =>
            string.Equals(session.Name, name, StringComparison.OrdinalIgnoreCase));

        if (existing is not null && !replaceExisting)
        {
            ShowStatus(string.Format(Strings.Status_SessionExists, name));
            return false;
        }

        var session = existing ?? new NamedSession();
        if (string.IsNullOrWhiteSpace(session.Id))
        {
            session.Id = Guid.NewGuid().ToString("N");
        }

        var draftDirectory = GetSessionDraftDirectory(session)!;
        var keepDrafts = new HashSet<string>(StringComparer.Ordinal);
        var files = new List<NamedSessionFile>(Documents.Count);

        try
        {
            for (var index = 0; index < Documents.Count; index++)
            {
                var document = Documents[index];
                string? draftFile = null;
                string? draftPath = null;

                // Untitled tabs and dirty tabs need their in-memory text stored
                // with the session; clean files can be reopened from disk.
                if (document.IsUntitled || document.IsDirty)
                {
                    Directory.CreateDirectory(draftDirectory);
                    draftFile = $"{index}.txt";
                    draftPath = Path.Combine(draftDirectory, draftFile);
                    await File.WriteAllTextAsync(
                            draftPath,
                            document.Document.Text,
                            new UTF8Encoding(false))
                        .ConfigureAwait(true);
                    keepDrafts.Add(draftFile);
                }

                document.SetSessionDraftPath(draftPath);

                files.Add(new NamedSessionFile
                {
                    Path = document.FilePath ?? string.Empty,
                    Title = document.Title,
                    DraftFile = draftFile,
                    CaretOffset = document.Host?.CaretOffset ?? document.SavedCaretOffset,
                    IsDirty = document.IsDirty,
                    IsPinned = document.IsPinned,
                    TabColor = document.TabColorHex,
                    IsReadOnly = document.IsReadOnly,
                    Encoding = document.Encoding.Kind,
                    LineEnding = document.LineEnding.Value,
                    LanguageId = document.Language.Id
                });
            }

            PruneSessionDrafts(draftDirectory, keepDrafts);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Could not save session drafts: {ex}");
            ShowStatus("Could not save the session drafts");
            return false;
        }

        session.Name = name;
        session.Files = files;
        session.ActiveIndex = ActiveDocument is { } active ? Documents.IndexOf(active) : -1;
        session.SavedAtUtc = DateTime.UtcNow;

        if (existing is null)
        {
            Settings.Sessions.Add(session);
        }

        Settings.CurrentSessionName = name;
        CurrentSessionName = name;
        RebuildSessions();
        SaveSettingsNow();
        ShowStatus(string.Format(Strings.Status_SessionSaved, name));
        return true;
    }

    public async Task<bool> OpenNamedSessionAsync(NamedSession session)
    {
        if (!await CloseWorkspaceForSessionSwitchAsync())
        {
            return false;
        }

        var opened = 0;
        var skipped = 0;
        var draftDirectory = GetSessionDraftDirectory(session);

        foreach (var file in session.Files)
        {
            var draftPath = ResolveSessionDraftPath(draftDirectory, file.DraftFile);
            var draftContent = await TryReadSessionDraftAsync(draftPath).ConfigureAwait(true);
            if (draftContent is not null)
            {
                var draftDocument = CreateDocument();
                draftDocument.RestoreSessionDraft(file, draftContent, draftPath);
                AddDocument(draftDocument, activate: false);
                if (!string.IsNullOrWhiteSpace(file.Path) && File.Exists(file.Path))
                {
                    AddRecentFile(file.Path);
                }

                opened++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(file.Path) || !File.Exists(file.Path))
            {
                skipped++;
                continue;
            }

            var document = CreateDocument();
            try
            {
                await document.LoadAsync(file.Path);
                document.ApplySessionState(file.CaretOffset, file.IsPinned, file.TabColor, file.IsReadOnly);
                AddDocument(document, activate: false);
                AddRecentFile(file.Path);
                opened++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                document.Dispose();
                skipped++;
            }
        }

        if (Documents.Count > 0)
        {
            ActiveDocument = Documents[Math.Clamp(session.ActiveIndex, 0, Documents.Count - 1)];
        }

        Settings.CurrentSessionName = session.Name;
        CurrentSessionName = session.Name;
        RebuildSessions();
        SaveSettingsNow();

        var suffix = skipped > 0 ? $" ({skipped} file(s) unavailable)" : string.Empty;
        ShowStatus(string.Format(Strings.Status_SessionOpened, session.Name, opened, suffix));
        return true;
    }

    public async Task<bool> RenameSessionAsync(string oldName, string newName)
    {
        newName = newName.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            return false;
        }

        if (Settings.Sessions.Any(session =>
                !string.Equals(session.Name, oldName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(session.Name, newName, StringComparison.OrdinalIgnoreCase)))
        {
            await _dialogs.ShowErrorAsync(Strings.Dialog_SessionExistsTitle, string.Format(Strings.Dialog_SessionExistsMessage, newName));
            return false;
        }

        var session = Settings.Sessions.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, oldName, StringComparison.OrdinalIgnoreCase));

        if (session is null)
        {
            return false;
        }

        session.Name = newName;
        Settings.CurrentSessionName = newName;
        CurrentSessionName = newName;
        RebuildSessions();
        SaveSettingsNow();
        ShowStatus(string.Format(Strings.Status_SessionRenamed, newName));
        return true;
    }

    public void DeleteSession(string name)
    {
        var session = Settings.Sessions.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));

        if (session is null)
        {
            return;
        }

        Settings.Sessions.Remove(session);
        DeleteSessionDraftFiles(session);
        if (string.Equals(Settings.CurrentSessionName, name, StringComparison.OrdinalIgnoreCase))
        {
            Settings.CurrentSessionName = null;
            CurrentSessionName = null;
        }

        RebuildSessions();
        SaveSettingsNow();
        ShowStatus(string.Format(Strings.Status_SessionDeleted, name));
    }

    private async Task<bool> CloseWorkspaceForSessionSwitchAsync()
    {
        if (Documents.Count > 0)
        {
            var currentName = CurrentSessionName;
            if (!string.IsNullOrWhiteSpace(currentName) && SessionNameExists(currentName))
            {
                // Switching sessions snapshots the current workspace into its
                // named session. Unsaved tabs are kept as drafts, exactly like
                // the no-prompt recovery behavior on application exit.
                if (!await SaveSessionWithNameAsync(currentName, replaceExisting: true))
                {
                    return false;
                }
            }
            else if (Documents.Any(document => document.IsDirty))
            {
                // The workspace has unsaved work but no named session yet.
                // Keep it accessible instead of discarding it or prompting.
                if (!await SaveSessionWithNameAsync(
                        SuggestedWorkspaceSnapshotName(),
                        replaceExisting: false))
                {
                    return false;
                }
            }
        }

        foreach (var document in Documents.ToList())
        {
            RemoveDocument(document);
        }

        return true;
    }

    private string? GetSessionDraftDirectory(NamedSession session) =>
        string.IsNullOrWhiteSpace(session.Id)
            ? null
            : Path.Combine(_sessionDraftsDirectory, session.Id);

    private static string? ResolveSessionDraftPath(string? draftDirectory, string? draftFile)
    {
        if (string.IsNullOrWhiteSpace(draftDirectory) || string.IsNullOrWhiteSpace(draftFile))
        {
            return null;
        }

        return Path.Combine(draftDirectory, Path.GetFileName(draftFile));
    }

    private static async Task<string?> TryReadSessionDraftAsync(string? draftPath)
    {
        if (string.IsNullOrWhiteSpace(draftPath))
        {
            return null;
        }

        try
        {
            return await File.ReadAllTextAsync(draftPath).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void PruneSessionDrafts(string draftDirectory, HashSet<string> keepDrafts)
    {
        if (!Directory.Exists(draftDirectory))
        {
            return;
        }

        foreach (var draft in Directory.EnumerateFiles(draftDirectory, "*.txt"))
        {
            if (keepDrafts.Contains(Path.GetFileName(draft)))
            {
                continue;
            }

            try
            {
                File.Delete(draft);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Could not remove stale session draft: {ex.Message}");
            }
        }
    }

    private void DeleteSessionDraftFiles(NamedSession session)
    {
        var directory = GetSessionDraftDirectory(session);
        if (directory is null)
        {
            return;
        }

        // Open tabs from this session must not keep writing into a removed folder.
        foreach (var document in Documents)
        {
            if (document.SessionDraftPath is { } draftPath && IsInsideDirectory(directory, draftPath))
            {
                document.SetSessionDraftPath(null);
            }
        }

        if (!Directory.Exists(directory))
        {
            return;
        }

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Could not remove session drafts: {ex.Message}");
        }
    }

    private static bool IsInsideDirectory(string directory, string path)
    {
        var root = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(path);
        return target.StartsWith(root, PathComparison);
    }

    private void RebuildSessions()
    {
        Sessions.Clear();

        foreach (var session in Settings.Sessions
                     .OrderByDescending(session => session.SavedAtUtc)
                     .ThenBy(session => session.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(session.Id))
            {
                session.Id = Guid.NewGuid().ToString("N");
            }

            Sessions.Add(new SessionItemViewModel(
                session,
                OpenSessionCommand,
                RenameSessionItemCommand,
                DeleteSessionItemCommand));
        }

        if (Settings.CurrentSessionName is { } current &&
            Settings.Sessions.Any(session => string.Equals(session.Name, current, StringComparison.OrdinalIgnoreCase)))
        {
            CurrentSessionName = current;
        }
        else
        {
            CurrentSessionName = null;
        }

        OnPropertyChanged(nameof(HasSessions));
        OnPropertyChanged(nameof(HasNoSessions));
        OnPropertyChanged(nameof(CurrentSessionDisplay));
        OnPropertyChanged(nameof(CanManageCurrentSession));
    }

    private string SuggestedSessionName()
    {
        if (!string.IsNullOrWhiteSpace(CurrentSessionName))
        {
            return CurrentSessionName;
        }

        var folder = ActiveFolderName;
        return folder == "Session" ? "My session" : folder;
    }

    [RelayCommand]
    private Task ShowAboutAsync() => _dialogs.ShowAboutAsync();

    [RelayCommand]
    private void Exit() => ExitRequested?.Invoke(this, EventArgs.Empty);

    public void OpenSearchResult(SearchResultItem? result) => FindReplace.SelectResult(result);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _statusTimer?.Stop();
        _settingsTimer?.Stop();
        _recoveryTimer?.Stop();
        _recoveryDebounce?.Stop();
        _fileWatcher.FileChanged -= OnFileChangedOnDisk;
        EditorSettings.Changed -= OnEditorSettingsChanged;
        FindReplace.CloseRequested -= OnFindCloseRequested;
        FindReplace.FocusRequested -= OnFindFocusRequested;

        foreach (var document in Documents.ToList())
        {
            UnsubscribeDocument(document);
            document.Dispose();
        }
    }

    partial void OnActiveDocumentChanged(DocumentViewModel? value)
    {
        foreach (var document in Documents)
        {
            document.IsActive = ReferenceEquals(document, value);
        }

        FindReplace.SetTarget(value);
        OnPropertyChanged(nameof(WindowTitle));
        NotifyStatusProperties();
        NotifyWorkspaceProperties();

        if (!string.Equals(StatusMessage, Strings.Status_Ready, StringComparison.Ordinal))
        {
            _statusTimer?.Stop();
            StatusMessage = Strings.Status_Ready;
        }

        if (!HasDocuments)
        {
            IsFindBarOpen = false;
        }
    }

    partial void OnCurrentSessionNameChanged(string? value)
    {
        OnPropertyChanged(nameof(CurrentSessionDisplay));
        OnPropertyChanged(nameof(CanManageCurrentSession));
    }

    private DocumentViewModel CreateDocument() =>
        new(_fileService, _languageService, ThemeService, EditorSettings, ++_untitledCounter);

    private void AddDocument(DocumentViewModel document, bool activate = true)
    {
        document.ActivateRequested += OnDocumentActivateRequested;
        document.CloseRequested += OnDocumentCloseRequested;
        document.ActionRequested += OnDocumentActionRequested;
        document.PropertyChanged += OnDocumentPropertyChanged;

        Documents.Add(document);
        TrackDocument(document);

        if (activate)
        {
            ActiveDocument = document;
        }

        OnPropertyChanged(nameof(HasDocuments));
        OnPropertyChanged(nameof(IsWelcomeVisible));
        ScheduleRecoverySnapshot();
    }

    private void RemoveDocument(DocumentViewModel document)
    {
        var index = Documents.IndexOf(document);
        UnsubscribeDocument(document);
        UntrackDocument(document);
        Documents.Remove(document);
        document.Dispose();

        if (ReferenceEquals(ActiveDocument, document))
        {
            ActiveDocument = Documents.Count == 0
                ? null
                : Documents[Math.Clamp(index, 0, Documents.Count - 1)];
        }

        OnPropertyChanged(nameof(HasDocuments));
        OnPropertyChanged(nameof(IsWelcomeVisible));
        OnPropertyChanged(nameof(WindowTitle));
        ScheduleRecoverySnapshot();
    }

    private void UnsubscribeDocument(DocumentViewModel document)
    {
        document.ActivateRequested -= OnDocumentActivateRequested;
        document.CloseRequested -= OnDocumentCloseRequested;
        document.ActionRequested -= OnDocumentActionRequested;
        document.PropertyChanged -= OnDocumentPropertyChanged;
    }

    public async Task<bool> SaveDocumentAsync(DocumentViewModel? document, bool saveAs = false)
    {
        if (document is null)
        {
            return false;
        }

        if (!saveAs && !document.IsDirty)
        {
            ShowStatus(Strings.Status_NoChangesToSave);
            return true;
        }

        try
        {
            if (!saveAs && document.IsUntitled && document.SessionDraftPath is not null)
            {
                await document.SaveSessionDraftAsync();
                ShowStatus(string.Format(Strings.Status_SavedDocToSession, document.Title));
                return true;
            }

            string? targetPath = null;
            if (saveAs || document.IsUntitled)
            {
                targetPath = await _dialogs.PickSaveFileAsync(document);
                if (string.IsNullOrWhiteSpace(targetPath))
                {
                    return false;
                }
            }

            var previousPath = document.FilePath;
            if (previousPath is not null)
            {
                _fileWatcher.Suppress(previousPath);
            }

            if (targetPath is not null)
            {
                _fileWatcher.Suppress(targetPath);
            }

            await document.SaveAsync(targetPath);

            if (previousPath is not null &&
                document.FilePath is not null &&
                !PathsEqual(previousPath, document.FilePath))
            {
                _fileWatcher.Untrack(previousPath);
            }

            TrackDocument(document);
            _declinedExternalChanges.Remove(document.Id);
            AddRecentFile(document.FilePath ?? targetPath!);
            ShowStatus(string.Format(Strings.Status_SavedDoc, document.Title));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            await _dialogs.ShowErrorAsync("Could not save file", ex.Message);
            return false;
        }
    }

    private async Task<bool> ConfirmCloseDocumentAsync(DocumentViewModel? document)
    {
        if (document is null)
        {
            return true;
        }

        if (!document.IsDirty)
        {
            return true;
        }

        var choice = await _dialogs.AskSaveChangesAsync(document.Title);
        return choice switch
        {
            SaveChangesChoice.Save => await SaveDocumentAsync(document, saveAs: false),
            SaveChangesChoice.DontSave => true,
            _ => false
        };
    }

    private async Task CloseDocumentAsync(DocumentViewModel? document)
    {
        if (document is null)
        {
            return;
        }

        if (await ConfirmCloseDocumentAsync(document))
        {
            RemoveDocument(document);
        }
    }

    private void CycleDocument(bool forward)
    {
        if (Documents.Count < 2 || ActiveDocument is null)
        {
            return;
        }

        var index = Documents.IndexOf(ActiveDocument);
        index = forward
            ? (index + 1) % Documents.Count
            : (index - 1 + Documents.Count) % Documents.Count;
        ActiveDocument = Documents[index];
    }

    private void BeginFind(bool replaceMode)
    {
        if (ActiveDocument?.Host is not { } host)
        {
            return;
        }

        FindReplace.IsReplaceMode = replaceMode;

        if (host.SelectionLength is > 0 and < 300)
        {
            var selected = ActiveDocument.Document.GetText(host.SelectionStart, host.SelectionLength);
            if (!selected.Contains('\n') && !selected.Contains('\r'))
            {
                FindReplace.SearchText = selected;
            }
        }

        IsFindBarOpen = true;
        FindReplace.RequestFocus();
    }

    public async Task OpenFileSearchResultAsync(FileSearchResult result)
    {
        await OpenPathsAsync([result.FilePath]);

        var document = ActiveDocument;
        if (document?.FilePath is null || !PathsEqual(document.FilePath, result.FilePath))
        {
            return;
        }

        document.SavedCaretOffset = Math.Clamp(result.Offset, 0, document.Document.TextLength);
        document.PendingSelectionLength = result.Length;
        document.PendingSelectionLine = result.Line;

        if (document.Host is { } host)
        {
            document.ApplyPendingSelection(host);
            host.Focus();
        }

        ShowStatus(string.Format(Strings.Status_OpenedAtLine, document.Title, result.Line));
    }

    public async Task CloseDocumentsAsync(IEnumerable<DocumentViewModel> documents)
    {
        foreach (var document in documents.ToList())
        {
            if (!await ConfirmCloseDocumentAsync(document))
            {
                return;
            }

            RemoveDocument(document);
        }
    }

    public async Task CloseAllDocumentsAsync()
    {
        foreach (var document in Documents.ToList())
        {
            if (!await ConfirmCloseDocumentAsync(document))
            {
                return;
            }

            RemoveDocument(document);
        }
    }

    public void TogglePin(DocumentViewModel document)
    {
        document.IsPinned = !document.IsPinned;
        var index = Documents.IndexOf(document);
        if (index < 0)
        {
            return;
        }

        var pinnedOthers = Documents.Count(candidate =>
            candidate.IsPinned && !ReferenceEquals(candidate, document));
        var target = Math.Clamp(pinnedOthers, 0, Documents.Count - 1);
        Documents.Move(index, target);
        ActiveDocument = document;
        ShowStatus(document.IsPinned ? "Tab pinned" : "Tab unpinned");
    }

    public void MoveTab(DocumentViewModel document, int direction)
    {
        var index = Documents.IndexOf(document);
        if (index < 0)
        {
            return;
        }

        var target = direction switch
        {
            int.MinValue => 0,
            int.MaxValue => Documents.Count - 1,
            _ => Math.Clamp(index + direction, 0, Documents.Count - 1)
        };

        if (target != index)
        {
            Documents.Move(index, target);
            ActiveDocument = document;
        }
    }

    private async Task RestoreSessionAsync()
    {
        var session = Settings.Session;

        foreach (var entry in session.Documents)
        {
            if (string.IsNullOrWhiteSpace(entry.Path) || !File.Exists(entry.Path))
            {
                continue;
            }

            var document = CreateDocument();
            try
            {
                await document.LoadAsync(entry.Path);
                document.ApplySessionState(entry.CaretOffset, entry.IsPinned, entry.TabColor, entry.IsReadOnly);
                AddDocument(document, activate: false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                document.Dispose();
            }
        }

        if (Documents.Count > 0)
        {
            ActiveDocument = Documents[Math.Clamp(session.ActiveIndex, 0, Documents.Count - 1)];
        }
    }

    private void SaveSession()
    {
        if (!Settings.RestoreSession)
        {
            Settings.Session = new SessionState();
            return;
        }

        var documents = Documents
            .Where(document => document.FilePath is not null)
            .Select(document => new SessionDocument
            {
                Path = document.FilePath!,
                CaretOffset = document.SavedCaretOffset,
                IsPinned = document.IsPinned,
                TabColor = document.TabColorHex,
                IsReadOnly = document.IsReadOnly
            })
            .ToList();

        var activeIndex = ActiveDocument?.FilePath is null
            ? (documents.Count > 0 ? documents.Count - 1 : -1)
            : documents.FindIndex(entry => PathsEqual(entry.Path, ActiveDocument.FilePath));

        Settings.Session = new SessionState
        {
            Documents = documents,
            ActiveIndex = activeIndex
        };
    }

    private void AddRecentFile(string path)
    {
        var comparison = PathComparison;
        Settings.RecentFiles.RemoveAll(entry => string.Equals(entry, path, comparison));
        Settings.RecentFiles.Insert(0, path);

        if (Settings.RecentFiles.Count > 15)
        {
            Settings.RecentFiles.RemoveRange(15, Settings.RecentFiles.Count - 15);
        }

        RebuildRecentFiles();
        ScheduleSettingsSave();
    }

    private void RebuildRecentFiles()
    {
        RecentFiles.Clear();
        foreach (var path in Settings.RecentFiles.Distinct(PathComparer).Where(File.Exists).Take(15))
        {
            RecentFiles.Add(new RecentFileViewModel(path, OpenRecentCommand));
        }

        OnPropertyChanged(nameof(HasRecentFiles));
        OnPropertyChanged(nameof(HasNoRecentFiles));
    }

    private void OnDocumentActivateRequested(object? sender, EventArgs e)
    {
        if (sender is DocumentViewModel document)
        {
            ActiveDocument = document;
        }
    }

    private async void OnDocumentActionRequested(object? sender, DocumentActionEventArgs e)
    {
        if (sender is not DocumentViewModel document)
        {
            return;
        }

        try
        {
            await HandleDocumentActionAsync(document, e.Action);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Could not complete the action", ex.Message);
        }
    }

    /// <summary>Executes a tab context-menu action. Public for tests.</summary>
    public async Task HandleDocumentActionAsync(DocumentViewModel document, string action)
    {
        switch (action)
        {
            case "close":
                await CloseDocumentAsync(document);
                break;
            case "close-others":
                await CloseDocumentsAsync(Documents.Where(candidate => !ReferenceEquals(candidate, document)));
                break;
            case "close-right":
                await CloseDocumentsAsync(Documents.Skip(Documents.IndexOf(document) + 1));
                break;
            case "close-left":
                await CloseDocumentsAsync(Documents.Take(Documents.IndexOf(document)));
                break;
            case "close-all":
                await CloseAllDocumentsAsync();
                break;
            case "save":
                await SaveDocumentAsync(document, saveAs: false);
                break;
            case "save-as":
                await SaveDocumentAsync(document, saveAs: true);
                break;
            case "reload":
                await ReloadDocumentAsync(document);
                break;
            case "rename":
                await RenameDocumentAsync(document);
                break;
            case "trash":
                await MoveDocumentToTrashAsync(document);
                break;
            case "open-folder":
                await OpenContainingFolderAsync(document);
                break;
            case "open-terminal":
                await OpenTerminalAsync(document);
                break;
            case "copy-path":
                await CopyFilePathAsync(document);
                break;
            case "copy-name":
                if (document.FilePath is { } filePath)
                {
                    await _dialogs.SetClipboardTextAsync(Path.GetFileName(filePath));
                    ShowStatus("File name copied");
                }
                break;
            case "copy-content":
                await _dialogs.SetClipboardTextAsync(document.Document.Text);
                ShowStatus("Document content copied");
                break;
            case "pin":
                TogglePin(document);
                break;
            case "readonly":
                document.IsReadOnly = !document.IsReadOnly;
                ShowStatus(document.IsReadOnly ? "Document is read-only" : "Document is editable");
                break;
            case "move-left":
                MoveTab(document, -1);
                break;
            case "move-right":
                MoveTab(document, 1);
                break;
            case "move-start":
                MoveTab(document, int.MinValue);
                break;
            case "move-end":
                MoveTab(document, int.MaxValue);
                break;
            default:
                if (action.StartsWith("color:", StringComparison.Ordinal))
                {
                    var color = action["color:".Length..];
                    document.TabColorHex = color == "none" ? null : color;
                    ShowStatus(color == "none" ? "Tab color cleared" : "Tab color applied");
                }

                break;
        }
    }

    private async void OnDocumentCloseRequested(object? sender, EventArgs e)
    {
        if (sender is not DocumentViewModel document)
        {
            return;
        }

        try
        {
            await CloseDocumentAsync(document);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Could not close the document", ex.Message);
        }
    }

    private void OnDocumentPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentViewModel.IsDirty) &&
            sender is DocumentViewModel dirtyDocument &&
            dirtyDocument.IsDirty)
        {
            ScheduleRecoverySnapshot();
        }

        if (!ReferenceEquals(sender, ActiveDocument))
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(DocumentViewModel.IsDirty):
            case nameof(DocumentViewModel.Title):
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(ActiveFolderName));
                OnPropertyChanged(nameof(ActiveFolderPath));
                break;
            case nameof(DocumentViewModel.FilePath):
                OnPropertyChanged(nameof(ActiveFolderName));
                OnPropertyChanged(nameof(ActiveFolderPath));
                break;
            case nameof(DocumentViewModel.Language):
                OnPropertyChanged(nameof(ActiveLanguageName));
                OnPropertyChanged(nameof(ActiveLanguageAccentBrush));
                OnPropertyChanged(nameof(ActiveLanguageSoftBrush));
                OnPropertyChanged(nameof(StatusLanguage));
                break;
            case nameof(DocumentViewModel.CaretLine):
                OnPropertyChanged(nameof(StatusCaretLine));
                break;
            case nameof(DocumentViewModel.CaretColumn):
                OnPropertyChanged(nameof(StatusCaretColumn));
                break;
            case nameof(DocumentViewModel.SelectionLength):
                OnPropertyChanged(nameof(StatusSelectionLength));
                break;
            case nameof(DocumentViewModel.LineCount):
                OnPropertyChanged(nameof(StatusLineCount));
                OnPropertyChanged(nameof(StatusLinesText));
                break;
            case nameof(DocumentViewModel.Length):
                OnPropertyChanged(nameof(StatusLength));
                OnPropertyChanged(nameof(StatusLengthText));
                break;
            case nameof(DocumentViewModel.Zoom):
                OnPropertyChanged(nameof(StatusZoom));
                break;
            case nameof(DocumentViewModel.Encoding):
                OnPropertyChanged(nameof(StatusEncoding));
                break;
            case nameof(DocumentViewModel.LineEnding):
                OnPropertyChanged(nameof(StatusLineEnding));
                break;
        }
    }

    private void NotifyWorkspaceProperties()
    {
        OnPropertyChanged(nameof(ActiveFolderName));
        OnPropertyChanged(nameof(ActiveFolderPath));
        OnPropertyChanged(nameof(ActiveLanguageName));
        OnPropertyChanged(nameof(ActiveLanguageAccentBrush));
        OnPropertyChanged(nameof(ActiveLanguageSoftBrush));
    }

    private void NotifyStatusProperties()
    {
        OnPropertyChanged(nameof(StatusCaretLine));
        OnPropertyChanged(nameof(StatusCaretColumn));
        OnPropertyChanged(nameof(StatusSelectionLength));
        OnPropertyChanged(nameof(StatusLineCount));
        OnPropertyChanged(nameof(StatusLinesText));
        OnPropertyChanged(nameof(StatusLength));
        OnPropertyChanged(nameof(StatusLengthText));
        OnPropertyChanged(nameof(StatusZoom));
        OnPropertyChanged(nameof(StatusEncoding));
        OnPropertyChanged(nameof(StatusLineEnding));
        OnPropertyChanged(nameof(StatusLanguage));
    }

    private void OnFindCloseRequested(object? sender, EventArgs e) => CloseFind();

    private void OnFindFocusRequested(object? sender, EventArgs e) =>
        FindFocusRequested?.Invoke(this, EventArgs.Empty);

    private void OnEditorSettingsChanged(object? sender, EventArgs e) => ScheduleSettingsSave();

    private void TrackDocument(DocumentViewModel document)
    {
        if (document.FilePath is { } path)
        {
            _fileWatcher.Track(path);
        }
    }

    private void UntrackDocument(DocumentViewModel document)
    {
        if (document.FilePath is { } path)
        {
            _fileWatcher.Untrack(path);
        }
    }

    private void OnFileChangedOnDisk(object? sender, string path)
    {
        Dispatcher.UIThread.Post(() => _ = HandleExternalChangeAsync(path));
    }

    /// <summary>Handles a file changed by another program. Public for tests and tooling.</summary>
    public async Task HandleExternalChangeAsync(string path)
    {
        var document = Documents.FirstOrDefault(candidate =>
            candidate.FilePath is not null && PathsEqual(candidate.FilePath, path));

        if (document is null || !_externalChangeInProgress.Add(document.Id))
        {
            return;
        }

        try
        {
            if (!File.Exists(path))
            {
                ShowStatus(string.Format(Strings.Status_FileDeletedOnDisk, document.Title));
                return;
            }

            if (document.IsDirty && _declinedExternalChanges.Contains(document.Id))
            {
                return;
            }

            if (document.IsDirty)
            {
                var reload = await _dialogs.ConfirmAsync(
                    "File changed on disk",
                    $"\"{document.Title}\" changed on disk. Reload it and discard your unsaved changes?",
                    Strings.Tab_Reload);

                if (!reload)
                {
                    _declinedExternalChanges.Add(document.Id);
                    ShowStatus(string.Format(Strings.Status_KeptLocalChanges, document.Title));
                    return;
                }
            }

            await document.ReloadAsync();
            _declinedExternalChanges.Remove(document.Id);
            ShowStatus($"Reloaded {document.Title} after an external change");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            await _dialogs.ShowErrorAsync("Could not reload the changed file", ex.Message);
        }
        finally
        {
            _externalChangeInProgress.Remove(document.Id);
        }
    }

    private void StartRecoveryTimer()
    {
        if (_disposed)
        {
            return;
        }

        if (!Settings.RecoveryEnabled)
        {
            StopRecoveryTimers();
            _sessionService.Clear();
            return;
        }

        _recoverySuppressed = false;

        if (_recoveryTimer is null)
        {
            _recoveryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            _recoveryTimer.Tick += async (_, _) => await SaveRecoverySnapshotAsync();
        }

        if (_recoveryDebounce is null)
        {
            _recoveryDebounce = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _recoveryDebounce.Tick += async (_, _) =>
            {
                _recoveryDebounce?.Stop();
                await SaveRecoverySnapshotAsync();
            };
        }

        _recoveryTimer.Start();
    }

    private void StopRecoveryTimers()
    {
        _recoverySuppressed = true;
        _recoveryTimer?.Stop();
        _recoveryDebounce?.Stop();
    }

    private void ScheduleRecoverySnapshot()
    {
        if (_disposed || _recoverySuppressed || !Settings.RecoveryEnabled)
        {
            return;
        }

        if (_recoveryDebounce is null)
        {
            _recoveryDebounce = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _recoveryDebounce.Tick += async (_, _) =>
            {
                _recoveryDebounce?.Stop();
                await SaveRecoverySnapshotAsync();
            };
        }

        _recoveryDebounce.Stop();
        _recoveryDebounce.Start();
    }

    /// <summary>Captures the workspace for crash recovery. Public for tests and tooling.</summary>
    public async Task<bool> SaveRecoverySnapshotAsync()
    {
        if (_disposed || _recoverySuppressed || _recoverySaveInProgress || !Settings.RecoveryEnabled)
        {
            return false;
        }

        _recoverySaveInProgress = true;
        try
        {
            if (Documents.Count == 0)
            {
                await _sessionService.ClearAsync();
                return true;
            }

            var snapshot = new RecoverySnapshot
            {
                ActiveIndex = ActiveDocument is null ? -1 : Documents.IndexOf(ActiveDocument),
                SavedAtUtc = DateTime.UtcNow
            };

            foreach (var document in Documents)
            {
                var shouldBackUpContent = document.IsDirty || (document.IsUntitled && document.Length > 0);

                // Extremely large documents are recovered from disk instead of copying
                // tens of megabytes into the recovery folder every few seconds.
                var content = shouldBackUpContent && document.Length <= 20 * 1024 * 1024
                    ? document.Document.Text
                    : null;

                if (content is null && document.FilePath is null)
                {
                    continue;
                }

                snapshot.Documents.Add(new RecoveryDocument
                {
                    Id = document.Id.ToString("N"),
                    Path = document.FilePath,
                    Title = document.Title,
                    Content = content,
                    CaretOffset = document.Host?.CaretOffset ?? document.SavedCaretOffset,
                    Encoding = document.Encoding.Kind,
                    LineEnding = document.LineEnding.Value,
                    LanguageId = document.Language.Id,
                    IsDirty = document.IsDirty,
                    IsPinned = document.IsPinned,
                    TabColor = document.TabColorHex,
                    IsReadOnly = document.IsReadOnly
                });
            }

            await _sessionService.SaveAsync(snapshot);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not save the recovery snapshot: {ex}");
            return false;
        }
        finally
        {
            _recoverySaveInProgress = false;
        }
    }

    private async Task<bool> TryRestoreRecoveryAsync()
    {
        if (!Settings.RecoveryEnabled)
        {
            await _sessionService.ClearAsync();
            return false;
        }

        var recovery = await _sessionService.LoadAsync();
        if (recovery is null || recovery.Documents.Count == 0)
        {
            return false;
        }

        var restored = 0;

        foreach (var entry in recovery.Documents)
        {
            var document = CreateDocument();
            try
            {
                if (await document.RestoreRecoveryAsync(entry))
                {
                    AddDocument(document, activate: false);
                    restored++;
                }
                else
                {
                    document.Dispose();
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                document.Dispose();
            }
        }

        if (restored == 0)
        {
            await _sessionService.ClearAsync();
            return false;
        }

        ActiveDocument = Documents[Math.Clamp(recovery.ActiveIndex, 0, Documents.Count - 1)];
        ShowStatus($"Restored {restored} document{(restored == 1 ? string.Empty : "s")} from the previous session");
        return true;
    }

    private void ScheduleSettingsSave()
    {
        if (_disposed)
        {
            return;
        }

        _settingsTimer ??= CreateTimer(TimeSpan.FromMilliseconds(700), () =>
        {
            _settingsTimer?.Stop();
            _settingsService.Save(Settings);
        });

        _settingsTimer.Stop();
        _settingsTimer.Start();
    }

    private void SaveSettingsNow()
    {
        _settingsTimer?.Stop();
        _settingsService.Save(Settings);
    }

    private void ShowStatus(string message)
    {
        StatusMessage = message;

        _statusTimer ??= CreateTimer(TimeSpan.FromSeconds(7), () =>
        {
            _statusTimer?.Stop();
            StatusMessage = Strings.Status_Ready;
        });

        _statusTimer.Stop();
        _statusTimer.Start();
    }

    private static DispatcherTimer CreateTimer(TimeSpan interval, Action onTick)
    {
        var timer = new DispatcherTimer { Interval = interval };
        timer.Tick += (_, _) => onTick();
        return timer;
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(left, right, PathComparison);

    private static StringComparison PathComparison =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static StringComparer PathComparer =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
}
