using NotepadMoreOS.Models;

namespace NotepadMoreOS.Tests;

public sealed class DocumentViewModelTests
{
    [Fact]
    public void NewDocument_IsUntitledAndClean()
    {
        using var document = TestEnvironment.CreateDocument();

        Assert.True(document.IsUntitled);
        Assert.False(document.IsDirty);
        Assert.Equal("new 1", document.Title);
        Assert.Equal(LanguageInfo.PlainText, document.Language);
        Assert.Equal(0, document.Length);
    }

    [Fact]
    public void Editing_MarksDirty_AndUndoRestoresCleanState()
    {
        using var document = TestEnvironment.CreateDocument();

        document.Document.Insert(0, "hello");
        Assert.True(document.IsDirty);

        document.Document.UndoStack.Undo();
        Assert.False(document.IsDirty);
        Assert.Equal(0, document.Length);
    }

    [Fact]
    public void SaveAsync_WritesFileAndClearsDirty() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "notes.txt");
            using var document = TestEnvironment.CreateDocument();
            document.Document.Insert(0, "conteúdo");

            var saved = await document.SaveAsync(path);

            Assert.True(saved);
            Assert.False(document.IsDirty);
            Assert.False(document.IsUntitled);
            Assert.Equal("notes.txt", document.Title);
            Assert.Equal("conteúdo", await File.ReadAllTextAsync(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void SaveAs_UpdatesLanguageAndBadge() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "Program.cs");
            using var document = TestEnvironment.CreateDocument();
            document.Document.Insert(0, "class Program { }");

            await document.SaveAsync(path);

            Assert.Equal("csharp", document.Language.Id);
            Assert.Equal("source.cs", document.LanguageScope);
            Assert.Equal("CS", document.ExtensionBadge);
            Assert.Equal("Program.cs", document.Title);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void LoadAsync_DetectsEncodingLineEndingsAndLanguage() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "sample.cs");
            await File.WriteAllBytesAsync(
                path,
                [0xEF, 0xBB, 0xBF, .. System.Text.Encoding.UTF8.GetBytes("class A\r\n{\r\n}\r\n")]);

            using var document = TestEnvironment.CreateDocument();
            await document.LoadAsync(path);

            Assert.Equal(TextEncodingKind.Utf8Bom, document.Encoding.Kind);
            Assert.Equal(LineEnding.CrLf, document.LineEnding.Value);
            Assert.Equal("csharp", document.Language.Id);
            Assert.False(document.IsDirty);
            Assert.Equal(4, document.LineCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void ChangingLineEnding_ConvertsTextAndMarksDirty() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "sample.txt");
            await File.WriteAllTextAsync(path, "a\r\nb\r\n");

            using var document = TestEnvironment.CreateDocument();
            await document.LoadAsync(path);

            document.LineEnding = LineEndingCatalog.Get(LineEnding.Lf);

            Assert.Equal("a\nb\n", document.Document.Text);
            Assert.True(document.IsDirty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void ChangingEncoding_MarksDirty_AndSavesWithChosenEncoding() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "latin.txt");
            using var document = TestEnvironment.CreateDocument();
            document.Document.Insert(0, "café ção");

            document.Encoding = EncodingCatalog.Get(TextEncodingKind.Windows1252);
            Assert.True(document.IsDirty);

            await document.SaveAsync(path);

            var bytes = await File.ReadAllBytesAsync(path);
            Assert.Equal("café ção", System.Text.Encoding.GetEncoding(1252).GetString(bytes));
            Assert.False(document.IsDirty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void ReloadAsync_ReplacesContentFromDisk() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "reload.txt");
            await File.WriteAllTextAsync(path, "original");

            using var document = TestEnvironment.CreateDocument();
            await document.LoadAsync(path);
            document.Document.Insert(document.Document.TextLength, " changed");

            await File.WriteAllTextAsync(path, "from disk");
            Assert.True(await document.ReloadAsync());

            Assert.Equal("from disk", document.Document.Text);
            Assert.False(document.IsDirty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    });

    [Fact]
    public void ReopenWithEncoding_ReinterpretsTextAndKeepsCaret() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "encoding.txt");
            await File.WriteAllBytesAsync(path, System.Text.Encoding.UTF8.GetBytes("olá"));

            using var document = TestEnvironment.CreateDocument();
            await document.LoadAsync(path);
            document.SavedCaretOffset = 1;

            Assert.True(await document.ReopenWithEncodingAsync(EncodingCatalog.Get(TextEncodingKind.Windows1252)));
            Assert.Equal("olÃ¡", document.Document.Text);
            Assert.Equal(TextEncodingKind.Windows1252, document.Encoding.Kind);
            Assert.Equal(1, document.SavedCaretOffset);
            Assert.False(document.IsDirty);

            Assert.True(await document.ReopenWithEncodingAsync(EncodingCatalog.Get(TextEncodingKind.Utf8)));
            Assert.Equal("olá", document.Document.Text);
            Assert.Equal(TextEncodingKind.Utf8, document.Encoding.Kind);
            Assert.False(document.IsDirty);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    });

    [Fact]
    public void RestoreRecovery_UsesBackedUpContentAndMetadata() => AsyncTest.Run(async () =>
    {
        using var document = TestEnvironment.CreateDocument();

        var restored = await document.RestoreRecoveryAsync(new RecoveryDocument
        {
            Id = "recovered",
            Title = "new 7",
            Content = "olá\nmundo",
            CaretOffset = 3,
            Encoding = TextEncodingKind.Utf16Le,
            LineEnding = LineEnding.CrLf,
            LanguageId = "csharp",
            IsDirty = true
        });

        Assert.True(restored);
        Assert.Equal("olá\nmundo", document.Document.Text);
        Assert.True(document.IsDirty);
        Assert.True(document.IsUntitled);
        Assert.Equal("new 7", document.Title);
        Assert.Equal(TextEncodingKind.Utf16Le, document.Encoding.Kind);
        Assert.Equal(LineEnding.CrLf, document.LineEnding.Value);
        Assert.Equal("csharp", document.Language.Id);
        Assert.Equal(3, document.SavedCaretOffset);
    });

    [Fact]
    public void RestoreRecovery_LoadsCleanFileFromDisk() => AsyncTest.Run(async () =>
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "clean.txt");
            await File.WriteAllTextAsync(path, "from disk");

            using var document = TestEnvironment.CreateDocument();
            var restored = await document.RestoreRecoveryAsync(new RecoveryDocument
            {
                Id = "clean",
                Path = path,
                Title = "clean.txt",
                CaretOffset = 4
            });

            Assert.True(restored);
            Assert.Equal("from disk", document.Document.Text);
            Assert.False(document.IsDirty);
            Assert.Equal(path, document.FilePath);
            Assert.Equal(4, document.SavedCaretOffset);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    });

    [Fact]
    public void ApplyPendingSelection_SelectsMatchAndScrollsToLine()
    {
        using var document = TestEnvironment.CreateDocument();
        document.Document.Text = "one\nneedle here\n";
        var host = new TestEditorHost(document.Document);
        document.Host = host;
        document.SavedCaretOffset = 4;
        document.PendingSelectionLength = 6;
        document.PendingSelectionLine = 2;

        document.ApplyPendingSelection(host);

        Assert.Equal(4, host.SelectionStart);
        Assert.Equal(6, host.SelectionLength);
        Assert.Equal(2, host.LastScrollLine);
        Assert.Equal(0, document.PendingSelectionLength);
    }

    [Fact]
    public void CommentPrefix_MatchesLanguageConventions()    {
        using var document = TestEnvironment.CreateDocument();

        document.Language = TestEnvironment.LanguageService.FindById("csharp");
        Assert.Equal("//", document.CommentPrefix);

        document.Language = TestEnvironment.LanguageService.FindById("python");
        Assert.Equal("#", document.CommentPrefix);

        document.Language = TestEnvironment.LanguageService.FindById("sql");
        Assert.Equal("--", document.CommentPrefix);

        document.Language = TestEnvironment.LanguageService.FindById("html");
        Assert.Null(document.CommentPrefix);
    }
}
