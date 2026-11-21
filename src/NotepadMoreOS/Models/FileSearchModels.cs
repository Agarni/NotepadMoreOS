namespace NotepadMoreOS.Models;

public sealed record FileSearchOptions(
    string Folder,
    string Pattern,
    string FileMasks,
    bool MatchCase,
    bool WholeWord,
    bool UseRegex,
    bool IncludeSubdirectories,
    bool SkipHiddenAndBuildFolders)
{
    public SearchOptions ToSearchOptions() =>
        new(Pattern, MatchCase, WholeWord, UseRegex);
}

public sealed record FileSearchResult(
    string FilePath,
    int Offset,
    int Length,
    int Line,
    int Column,
    string Preview);

public sealed record FileSearchProgress(
    int FilesScanned,
    int FilesMatched,
    int MatchesFound,
    string CurrentFile);

public sealed record FileSearchSummary(
    IReadOnlyList<FileSearchResult> Results,
    int FilesScanned,
    int FilesMatched,
    int FilesSkipped,
    bool Truncated,
    bool Cancelled,
    string? Error);
