using NotepadMoreOS.Resources;
using System.ComponentModel;
using System.Text;
using Avalonia.Media;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotepadMoreOS.Models;
using NotepadMoreOS.Services;

namespace NotepadMoreOS.ViewModels;

public partial class DocumentViewModel : ObservableObject, IDisposable
{
    private readonly FileService _fileService;
    private readonly LanguageService _languageService;
    private TextDocument _document;
    private IBrush _languageAccentBrush = new SolidColorBrush(Color.Parse("#8A93A0"));
    private IBrush _languageSoftBrush = new SolidColorBrush(Color.FromArgb(0x28, 0x5B, 0xD0, 0x8E));
    private IBrush? _tabColorBrush;
    private TextEncodingKind _savedEncoding = TextEncodingKind.Utf8;
    private bool _isLoading;
    private bool _disposed;

    public DocumentViewModel(
        FileService fileService,
        LanguageService languageService,
        ThemeService themeService,
        EditorSettingsViewModel editorSettings,
        int untitledNumber)
    {
        _fileService = fileService;
        _languageService = languageService;
        ThemeService = themeService;
        EditorSettings = editorSettings;

        _title = $"new {untitledNumber}";
        _lineEnding = LineEndingCatalog.Get(LineEndingCatalog.Detect(string.Empty));

        _document = new TextDocument();
        AttachDocument(_document);
        _document.UndoStack.MarkAsOriginalFile();
        UpdateLanguageBrushes();

        EditorSettings.Changed += OnEditorSettingsChanged;
    }

    public ThemeService ThemeService { get; }

    public EditorSettingsViewModel EditorSettings { get; }

    public Guid Id { get; } = Guid.NewGuid();

    public TextDocument Document => _document;

    public IEditorHost? Host { get; set; }

    public string? FilePath { get; private set; }

    /// <summary>
    /// Backing draft file inside a named session, used by untitled tabs so
    /// Save updates the session copy without asking for a new file name.
    /// </summary>
    public string? SessionDraftPath { get; private set; }

    public bool IsUntitled => string.IsNullOrEmpty(FilePath);

    public bool HasFilePath => !string.IsNullOrEmpty(FilePath);

    public string ExtensionBadge
    {
        get
        {
            if (string.IsNullOrEmpty(FilePath))
            {
                return "TXT";
            }

            var extension = Path.GetExtension(FilePath).TrimStart('.');
            if (string.IsNullOrEmpty(extension))
            {
                return "TXT";
            }

            extension = extension.ToUpperInvariant();
            return extension.Length > 3 ? extension[..3] : extension;
        }
    }

    public int SavedCaretOffset { get; set; }

    public int PendingSelectionLength { get; set; }

    public int PendingSelectionLine { get; set; }

    public void ApplyPendingSelection(IEditorHost host)
    {
        if (PendingSelectionLength <= 0)
        {
            return;
        }

        var start = Math.Clamp(SavedCaretOffset, 0, host.Document.TextLength);
        var length = Math.Clamp(PendingSelectionLength, 0, host.Document.TextLength - start);
        host.Select(start, length);

        if (PendingSelectionLine > 0)
        {
            host.ScrollToLine(PendingSelectionLine);
        }

        PendingSelectionLength = 0;
        PendingSelectionLine = 0;
    }

    public int Length => _document.TextLength;

    public int LineCount => _document.LineCount;

    public bool CanUndo => _document.UndoStack.CanUndo;

    public bool CanRedo => _document.UndoStack.CanRedo;

    public double EffectiveFontSize => Math.Clamp(EditorSettings.FontSize * Zoom, 6, 96);

    public string ZoomPercent => $"{Zoom * 100:0}%";

    public string LanguageScope => Language.Scope;

    public IBrush LanguageAccentBrush => _languageAccentBrush;

    public IBrush LanguageSoftBrush => _languageSoftBrush;

    public IBrush? TabColorBrush => _tabColorBrush;

    public bool HasTabColor => _tabColorBrush is not null;

    public bool CanClose => !IsPinned;

    public string PinMenuHeader => IsPinned ? Strings.Tab_Unpin : Strings.Tab_Pin;

    public string ReadOnlyMenuHeader => IsReadOnly ? Strings.Tab_ReadWrite : Strings.Tab_ReadOnly;

    public string? CommentPrefix => Language.Id switch
    {
        "python" or "shellscript" or "yaml" or "perl" or "ruby" or "r" or "toml" or "properties" or "makefile" or "dockerfile" => "#",
        "sql" or "lua" or "haskell" => "--",
        "html" or "xml" or "css" or "scss" or "less" or "markdown" => null,
        _ => "//"
    };

    public event EventHandler? CloseRequested;
    public event EventHandler? ActivateRequested;
    public event EventHandler<DocumentActionEventArgs>? ActionRequested;

    [ObservableProperty] private string _title;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private LanguageInfo _language = LanguageInfo.PlainText;
    [ObservableProperty] private EncodingOption _encoding = EncodingCatalog.Get(TextEncodingKind.Utf8);
    [ObservableProperty] private LineEndingOption _lineEnding;
    [ObservableProperty] private int _caretLine = 1;
    [ObservableProperty] private int _caretColumn = 1;
    [ObservableProperty] private int _selectionLength;
    [ObservableProperty] private double _zoom = 1.0;
    [ObservableProperty] private bool _isReadOnly;
    [ObservableProperty] private bool _isPinned;
    [ObservableProperty] private string? _tabColorHex;

    public async Task LoadAsync(string path)
    {
        var loaded = await _fileService.LoadAsync(path).ConfigureAwait(true);
        ApplyLoaded(path, loaded, resetCaret: true);
    }

    public async Task<bool> ReloadAsync()
    {
        if (string.IsNullOrEmpty(FilePath))
        {
            return false;
        }

        var previousCaret = SavedCaretOffset;
        var loaded = await _fileService.LoadAsync(FilePath).ConfigureAwait(true);
        ApplyLoaded(FilePath, loaded, resetCaret: false);
        SavedCaretOffset = Math.Clamp(previousCaret, 0, _document.TextLength);
        if (Host is not null)
        {
            Host.CaretOffset = SavedCaretOffset;
        }

        return true;
    }

    /// <summary>
    /// Reinterprets the file on disk with a specific encoding, discarding the
    /// current in-memory text. Callers must confirm before invoking this on a
    /// dirty document.
    /// </summary>
    public async Task<bool> ReopenWithEncodingAsync(EncodingOption encoding)
    {
        if (string.IsNullOrEmpty(FilePath))
        {
            return false;
        }

        var previousCaret = SavedCaretOffset;
        var loaded = await _fileService.LoadAsync(FilePath, encoding.Kind).ConfigureAwait(true);
        ApplyLoaded(FilePath, loaded, resetCaret: false);
        SavedCaretOffset = Math.Clamp(previousCaret, 0, _document.TextLength);
        Encoding = EncodingCatalog.Get(encoding.Kind);

        if (Host is not null)
        {
            Host.CaretOffset = SavedCaretOffset;
            Host.ScrollToLine(_document.GetLineByOffset(SavedCaretOffset).LineNumber);
        }

        return true;
    }

    /// <summary>
    /// Restores a document from a crash-recovery snapshot. When content was
    /// backed up it is treated as unsaved work and the document starts dirty.
    /// </summary>
    public async Task<bool> RestoreRecoveryAsync(RecoveryDocument recovery)
    {
        if (recovery.Content is not null)
        {
            _isLoading = true;
            try
            {
                FilePath = string.IsNullOrWhiteSpace(recovery.Path) ? null : Path.GetFullPath(recovery.Path);
                Title = string.IsNullOrWhiteSpace(recovery.Title)
                    ? (FilePath is null ? "recovered" : Path.GetFileName(FilePath))
                    : recovery.Title;

                _savedEncoding = recovery.Encoding;
                Encoding = EncodingCatalog.Get(recovery.Encoding);
                LineEnding = LineEndingCatalog.Get(recovery.LineEnding);
                Language = _languageService.FindById(recovery.LanguageId);

                var document = new TextDocument(recovery.Content);
                if (FilePath is not null)
                {
                    document.FileName = FilePath;
                }

                SetDocument(document);
                IsDirty = true;
                SavedCaretOffset = Math.Clamp(recovery.CaretOffset, 0, document.TextLength);
                IsPinned = recovery.IsPinned;
                TabColorHex = recovery.TabColor;
                IsReadOnly = recovery.IsReadOnly;
                PendingSelectionLength = 0;
                PendingSelectionLine = 0;
                UpdateStats();

                OnPropertyChanged(nameof(IsUntitled));
                OnPropertyChanged(nameof(HasFilePath));
                OnPropertyChanged(nameof(ExtensionBadge));
                OnPropertyChanged(nameof(FilePath));
                return true;
            }
            finally
            {
                _isLoading = false;
            }
        }

        if (string.IsNullOrWhiteSpace(recovery.Path) || !File.Exists(recovery.Path))
        {
            return false;
        }

        try
        {
            await LoadAsync(recovery.Path).ConfigureAwait(true);
            SavedCaretOffset = Math.Clamp(recovery.CaretOffset, 0, Document.TextLength);
            IsPinned = recovery.IsPinned;
            TabColorHex = recovery.TabColor;
            IsReadOnly = recovery.IsReadOnly;
            if (!string.IsNullOrWhiteSpace(recovery.Title))
            {
                Title = recovery.Title;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Restores an unsaved or untitled tab stored with a named session. The
    /// draft is applied only in memory; the original file is never touched.
    /// </summary>
    public bool RestoreSessionDraft(NamedSessionFile sessionFile, string content, string? sessionDraftPath)
    {
        _isLoading = true;
        try
        {
            FilePath = string.IsNullOrWhiteSpace(sessionFile.Path)
                ? null
                : Path.GetFullPath(sessionFile.Path);
            SessionDraftPath = sessionDraftPath;
            Title = string.IsNullOrWhiteSpace(sessionFile.Title)
                ? (FilePath is null ? "recovered" : Path.GetFileName(FilePath))
                : sessionFile.Title;

            _savedEncoding = sessionFile.Encoding;
            Encoding = EncodingCatalog.Get(sessionFile.Encoding);
            LineEnding = LineEndingCatalog.Get(sessionFile.LineEnding);
            Language = _languageService.FindById(sessionFile.LanguageId);

            var document = new TextDocument(content);
            if (FilePath is not null)
            {
                document.FileName = FilePath;
            }

            SetDocument(document);

            // A session draft is the saved baseline for its tab. Untitled tabs
            // have no disk file to compare against, so they must not start
            // dirty until the user edits them again.
            var startsDirty = sessionFile.IsDirty && FilePath is not null;
            if (startsDirty)
            {
                IsDirty = true;
            }
            else
            {
                document.UndoStack.MarkAsOriginalFile();
                IsDirty = false;
            }

            SavedCaretOffset = Math.Clamp(sessionFile.CaretOffset, 0, document.TextLength);
            IsPinned = sessionFile.IsPinned;
            TabColorHex = sessionFile.TabColor;
            IsReadOnly = sessionFile.IsReadOnly;
            PendingSelectionLength = 0;
            PendingSelectionLine = 0;
            UpdateStats();

            OnPropertyChanged(nameof(IsUntitled));
            OnPropertyChanged(nameof(HasFilePath));
            OnPropertyChanged(nameof(ExtensionBadge));
            OnPropertyChanged(nameof(FilePath));
            return true;
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// Writes an untitled tab back to its session draft file without changing
    /// the tab's untitled state or asking for a file name.
    /// </summary>
    public async Task<bool> SaveSessionDraftAsync()
    {
        if (SessionDraftPath is null)
        {
            return false;
        }

        var directory = Path.GetDirectoryName(SessionDraftPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Session drafts are internal UTF-8 snapshots; the document's chosen
        // encoding is restored when it is saved to a real file.
        await File.WriteAllTextAsync(SessionDraftPath, _document.Text, new UTF8Encoding(false))
            .ConfigureAwait(true);

        _savedEncoding = Encoding.Kind;
        _document.UndoStack.MarkAsOriginalFile();
        UpdateDirty();
        UpdateStats();
        return true;
    }

    public void SetSessionDraftPath(string? path) => SessionDraftPath = path;

    public async Task<bool> SaveAsync(string? targetPath = null)
    {
        var path = targetPath ?? FilePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        path = Path.GetFullPath(path);
        var languageMayChange = FilePath is null || !string.Equals(Path.GetExtension(FilePath), Path.GetExtension(path), StringComparison.OrdinalIgnoreCase);

        await _fileService.SaveAsync(path, _document.Text, Encoding.Kind, LineEnding.Value).ConfigureAwait(true);

        FilePath = path;
        SessionDraftPath = null;
        _savedEncoding = Encoding.Kind;
        Title = Path.GetFileName(path);
        _document.FileName = path;
        _document.UndoStack.MarkAsOriginalFile();
        UpdateDirty();
        UpdateStats();

        if (languageMayChange)
        {
            Language = _languageService.Detect(path);
        }

        OnPropertyChanged(nameof(IsUntitled));
        OnPropertyChanged(nameof(HasFilePath));
        OnPropertyChanged(nameof(ExtensionBadge));
        return true;
    }

    public void UpdateCaret(int line, int column, int selectionLength)
    {
        CaretLine = Math.Max(1, line);
        CaretColumn = Math.Max(1, column);
        SelectionLength = Math.Max(0, selectionLength);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        EditorSettings.Changed -= OnEditorSettingsChanged;
        DetachDocument(_document);
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Activate() => ActivateRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void RequestAction(string? action)
    {
        if (!string.IsNullOrWhiteSpace(action))
        {
            ActionRequested?.Invoke(this, new DocumentActionEventArgs(action));
        }
    }

    public void SetFilePath(string path)
    {
        FilePath = Path.GetFullPath(path);
        SessionDraftPath = null;
        Title = Path.GetFileName(FilePath);
        _document.FileName = FilePath;
        Language = _languageService.Detect(FilePath);
        OnPropertyChanged(nameof(IsUntitled));
        OnPropertyChanged(nameof(HasFilePath));
        OnPropertyChanged(nameof(ExtensionBadge));
        OnPropertyChanged(nameof(FilePath));
    }

    public void ApplySessionState(int caretOffset, bool isPinned, string? tabColor, bool isReadOnly)
    {
        SavedCaretOffset = Math.Clamp(caretOffset, 0, _document.TextLength);
        IsPinned = isPinned;
        TabColorHex = tabColor;
        IsReadOnly = isReadOnly;
    }

    partial void OnEncodingChanged(EncodingOption value)
    {
        if (!_isLoading)
        {
            UpdateDirty();
        }
    }

    partial void OnLineEndingChanged(LineEndingOption value)
    {
        if (_isLoading || value is null)
        {
            return;
        }

        var normalized = LineEndingCatalog.Normalize(_document.Text, value.Value);
        if (!string.Equals(normalized, _document.Text, StringComparison.Ordinal))
        {
            _document.UndoStack.StartUndoGroup();
            try
            {
                _document.Replace(0, _document.TextLength, normalized);
            }
            finally
            {
                _document.UndoStack.EndUndoGroup();
            }

            UpdateStats();
        }
    }

    partial void OnIsPinnedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanClose));
        OnPropertyChanged(nameof(PinMenuHeader));
    }

    partial void OnIsReadOnlyChanged(bool value) =>
        OnPropertyChanged(nameof(ReadOnlyMenuHeader));

    partial void OnTabColorHexChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _tabColorBrush = null;
        }
        else
        {
            try
            {
                _tabColorBrush = new SolidColorBrush(Color.Parse(value));
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                _tabColorBrush = null;
            }
        }

        OnPropertyChanged(nameof(TabColorBrush));
        OnPropertyChanged(nameof(HasTabColor));
    }

    partial void OnLanguageChanged(LanguageInfo value)
    {
        UpdateLanguageBrushes();
        OnPropertyChanged(nameof(LanguageScope));
        OnPropertyChanged(nameof(CommentPrefix));
    }

    private void UpdateLanguageBrushes()
    {
        var color = GetLanguageColor(Language.Id);
        _languageAccentBrush = new SolidColorBrush(color);
        _languageSoftBrush = new SolidColorBrush(Color.FromArgb(0x28, color.R, color.G, color.B));
        OnPropertyChanged(nameof(LanguageAccentBrush));
        OnPropertyChanged(nameof(LanguageSoftBrush));
    }

    private static Color GetLanguageColor(string? languageId) => languageId switch
    {
        "csharp" => Color.Parse("#B180F0"),
        "javascript" => Color.Parse("#E8D45A"),
        "typescript" => Color.Parse("#4A9EDB"),
        "python" => Color.Parse("#5A9FD4"),
        "html" => Color.Parse("#E06C4F"),
        "css" or "scss" or "less" => Color.Parse("#4A8FDF"),
        "json" => Color.Parse("#A8C97F"),
        "markdown" => Color.Parse("#6FA8DC"),
        "xml" => Color.Parse("#E09A55"),
        "yaml" => Color.Parse("#D06A6A"),
        "shellscript" => Color.Parse("#89E051"),
        "sql" => Color.Parse("#E0A44A"),
        "cpp" or "c" => Color.Parse("#7CA9E8"),
        "java" => Color.Parse("#C08A5A"),
        "go" => Color.Parse("#54C5D0"),
        "rust" => Color.Parse("#D9A07A"),
        "php" => Color.Parse("#8B9BE8"),
        "ruby" => Color.Parse("#E06060"),
        _ => Color.Parse("#8A93A0")
    };

    partial void OnZoomChanged(double value)
    {
        OnPropertyChanged(nameof(EffectiveFontSize));
        OnPropertyChanged(nameof(ZoomPercent));
    }

    private void ApplyLoaded(string path, LoadedFile loaded, bool resetCaret)
    {
        _isLoading = true;
        try
        {
            FilePath = Path.GetFullPath(path);
            SessionDraftPath = null;
            Title = Path.GetFileName(FilePath);
            _savedEncoding = loaded.Encoding;

            Encoding = EncodingCatalog.Get(loaded.Encoding);
            LineEnding = LineEndingCatalog.Get(loaded.LineEnding);

            var document = new TextDocument(loaded.Text) { FileName = FilePath };
            document.UndoStack.ClearAll();
            document.UndoStack.MarkAsOriginalFile();
            SetDocument(document);

            Language = _languageService.Detect(FilePath);
            IsDirty = false;

            if (resetCaret)
            {
                SavedCaretOffset = 0;
            }

            UpdateStats();
            OnPropertyChanged(nameof(IsUntitled));
            OnPropertyChanged(nameof(HasFilePath));
            OnPropertyChanged(nameof(ExtensionBadge));
            OnPropertyChanged(nameof(FilePath));
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void SetDocument(TextDocument document)
    {
        DetachDocument(_document);
        _document = document;
        AttachDocument(_document);
        OnPropertyChanged(nameof(Document));
    }

    private void AttachDocument(TextDocument document)
    {
        document.Changed += OnDocumentChanged;
        document.UndoStack.PropertyChanged += OnUndoStackPropertyChanged;
    }

    private void DetachDocument(TextDocument document)
    {
        document.Changed -= OnDocumentChanged;
        document.UndoStack.PropertyChanged -= OnUndoStackPropertyChanged;
    }

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        UpdateStats();
        UpdateDirty();
    }

    private void OnUndoStackPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UndoStack.IsOriginalFile))
        {
            UpdateDirty();
        }
    }

    private void UpdateStats()
    {
        OnPropertyChanged(nameof(Length));
        OnPropertyChanged(nameof(LineCount));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private void UpdateDirty()
    {
        var dirty = !_document.UndoStack.IsOriginalFile || Encoding.Kind != _savedEncoding;
        if (IsDirty != dirty)
        {
            IsDirty = dirty;
        }
    }

    private void OnEditorSettingsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(EffectiveFontSize));
    }
}


public sealed class DocumentActionEventArgs(string action) : EventArgs
{
    public string Action { get; } = action;
}
