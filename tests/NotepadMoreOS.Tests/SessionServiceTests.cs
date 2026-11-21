using NotepadMoreOS.Models;
using NotepadMoreOS.Services;

namespace NotepadMoreOS.Tests;

public sealed class SessionServiceTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsMetadataAndContent()
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var service = new SessionService(directory);
            var snapshot = new RecoverySnapshot
            {
                ActiveIndex = 1,
                Documents =
                [
                    new RecoveryDocument
                    {
                        Id = "first",
                        Title = "new 1",
                        Content = "unsaved work",
                        CaretOffset = 4,
                        Encoding = TextEncodingKind.Utf16Le,
                        LineEnding = LineEnding.CrLf,
                        LanguageId = "csharp",
                        IsDirty = true
                    },
                    new RecoveryDocument
                    {
                        Id = "second",
                        Path = "/tmp/clean.txt",
                        Title = "clean.txt",
                        IsDirty = false
                    }
                ]
            };

            await service.SaveAsync(snapshot);

            Assert.True(service.HasRecovery);
            Assert.True(File.Exists(Path.Combine(directory, "first.txt")));
            Assert.False(File.Exists(Path.Combine(directory, "second.txt")));

            var loaded = await service.LoadAsync();

            Assert.NotNull(loaded);
            Assert.Equal(1, loaded!.ActiveIndex);
            Assert.Equal(2, loaded.Documents.Count);
            Assert.Equal("unsaved work", loaded.Documents[0].Content);
            Assert.Equal("first.txt", loaded.Documents[0].BackupFile);
            Assert.Null(loaded.Documents[1].Content);
            Assert.Equal(4, loaded.Documents[0].CaretOffset);
            Assert.Equal(TextEncodingKind.Utf16Le, loaded.Documents[0].Encoding);
            Assert.Equal(LineEnding.CrLf, loaded.Documents[0].LineEnding);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Save_RemovesBackupsForDocumentsThatBecameClean()
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var service = new SessionService(directory);
            await service.SaveAsync(new RecoverySnapshot
            {
                Documents =
                [
                    new RecoveryDocument { Id = "a", Content = "dirty", IsDirty = true }
                ]
            });

            Assert.True(File.Exists(Path.Combine(directory, "a.txt")));

            await service.SaveAsync(new RecoverySnapshot
            {
                Documents =
                [
                    new RecoveryDocument { Id = "a", Path = "/tmp/a.txt" }
                ]
            });

            Assert.False(File.Exists(Path.Combine(directory, "a.txt")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Clear_RemovesStateAndContentFiles()
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var service = new SessionService(directory);
            await service.SaveAsync(new RecoverySnapshot
            {
                Documents = [new RecoveryDocument { Id = "a", Content = "x" }]
            });

            await service.ClearAsync();

            Assert.False(service.HasRecovery);
            Assert.Empty(Directory.EnumerateFiles(directory, "*.txt"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Load_CorruptState_ReturnsNull()
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "recovery.json"), "{ not json");
            var service = new SessionService(directory);

            Assert.Null(await service.LoadAsync());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
