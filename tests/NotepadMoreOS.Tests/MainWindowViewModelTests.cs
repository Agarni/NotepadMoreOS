using NotepadMoreOS.Models;
using NotepadMoreOS.Services;
using NotepadMoreOS.ViewModels;

namespace NotepadMoreOS.Tests;

public sealed class MainWindowViewModelTests
{
    private static MainWindowViewModel CreateViewModel(
        SettingsService settingsService,
        AppSettings settings,
        SessionService? sessionService = null,
        FileWatchService? fileWatcher = null,
        ThemeService? themeService = null)
    {
        var settingsDirectory = Path.GetDirectoryName(settingsService.SettingsFile) ?? Path.GetTempPath();
        return new MainWindowViewModel(
            settingsService,
            TestEnvironment.FileService,
            TestEnvironment.LanguageService,
            themeService ?? TestEnvironment.ThemeService,
            new SearchService(),
            new DialogService(),
            sessionService ?? new SessionService(Path.Combine(settingsDirectory, "recovery")),
            fileWatcher ?? new FileWatchService(),
            settings);
    }

    [Fact]
    public void NewDocument_BecomesTheActiveDocument()
    {
        var settings = new AppSettings();
        using var viewModel = CreateViewModel(new SettingsService(Path.Combine(Path.GetTempPath(), "unused.json")), settings);

        viewModel.NewDocumentCommand.Execute(null);

        Assert.Single(viewModel.Documents);
        Assert.Same(viewModel.Documents[0], viewModel.ActiveDocument);
        Assert.True(viewModel.HasDocuments);
        Assert.False(viewModel.IsWelcomeVisible);
        Assert.Equal("new 1 - NotepadMoreOS", viewModel.WindowTitle);
    }

    [Fact]
    public void SetAccentColor_UpdatesSettingsAndThemeService()
    {
        var settings = new AppSettings();
        using var viewModel = CreateViewModel(
            new SettingsService(Path.Combine(Path.GetTempPath(), "unused.json")),
            settings,
            themeService: new ThemeService());

        viewModel.SetAccentColorCommand.Execute("#4c8dff");

        Assert.Equal("#4C8DFF", settings.AccentColor);
        Assert.Equal("#4C8DFF", viewModel.ThemeService.AccentColor);
    }

    [Fact]
    public void SetAccentColor_IgnoresInvalidInput()
    {
        var settings = new AppSettings();
        using var viewModel = CreateViewModel(
            new SettingsService(Path.Combine(Path.GetTempPath(), "unused.json")),
            settings,
            themeService: new ThemeService());

        viewModel.SetAccentColorCommand.Execute("not-a-color");

        Assert.Equal(ThemeService.DefaultAccentColor, settings.AccentColor);
        Assert.Equal(ThemeService.DefaultAccentColor, viewModel.ThemeService.AccentColor);
    }

    [Fact]
    public void TabStylePreview_OverridesAndRestoresTheSavedStyle()
    {
        var settings = new AppSettings { TabStyle = TabStyles.Rounded };
        using var viewModel = CreateViewModel(
            new SettingsService(Path.Combine(Path.GetTempPath(), "unused.json")),
            settings);

        Assert.True(viewModel.TabsRounded);
        Assert.False(viewModel.TabsFloating);

        viewModel.PreviewTabStyle(TabStyles.Floating);

        Assert.False(viewModel.TabsRounded);
        Assert.True(viewModel.TabsFloating);
        Assert.Equal(TabStyles.Floating, viewModel.EffectiveTabStyle);

        viewModel.PreviewTabStyle(null);

        Assert.True(viewModel.TabsRounded);
        Assert.Equal(TabStyles.Rounded, viewModel.EffectiveTabStyle);
    }

    [Fact]
    public void OpenPaths_AddsRecentAndAvoidsDuplicates() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var first = Path.Combine(directory, "a.txt");
            var second = Path.Combine(directory, "b.txt");
            await File.WriteAllTextAsync(first, "a");
            await File.WriteAllTextAsync(second, "b");

            var settingsPath = Path.Combine(directory, "settings.json");
            var settings = new AppSettings();
            using var viewModel = CreateViewModel(new SettingsService(settingsPath), settings);

            await viewModel.OpenPathsAsync([first, second]);
            await viewModel.OpenPathsAsync([first]);

            Assert.Equal(2, viewModel.Documents.Count);
            Assert.Equal(first, viewModel.ActiveDocument?.FilePath);
            Assert.Equal(2, viewModel.RecentFiles.Count);
            Assert.Equal(first, viewModel.RecentFiles[0].Path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void RequestClose_SavesSession_AndInitializeRestoresIt() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var file = Path.Combine(directory, "restored.txt");
            await File.WriteAllTextAsync(file, "restored content");

            var settingsPath = Path.Combine(directory, "settings.json");
            var settingsService = new SettingsService(settingsPath);
            var settings = settingsService.Load();

            var firstViewModel = CreateViewModel(settingsService, settings);
            await firstViewModel.OpenPathsAsync([file]);
            firstViewModel.ActiveDocument!.SavedCaretOffset = 4;

            Assert.True(await firstViewModel.RequestCloseApplicationAsync());
            Assert.True(File.Exists(settingsPath));
            firstViewModel.Dispose();

            var restoredSettings = new SettingsService(settingsPath).Load();
            Assert.Single(restoredSettings.Session.Documents);
            Assert.Equal(file, restoredSettings.Session.Documents[0].Path);

            using var secondViewModel = CreateViewModel(new SettingsService(settingsPath), restoredSettings);
            await secondViewModel.InitializeAsync([]);

            Assert.Single(secondViewModel.Documents);
            Assert.Equal(file, secondViewModel.ActiveDocument?.FilePath);
            Assert.Equal(4, secondViewModel.ActiveDocument?.SavedCaretOffset);
            Assert.False(secondViewModel.ActiveDocument?.IsDirty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void StatusProxies_TrackAndUpdateActiveDocument()
    {
        var settings = new AppSettings();
        using var viewModel = CreateViewModel(new SettingsService(Path.Combine(Path.GetTempPath(), "unused.json")), settings);

        viewModel.NewDocumentCommand.Execute(null);
        var document = viewModel.ActiveDocument!;
        document.UpdateCaret(12, 5, 3);

        Assert.Equal(12, viewModel.StatusCaretLine);
        Assert.Equal(5, viewModel.StatusCaretColumn);
        Assert.Equal(3, viewModel.StatusSelectionLength);
        Assert.Equal("100%", viewModel.StatusZoom);

        viewModel.StatusEncoding = EncodingCatalog.Get(TextEncodingKind.Utf16Le);
        viewModel.StatusLineEnding = LineEndingCatalog.Get(LineEnding.CrLf);
        viewModel.StatusLanguage = TestEnvironment.LanguageService.FindById("python");

        Assert.Equal(TextEncodingKind.Utf16Le, document.Encoding.Kind);
        Assert.Equal(LineEnding.CrLf, document.LineEnding.Value);
        Assert.Equal("python", document.Language.Id);
    }

    [Fact]
    public void OpenFileSearchResult_OpensFileAndSelectsMatch() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var file = Path.Combine(directory, "result.txt");
            await File.WriteAllTextAsync(file, "first\nneedle here\n");
            var offset = (await File.ReadAllTextAsync(file)).IndexOf("needle", StringComparison.Ordinal);

            using var viewModel = CreateViewModel(
                new SettingsService(Path.Combine(directory, "settings.json")),
                new AppSettings());

            var result = new FileSearchResult(file, offset, 6, 2, 1, "needle here");
            await viewModel.OpenFileSearchResultAsync(result);

            var document = viewModel.ActiveDocument!;
            Assert.Equal(file, document.FilePath);
            Assert.Equal(offset, document.SavedCaretOffset);
            Assert.Equal(6, document.PendingSelectionLength);

            var host = new TestEditorHost(document.Document);
            document.Host = host;

            await viewModel.OpenFileSearchResultAsync(result);

            Assert.Equal(offset, host.SelectionStart);
            Assert.Equal(6, host.SelectionLength);
            Assert.Equal(2, host.LastScrollLine);
            Assert.Equal(0, document.PendingSelectionLength);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void RecoverySnapshot_RestoresAnUntitledDocument() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var settingsPath = Path.Combine(directory, "settings.json");
            var recoveryDirectory = Path.Combine(directory, "recovery");
            var sessionService = new SessionService(recoveryDirectory);

            var first = CreateViewModel(
                new SettingsService(settingsPath),
                new AppSettings(),
                sessionService);

            first.NewDocumentCommand.Execute(null);
            var original = first.ActiveDocument!;
            original.Document.Insert(0, "unsaved work");
            original.SavedCaretOffset = 4;

            await first.SaveRecoverySnapshotAsync();
            Assert.True(sessionService.HasRecovery);

            first.Dispose();

            var restoredSettings = new SettingsService(settingsPath).Load();
            using var second = CreateViewModel(
                new SettingsService(settingsPath),
                restoredSettings,
                sessionService);

            await second.InitializeAsync([]);

            Assert.Single(second.Documents);
            Assert.Equal("unsaved work", second.ActiveDocument!.Document.Text);
            Assert.True(second.ActiveDocument.IsDirty);
            Assert.True(second.ActiveDocument.IsUntitled);
            Assert.Equal(4, second.ActiveDocument.SavedCaretOffset);

            await sessionService.ClearAsync();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void ExternalChange_ReloadsCleanDocument() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var file = Path.Combine(directory, "external.txt");
            await File.WriteAllTextAsync(file, "before");
            using var viewModel = CreateViewModel(
                new SettingsService(Path.Combine(directory, "settings.json")),
                new AppSettings());

            await viewModel.OpenPathsAsync([file]);
            Assert.Equal("before", viewModel.ActiveDocument!.Document.Text);

            await File.WriteAllTextAsync(file, "after");
            await viewModel.HandleExternalChangeAsync(file);

            Assert.Equal("after", viewModel.ActiveDocument.Document.Text);
            Assert.Contains("external change", viewModel.StatusMessage);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void ExternalDelete_KeepsDocumentAndReportsStatus() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var file = Path.Combine(directory, "deleted.txt");
            await File.WriteAllTextAsync(file, "content");
            using var viewModel = CreateViewModel(
                new SettingsService(Path.Combine(directory, "settings.json")),
                new AppSettings());

            await viewModel.OpenPathsAsync([file]);
            File.Delete(file);

            await viewModel.HandleExternalChangeAsync(file);

            Assert.Equal("content", viewModel.ActiveDocument!.Document.Text);
            Assert.Contains("deleted", viewModel.StatusMessage);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void CloseWithDirtyFile_PreservesDraftWithoutWritingOriginal() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var file = Path.Combine(directory, "draft.txt");
            await File.WriteAllTextAsync(file, "original");
            var sessionService = new SessionService(Path.Combine(directory, "recovery"));
            var settingsPath = Path.Combine(directory, "settings.json");

            using var first = CreateViewModel(new SettingsService(settingsPath), new AppSettings(), sessionService);
            await first.OpenPathsAsync([file]);
            first.ActiveDocument!.Document.Insert(first.ActiveDocument.Document.TextLength, " modified");

            Assert.True(await first.RequestCloseApplicationAsync());
            Assert.True(sessionService.HasRecovery);
            Assert.Equal("original", await File.ReadAllTextAsync(file));

            var restoredSettings = new SettingsService(settingsPath).Load();
            using var second = CreateViewModel(
                new SettingsService(settingsPath),
                restoredSettings,
                sessionService);

            await second.InitializeAsync([]);

            Assert.Single(second.Documents);
            Assert.Equal("original modified", second.ActiveDocument!.Document.Text);
            Assert.True(second.ActiveDocument.IsDirty);
            Assert.Equal(file, second.ActiveDocument.FilePath);
            Assert.Equal("original", await File.ReadAllTextAsync(file));

            await sessionService.ClearAsync();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void CloseWithoutPreservation_ClearsRecoverySnapshot() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var file = Path.Combine(directory, "clean.txt");
            await File.WriteAllTextAsync(file, "saved");
            var sessionService = new SessionService(Path.Combine(directory, "recovery"));
            await sessionService.SaveAsync(new RecoverySnapshot
            {
                Documents = [new RecoveryDocument { Id = "legacy", Path = file }]
            });

            var settings = new AppSettings { RecoveryEnabled = false };
            using var viewModel = CreateViewModel(
                new SettingsService(Path.Combine(directory, "settings.json")),
                settings,
                sessionService);

            await viewModel.OpenPathsAsync([file]);
            Assert.True(await viewModel.RequestCloseApplicationAsync());
            Assert.False(sessionService.HasRecovery);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void CloseTab_RemovesCleanDocument_AndShowsWelcome()
    {
        var settings = new AppSettings();
        using var viewModel = CreateViewModel(new SettingsService(Path.Combine(Path.GetTempPath(), "unused.json")), settings);
        viewModel.NewDocumentCommand.Execute(null);

        viewModel.CloseTabCommand.Execute(viewModel.ActiveDocument);

        Assert.Empty(viewModel.Documents);
        Assert.Null(viewModel.ActiveDocument);
        Assert.True(viewModel.IsWelcomeVisible);
    }
}
