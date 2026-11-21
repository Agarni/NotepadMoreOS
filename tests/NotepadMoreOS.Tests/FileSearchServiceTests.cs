using System.Text;
using NotepadMoreOS.Models;
using NotepadMoreOS.Services;

namespace NotepadMoreOS.Tests;

public sealed class FileSearchServiceTests
{
    private readonly FileSearchService _service = new(new SearchService());

    private static FileSearchOptions Options(
        string folder,
        string pattern,
        string masks = "*.*",
        bool matchCase = false,
        bool wholeWord = false,
        bool useRegex = false,
        bool includeSubdirectories = true,
        bool skipHiddenAndBuildFolders = true) =>
        new(folder, pattern, masks, matchCase, wholeWord, useRegex, includeSubdirectories, skipHiddenAndBuildFolders);

    private static async Task<FileSearchSummary> SearchAsync(FileSearchService service, FileSearchOptions options)
    {
        return await service.SearchAsync(options, progress: null, CancellationToken.None);
    }

    [Fact]
    public async Task Search_FindsMatchesAcrossSubfolders_AndHonorsMasks()
    {
        var root = TestEnvironment.CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "sub"));
            Directory.CreateDirectory(Path.Combine(root, "node_modules"));
            await File.WriteAllTextAsync(Path.Combine(root, "a.cs"), "var a = 1;\nvar b = 2;\n");
            await File.WriteAllTextAsync(Path.Combine(root, "b.md"), "var markdown\n");
            await File.WriteAllTextAsync(Path.Combine(root, "sub", "c.cs"), "var c\n");
            await File.WriteAllTextAsync(Path.Combine(root, "node_modules", "ignored.cs"), "var ignored\n");

            var summary = await SearchAsync(_service, Options(root, "var", "*.cs"));

            Assert.Null(summary.Error);
            Assert.Equal(3, summary.Results.Count);
            Assert.Equal(2, summary.FilesMatched);
            Assert.DoesNotContain(summary.Results, result => result.FilePath.EndsWith("b.md"));
            Assert.DoesNotContain(summary.Results, result => result.FilePath.Contains("node_modules"));
            Assert.Equal((1, 1), (summary.Results[0].Line, summary.Results[0].Column));
            Assert.Equal((2, 1), (summary.Results[1].Line, summary.Results[1].Column));
            Assert.Equal("var a = 1;", summary.Results[0].Preview);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Search_SupportsMultipleMasks()
    {
        var root = TestEnvironment.CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "a.cs"), "needle\n");
            await File.WriteAllTextAsync(Path.Combine(root, "b.md"), "needle\n");
            await File.WriteAllTextAsync(Path.Combine(root, "c.txt"), "needle\n");

            var summary = await SearchAsync(_service, Options(root, "needle", "*.cs;*.md"));

            Assert.Equal(2, summary.Results.Count);
            Assert.Equal(2, summary.FilesMatched);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Search_RespectsCaseAndWholeWord()
    {
        var root = TestEnvironment.CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "Cat cat scatter\n");

            var caseSensitive = await SearchAsync(_service, Options(root, "Cat", "*.txt", matchCase: true));
            var wholeWord = await SearchAsync(_service, Options(root, "cat", "*.txt", wholeWord: true));

            Assert.Single(caseSensitive.Results);
            Assert.Equal(2, wholeWord.Results.Count);
            Assert.DoesNotContain(wholeWord.Results, result => result.Preview.Contains("scatter", StringComparison.Ordinal) && result.Offset > 8);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Search_SupportsRegularExpressions()
    {
        var root = TestEnvironment.CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "id=10\nid=200\n");

            var summary = await SearchAsync(_service, Options(root, @"id=(\d+)", "*.txt", useRegex: true));

            Assert.Equal(2, summary.Results.Count);
            Assert.All(summary.Results, result => Assert.Equal(1, result.Column));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Search_SkipsBinaryFiles()
    {
        var root = TestEnvironment.CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "text.txt"), "find me\n");
            await File.WriteAllBytesAsync(Path.Combine(root, "binary.bin"), [0x00, 0x01, 0x02, 0x66, 0x69, 0x6E, 0x64]);

            var summary = await SearchAsync(_service, Options(root, "find"));

            Assert.Single(summary.Results);
            Assert.Equal(1, summary.FilesSkipped);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Search_ReadsUtf16Files()
    {
        var root = TestEnvironment.CreateTempDirectory();
        try
        {
            var path = Path.Combine(root, "utf16.txt");
            await File.WriteAllBytesAsync(path, FileService.Encode("find me", TextEncodingKind.Utf16Le));

            var summary = await SearchAsync(_service, Options(root, "find", "*.txt"));

            Assert.Single(summary.Results);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Search_ReportsInvalidRegexAndMissingInputs()
    {
        var root = TestEnvironment.CreateTempDirectory();
        try
        {
            var invalid = await SearchAsync(_service, Options(root, "[", "*.txt", useRegex: true));
            var emptyPattern = await SearchAsync(_service, Options(root, "   "));
            var missingFolder = await SearchAsync(_service, Options(Path.Combine(root, "missing"), "x"));

            Assert.Equal("Invalid regular expression.", invalid.Error);
            Assert.Equal("Type something to find.", emptyPattern.Error);
            Assert.Equal("Choose an existing folder.", missingFolder.Error);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Search_ReturnsCancelledSummaryWhenTokenIsAlreadyCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var summary = await _service.SearchAsync(Options(".", "anything"), null, cancellation.Token);

        Assert.True(summary.Cancelled);
        Assert.Empty(summary.Results);
    }

    [Fact]
    public async Task Search_TruncatesAtTheResultLimit()
    {
        var root = TestEnvironment.CreateTempDirectory();
        try
        {
            var content = string.Join(' ', Enumerable.Repeat("match", FileSearchService.MaxResults + 10));
            await File.WriteAllTextAsync(Path.Combine(root, "big.txt"), content);

            var summary = await SearchAsync(_service, Options(root, "match", "*.txt"));

            Assert.True(summary.Truncated);
            Assert.Equal(FileSearchService.MaxResults, summary.Results.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Search_CanSkipSubdirectories()
    {
        var root = TestEnvironment.CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "sub"));
            await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "needle\n");
            await File.WriteAllTextAsync(Path.Combine(root, "sub", "b.txt"), "needle\n");

            var summary = await SearchAsync(_service, Options(root, "needle", "*.txt", includeSubdirectories: false));

            Assert.Single(summary.Results);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Search_ReportsLineAndColumnForCrLfFiles()
    {
        var root = TestEnvironment.CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "first\r\nsecond needle\r\n");

            var summary = await SearchAsync(_service, Options(root, "needle", "*.txt"));

            Assert.Single(summary.Results);
            Assert.Equal(2, summary.Results[0].Line);
            Assert.Equal(8, summary.Results[0].Column);
            Assert.Equal("second needle", summary.Results[0].Preview);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
