using NotepadMoreOS.Models;
using NotepadMoreOS.Services;
using NotepadMoreOS.ViewModels;

namespace NotepadMoreOS.Tests;

public sealed class SessionAndTabActionTests
{
    [Fact]
    public void NamedSession_RoundTripsFilesStateAndManagement() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var firstFile = Path.Combine(directory, "first.cs");
            var secondFile = Path.Combine(directory, "second.md");
            await File.WriteAllTextAsync(firstFile, "class First { }\n");
            await File.WriteAllTextAsync(secondFile, "# Second\n");

            var settingsPath = Path.Combine(directory, "settings.json");
            var settingsService = new SettingsService(settingsPath);
            var sessionService = new SessionService(Path.Combine(directory, "recovery"));
            var settings = new AppSettings();

            using var first = TestEnvironment.CreateViewModel(settingsService, settings, sessionService);
            await first.OpenPathsAsync([firstFile, secondFile]);

            var firstDocument = first.Documents.First(document => document.FilePath == firstFile);
            firstDocument.IsPinned = true;
            firstDocument.TabColorHex = "#E06C4F";
            firstDocument.IsReadOnly = true;
            firstDocument.SavedCaretOffset = 3;
            first.ActiveDocument = firstDocument;

            Assert.True(await first.SaveSessionWithNameAsync("Work", replaceExisting: false));
            Assert.Equal("Work", first.CurrentSessionName);
            Assert.Single(first.Settings.Sessions);

            var persisted = new SettingsService(settingsPath).Load();
            var session = Assert.Single(persisted.Sessions);
            Assert.Equal(2, session.Files.Count);
            var persistedFile = session.Files.First(file => file.Path == firstFile);
            Assert.True(persistedFile.IsPinned);
            Assert.Equal("#E06C4F", persistedFile.TabColor);
            Assert.True(persistedFile.IsReadOnly);
            Assert.Equal(3, persistedFile.CaretOffset);

            using var second = TestEnvironment.CreateViewModel(
                new SettingsService(settingsPath),
                persisted,
                sessionService);

            Assert.True(await second.OpenNamedSessionAsync(session));
            Assert.Equal(2, second.Documents.Count);
            Assert.Equal("Work", second.CurrentSessionName);

            var restored = second.Documents.First(document => document.FilePath == firstFile);
            Assert.True(restored.IsPinned);
            Assert.Equal("#E06C4F", restored.TabColorHex);
            Assert.True(restored.IsReadOnly);
            Assert.Equal(3, restored.SavedCaretOffset);

            Assert.True(await second.RenameSessionAsync("Work", "Renamed"));
            Assert.Equal("Renamed", second.CurrentSessionName);
            Assert.Equal("Renamed", second.Settings.Sessions.Single().Name);

            second.DeleteSession("Renamed");
            Assert.Empty(second.Settings.Sessions);
            Assert.Null(second.CurrentSessionName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void NamedSession_CleanFilesRestoreCleanAndKeepPath() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var file = Path.Combine(directory, "clean.txt");
            await File.WriteAllTextAsync(file, "clean");
            var settingsPath = Path.Combine(directory, "settings.json");
            using var first = TestEnvironment.CreateViewModel(new SettingsService(settingsPath), new AppSettings());
            await first.OpenPathsAsync([file]);
            Assert.False(first.ActiveDocument!.IsDirty);
            Assert.True(await first.SaveSessionWithNameAsync("Clean", replaceExisting: false));
            var saved = first.Settings.Sessions.Single();
            var entry = Assert.Single(saved.Files);
            Assert.Null(entry.DraftFile);
            Assert.False(entry.IsDirty);
            var persisted = new SettingsService(settingsPath).Load();
            using var second = TestEnvironment.CreateViewModel(new SettingsService(settingsPath), persisted);
            Assert.True(await second.OpenNamedSessionAsync(persisted.Sessions.Single()));
            var restored = Assert.Single(second.Documents);
            Assert.False(restored.IsDirty);
            Assert.Equal(file, restored.FilePath);
        }
        finally { Directory.Delete(directory, true); }
    });

    [Fact]
    public void EmptySession_ClearsWorkspaceAndPersists() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var settingsPath = Path.Combine(directory, "settings.json");
            using var viewModel = TestEnvironment.CreateViewModel(
                new SettingsService(settingsPath),
                new AppSettings());

            viewModel.NewDocumentCommand.Execute(null);
            Assert.Single(viewModel.Documents);

            Assert.True(await viewModel.CreateEmptySessionAsync("Clean slate"));
            Assert.Empty(viewModel.Documents);
            Assert.Equal("Clean slate", viewModel.CurrentSessionName);

            var persisted = new SettingsService(settingsPath).Load();
            var session = Assert.Single(persisted.Sessions);
            Assert.Empty(session.Files);
            Assert.Equal(-1, session.ActiveIndex);
            Assert.Equal("Clean slate", persisted.CurrentSessionName);

            Assert.False(await viewModel.CreateEmptySessionAsync("Clean slate"));
            Assert.Single(viewModel.Settings.Sessions);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void SwitchingSessions_PreservesUnsavedEditsWithoutPrompt() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var file = Path.Combine(directory, "work.txt");
            await File.WriteAllTextAsync(file, "original");

            var settingsPath = Path.Combine(directory, "settings.json");
            using var viewModel = TestEnvironment.CreateViewModel(
                new SettingsService(settingsPath),
                new AppSettings());

            await viewModel.OpenPathsAsync([file]);
            var edited = viewModel.ActiveDocument!;
            edited.Document.Text = "edited before switch";
            Assert.True(edited.IsDirty);

            Assert.True(await viewModel.SaveSessionWithNameAsync("Work", replaceExisting: false));
            Assert.True(await viewModel.CreateEmptySessionAsync("Empty"));
            Assert.Empty(viewModel.Documents);
            Assert.Equal("Empty", viewModel.CurrentSessionName);

            var work = viewModel.Settings.Sessions.Single(session => session.Name == "Work");
            Assert.True(await viewModel.OpenNamedSessionAsync(work));

            var restored = Assert.Single(viewModel.Documents);
            Assert.Equal("edited before switch", restored.Document.Text);
            Assert.Equal(file, restored.FilePath);
            Assert.True(restored.IsDirty);
            Assert.Equal("original", await File.ReadAllTextAsync(file));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void OpeningSession_WithoutNamedWorkspace_KeepsDirtyTabsInAutoSession() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var settings = new AppSettings();
            settings.Sessions.Add(new NamedSession
            {
                Name = "Target",
                Files = [],
                ActiveIndex = -1,
                SavedAtUtc = DateTime.UtcNow
            });

            using var viewModel = TestEnvironment.CreateViewModel(
                new SettingsService(Path.Combine(directory, "settings.json")),
                settings);

            viewModel.NewDocumentCommand.Execute(null);
            viewModel.ActiveDocument!.Document.Text = "scratch edit";
            Assert.True(viewModel.ActiveDocument.IsDirty);

            var target = settings.Sessions.Single(session => session.Name == "Target");
            Assert.True(await viewModel.OpenNamedSessionAsync(target));
            Assert.Empty(viewModel.Documents);

            var autoSession = viewModel.Settings.Sessions.Single(session => session.Name == "Unsaved workspace");
            Assert.True(await viewModel.OpenNamedSessionAsync(autoSession));
            var restored = Assert.Single(viewModel.Documents);
            Assert.Equal("scratch edit", restored.Document.Text);
            Assert.False(restored.IsDirty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void NamedSession_PreservesUnsavedAndUntitledTabs() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var file = Path.Combine(directory, "original.txt");
            await File.WriteAllTextAsync(file, "original");

            var settingsPath = Path.Combine(directory, "settings.json");
            using var first = TestEnvironment.CreateViewModel(
                new SettingsService(settingsPath),
                new AppSettings());

            await first.OpenPathsAsync([file]);
            var savedDocument = first.ActiveDocument!;
            savedDocument.Document.Text = "unsaved edit";

            first.NewDocumentCommand.Execute(null);
            var untitled = first.ActiveDocument!;
            untitled.Title = "Unsaved note";
            untitled.Document.Text = "draft text";

            Assert.True(await first.SaveSessionWithNameAsync("Drafts", replaceExisting: false));

            var session = Assert.Single(first.Settings.Sessions);
            Assert.Equal(2, session.Files.Count);
            Assert.All(session.Files, draft => Assert.False(string.IsNullOrWhiteSpace(draft.DraftFile)));

            var draftsDirectory = Path.Combine(directory, "session-drafts", session.Id);
            Assert.Equal(2, Directory.GetFiles(draftsDirectory, "*.txt").Length);

            var settingsJson = await File.ReadAllTextAsync(settingsPath);
            Assert.DoesNotContain("unsaved edit", settingsJson, StringComparison.Ordinal);
            Assert.DoesNotContain("draft text", settingsJson, StringComparison.Ordinal);
            Assert.Equal("original", await File.ReadAllTextAsync(file));

            var persisted = new SettingsService(settingsPath).Load();
            using var second = TestEnvironment.CreateViewModel(
                new SettingsService(settingsPath),
                persisted);

            Assert.True(await second.OpenNamedSessionAsync(Assert.Single(persisted.Sessions)));
            Assert.Equal(2, second.Documents.Count);

            var restoredSaved = second.Documents.Single(document => document.FilePath == file);
            Assert.Equal("unsaved edit", restoredSaved.Document.Text);
            Assert.True(restoredSaved.IsDirty);

            var restoredUntitled = second.Documents.Single(document => document.FilePath is null);
            Assert.Equal("draft text", restoredUntitled.Document.Text);
            Assert.Equal("Unsaved note", restoredUntitled.Title);
            Assert.False(restoredUntitled.IsDirty);
            Assert.False(string.IsNullOrWhiteSpace(restoredUntitled.SessionDraftPath));

            // Saving a restored, untouched draft must not open the Save As dialog.
            Assert.True(await second.SaveDocumentAsync(restoredUntitled));
            Assert.False(restoredUntitled.IsDirty);

            restoredUntitled.Document.Text = "updated draft";
            Assert.True(restoredUntitled.IsDirty);
            Assert.True(await second.SaveDocumentAsync(restoredUntitled));
            Assert.False(restoredUntitled.IsDirty);
            Assert.Null(restoredUntitled.FilePath);
            Assert.Equal("updated draft", await File.ReadAllTextAsync(restoredUntitled.SessionDraftPath!));

            var persistedAgain = new SettingsService(settingsPath).Load();
            using var third = TestEnvironment.CreateViewModel(
                new SettingsService(settingsPath),
                persistedAgain);
            Assert.True(await third.OpenNamedSessionAsync(Assert.Single(persistedAgain.Sessions)));
            var restoredAgain = third.Documents.Single(document => document.FilePath is null);
            Assert.Equal("updated draft", restoredAgain.Document.Text);
            Assert.False(restoredAgain.IsDirty);

            second.DeleteSession("Drafts");
            Assert.False(Directory.Exists(draftsDirectory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void MultipleSessions_KeepIndependentUntitledDrafts() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var settingsPath = Path.Combine(directory, "settings.json");
            using var first = TestEnvironment.CreateViewModel(
                new SettingsService(settingsPath),
                new AppSettings());

            first.NewDocumentCommand.Execute(null);
            var draftDocument = first.ActiveDocument!;
            draftDocument.Document.Text = "first session draft";

            Assert.True(await first.SaveSessionWithNameAsync("One", replaceExisting: false));
            var firstDraftPath = draftDocument.SessionDraftPath;
            Assert.False(string.IsNullOrWhiteSpace(firstDraftPath));

            draftDocument.Document.Text = "second session draft";
            Assert.True(await first.SaveSessionWithNameAsync("Two", replaceExisting: false));
            var secondDraftPath = draftDocument.SessionDraftPath;
            Assert.False(string.IsNullOrWhiteSpace(secondDraftPath));
            Assert.NotEqual(firstDraftPath, secondDraftPath);
            Assert.Equal("first session draft", await File.ReadAllTextAsync(firstDraftPath!));
            Assert.Equal("second session draft", await File.ReadAllTextAsync(secondDraftPath!));

            var persisted = new SettingsService(settingsPath).Load();
            Assert.Equal(2, persisted.Sessions.Count);

            using var restore = TestEnvironment.CreateViewModel(
                new SettingsService(settingsPath),
                persisted);

            Assert.True(await restore.OpenNamedSessionAsync(
                persisted.Sessions.Single(session => session.Name == "One")));
            var restoredFirst = Assert.Single(restore.Documents);
            Assert.Equal("first session draft", restoredFirst.Document.Text);
            Assert.False(restoredFirst.IsDirty);

            Assert.True(await restore.OpenNamedSessionAsync(
                persisted.Sessions.Single(session => session.Name == "Two")));
            var restoredSecond = Assert.Single(restore.Documents);
            Assert.Equal("second session draft", restoredSecond.Document.Text);
            Assert.False(restoredSecond.IsDirty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void NamedSession_CanContainOnlyUntitledTabs() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var settingsPath = Path.Combine(directory, "settings.json");
            using var first = TestEnvironment.CreateViewModel(
                new SettingsService(settingsPath),
                new AppSettings());

            first.NewDocumentCommand.Execute(null);
            var untitled = first.ActiveDocument!;
            untitled.Encoding = EncodingCatalog.Get(TextEncodingKind.Utf16Le);
            untitled.Document.Text = "only untitled draft";

            Assert.True(await first.SaveSessionWithNameAsync("Only drafts", replaceExisting: false));
            var session = Assert.Single(first.Settings.Sessions);
            var file = Assert.Single(session.Files);
            Assert.Equal(string.Empty, file.Path);
            Assert.False(string.IsNullOrWhiteSpace(file.DraftFile));

            var persisted = new SettingsService(settingsPath).Load();
            using var second = TestEnvironment.CreateViewModel(
                new SettingsService(settingsPath),
                persisted);

            Assert.True(await second.OpenNamedSessionAsync(Assert.Single(persisted.Sessions)));
            var restored = Assert.Single(second.Documents);
            Assert.Null(restored.FilePath);
            Assert.Equal("only untitled draft", restored.Document.Text);
            Assert.Equal(TextEncodingKind.Utf16Le, restored.Encoding.Kind);
            Assert.False(restored.IsDirty);

            restored.Document.Text = "updated ünïcode draft";
            Assert.True(await second.SaveDocumentAsync(restored));
            Assert.False(restored.IsDirty);

            var persistedAgain = new SettingsService(settingsPath).Load();
            using var third = TestEnvironment.CreateViewModel(
                new SettingsService(settingsPath),
                persistedAgain);
            Assert.True(await third.OpenNamedSessionAsync(Assert.Single(persistedAgain.Sessions)));
            var restoredAgain = Assert.Single(third.Documents);
            Assert.Equal("updated ünïcode draft", restoredAgain.Document.Text);
            Assert.Equal(TextEncodingKind.Utf16Le, restoredAgain.Encoding.Kind);
            Assert.False(restoredAgain.IsDirty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void TabActions_PinColorReadOnlyMoveAndCloseOthers() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            using var viewModel = TestEnvironment.CreateViewModel(
                new SettingsService(Path.Combine(directory, "settings.json")),
                new AppSettings());

            viewModel.NewDocumentCommand.Execute(null);
            viewModel.NewDocumentCommand.Execute(null);
            viewModel.NewDocumentCommand.Execute(null);

            var first = viewModel.Documents[0];
            var second = viewModel.Documents[1];

            viewModel.TogglePin(first);
            Assert.True(first.IsPinned);
            Assert.Same(first, viewModel.Documents[0]);

            await viewModel.HandleDocumentActionAsync(first, "color:#B180F0");
            Assert.Equal("#B180F0", first.TabColorHex);
            Assert.True(first.HasTabColor);

            await viewModel.HandleDocumentActionAsync(first, "readonly");
            Assert.True(first.IsReadOnly);

            viewModel.MoveTab(second, -1);
            Assert.Same(second, viewModel.Documents[0]);

            await viewModel.HandleDocumentActionAsync(second, "close-others");
            Assert.Single(viewModel.Documents);
            Assert.Same(second, viewModel.ActiveDocument);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });
}
