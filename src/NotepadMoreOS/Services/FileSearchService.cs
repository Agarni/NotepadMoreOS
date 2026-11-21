using System.IO.Enumeration;
using System.Text.RegularExpressions;
using NotepadMoreOS.Models;

namespace NotepadMoreOS.Services;

/// <summary>Searches text files inside a folder, with masks, encoding detection and cancellation.</summary>
public sealed class FileSearchService
{
    public const int MaxResults = 5_000;
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> BuildDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bin", "obj", "build", "dist", "out", "coverage", "target",
        ".git", ".svn", ".hg", ".vs", ".idea", ".vscode", ".venv", "venv",
        "__pycache__", ".pytest_cache", ".cache", ".next", ".nuxt"
    };

    private readonly SearchService _searchService;

    public FileSearchService(SearchService searchService)
    {
        _searchService = searchService;
    }

    public Task<FileSearchSummary> SearchAsync(
        FileSearchOptions options,
        IProgress<FileSearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(new FileSearchSummary([], 0, 0, 0, false, true, null));
        }

        return Task.Run(() => SearchCore(options, progress, cancellationToken), cancellationToken);
    }

    private FileSearchSummary SearchCore(
        FileSearchOptions options,
        IProgress<FileSearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Pattern))
        {
            return Error("Type something to find.");
        }

        if (string.IsNullOrWhiteSpace(options.Folder) || !Directory.Exists(options.Folder))
        {
            return Error("Choose an existing folder.");
        }

        var regex = _searchService.CreateMatcher(options.ToSearchOptions());
        if (regex is null)
        {
            return Error("Invalid regular expression.");
        }

        var masks = ParseMasks(options.FileMasks);
        var results = new List<FileSearchResult>();
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(Path.GetFullPath(options.Folder));

        var filesScanned = 0;
        var filesMatched = 0;
        var filesSkipped = 0;
        var cancelled = false;
        var truncated = false;

        while (pendingDirectories.Count > 0)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            var directory = pendingDirectories.Pop();

            try
            {
                foreach (var path in Directory.EnumerateFiles(directory))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        cancelled = true;
                        break;
                    }

                    if (!MatchesMasks(path, masks))
                    {
                        continue;
                    }

                    filesScanned++;

                    if (filesScanned % 25 == 0)
                    {
                        progress?.Report(new FileSearchProgress(filesScanned, filesMatched, results.Count, path));
                    }

                    try
                    {
                        var fileInfo = new FileInfo(path);
                        if (fileInfo.Length > MaxFileSizeBytes)
                        {
                            filesSkipped++;
                            continue;
                        }

                        var bytes = File.ReadAllBytes(path);
                        if (FileService.IsProbablyBinary(bytes))
                        {
                            filesSkipped++;
                            continue;
                        }

                        var (text, _) = FileService.Decode(bytes);
                        var remaining = MaxResults - results.Count;
                        var fileMatches = FindMatches(path, text, regex, remaining, out var fileTruncated);

                        if (fileMatches.Count > 0)
                        {
                            filesMatched++;
                            results.AddRange(fileMatches);
                        }

                        if (fileTruncated)
                        {
                            truncated = true;
                            break;
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or RegexMatchTimeoutException)
                    {
                        filesSkipped++;
                    }

                    if (results.Count >= MaxResults)
                    {
                        truncated = true;
                        break;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A single unreadable folder must not abort the whole search.
            }

            if (cancelled || truncated)
            {
                break;
            }

            if (!options.IncludeSubdirectories)
            {
                continue;
            }

            try
            {
                foreach (var subdirectory in Directory.EnumerateDirectories(directory))
                {
                    var name = Path.GetFileName(subdirectory);

                    if (options.SkipHiddenAndBuildFolders &&
                        (name.StartsWith('.') || BuildDirectoryNames.Contains(name)))
                    {
                        continue;
                    }

                    try
                    {
                        if ((File.GetAttributes(subdirectory) & FileAttributes.ReparsePoint) != 0)
                        {
                            continue;
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        continue;
                    }

                    pendingDirectories.Push(subdirectory);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Keep searching the rest of the tree.
            }
        }

        results.Sort(static (left, right) =>
        {
            var comparison = string.Compare(left.FilePath, right.FilePath, StringComparison.OrdinalIgnoreCase);
            return comparison != 0 ? comparison : left.Offset.CompareTo(right.Offset);
        });

        progress?.Report(new FileSearchProgress(filesScanned, filesMatched, results.Count, string.Empty));

        return new FileSearchSummary(
            results,
            filesScanned,
            filesMatched,
            filesSkipped,
            truncated,
            cancelled,
            null);
    }

    private static List<FileSearchResult> FindMatches(
        string path,
        string text,
        Regex regex,
        int maxResults,
        out bool truncated)
    {
        truncated = false;
        var matches = new List<FileSearchResult>();
        if (text.Length == 0 || maxResults <= 0)
        {
            truncated = maxResults <= 0;
            return matches;
        }

        var lineStarts = BuildLineStarts(text);

        foreach (Match match in regex.Matches(text))
        {
            if (match.Length == 0)
            {
                continue;
            }

            var (line, column, lineStart, lineEnd) = Locate(lineStarts, text, match.Index);
            var preview = SearchService.BuildPreview(
                text,
                lineStart,
                lineEnd - lineStart,
                match.Index - lineStart,
                match.Length);

            matches.Add(new FileSearchResult(
                path,
                match.Index,
                match.Length,
                line,
                column,
                preview));

            if (matches.Count >= maxResults)
            {
                truncated = true;
                break;
            }
        }

        return matches;
    }

    private static List<int> BuildLineStarts(string text)
    {
        var starts = new List<int> { 0 };

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                starts.Add(i + 1);
            }
            else if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        return starts;
    }

    private static (int Line, int Column, int LineStart, int LineEnd) Locate(
        List<int> lineStarts,
        string text,
        int offset)
    {
        var index = lineStarts.BinarySearch(offset);
        if (index < 0)
        {
            index = ~index - 1;
        }

        index = Math.Clamp(index, 0, lineStarts.Count - 1);

        var lineStart = lineStarts[index];
        var lineEnd = index + 1 < lineStarts.Count ? lineStarts[index + 1] : text.Length;

        while (lineEnd > lineStart && (text[lineEnd - 1] == '\n' || text[lineEnd - 1] == '\r'))
        {
            lineEnd--;
        }

        return (index + 1, offset - lineStart + 1, lineStart, lineEnd);
    }

    private static string[] ParseMasks(string? fileMasks)
    {
        if (string.IsNullOrWhiteSpace(fileMasks))
        {
            return [];
        }

        return fileMasks
            .Split([';', ',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(mask => mask == "*.*" ? "*" : mask)
            .Where(mask => mask.Length > 0)
            .ToArray();
    }

    private static bool MatchesMasks(string path, string[] masks)
    {
        if (masks.Length == 0)
        {
            return true;
        }

        var name = Path.GetFileName(path);
        foreach (var mask in masks)
        {
            if (FileSystemName.MatchesSimpleExpression(mask, name, ignoreCase: true))
            {
                return true;
            }
        }

        return false;
    }

    private static FileSearchSummary Error(string message) =>
        new([], 0, 0, 0, false, false, message);
}
