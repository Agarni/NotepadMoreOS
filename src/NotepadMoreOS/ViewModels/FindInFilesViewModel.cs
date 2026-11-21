using NotepadMoreOS.Resources;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotepadMoreOS.Models;
using NotepadMoreOS.Services;

namespace NotepadMoreOS.ViewModels;

public sealed class FileSearchResultViewModel
{
    public FileSearchResultViewModel(FileSearchResult result)
    {
        FilePath = result.FilePath;
        Offset = result.Offset;
        Length = result.Length;
        Line = result.Line;
        Column = result.Column;
        Preview = result.Preview;
    }

    public string FilePath { get; }
    public int Offset { get; }
    public int Length { get; }
    public int Line { get; }
    public int Column { get; }
    public string Preview { get; }

    public string Location => string.Format(Strings.FIF_Location, Line, Column);

    public FileSearchResult ToModel() => new(FilePath, Offset, Length, Line, Column, Preview);
}

public sealed class FileSearchGroupViewModel
{
    public FileSearchGroupViewModel(string filePath, IReadOnlyList<FileSearchResultViewModel> results)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        Directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        Results = results;
    }

    public string FilePath { get; }
    public string FileName { get; }
    public string Directory { get; }
    public IReadOnlyList<FileSearchResultViewModel> Results { get; }

    public string CountText => Results.Count == 1
        ? Strings.FIF_OneResult
        : string.Format(Strings.FIF_ManyResults, Results.Count);
}

public partial class FindInFilesViewModel : ObservableObject, IDisposable
{
    private readonly FileSearchService _searchService;
    private CancellationTokenSource? _cancellation;
    private bool _disposed;
    private int _searchVersion;

    public FindInFilesViewModel(FileSearchService searchService)
    {
        _searchService = searchService;
    }

    public ObservableCollection<FileSearchGroupViewModel> Groups { get; } = [];

    public bool HasResults => Groups.Count > 0;
    public bool HasNoResults => Groups.Count == 0;
    public bool ShowEmptyState => !IsSearching && !HasSearched && Groups.Count == 0;
    public bool CanEdit => !IsSearching;
    public bool CanSearch => !IsSearching;
    public bool CanOpenResult => SelectedResult is not null;

    [ObservableProperty] private string _folder = string.Empty;
    [ObservableProperty] private string _pattern = string.Empty;
    [ObservableProperty] private string _fileMasks = "*.*";
    [ObservableProperty] private bool _matchCase;
    [ObservableProperty] private bool _wholeWord;
    [ObservableProperty] private bool _useRegex;
    [ObservableProperty] private bool _includeSubdirectories = true;
    [ObservableProperty] private bool _skipHiddenAndBuildFolders = true;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private bool _hasSearched;
    [ObservableProperty] private string _status = Strings.FIF_EnterSearchAndFolder;
    [ObservableProperty] private string _currentFile = string.Empty;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private FileSearchResultViewModel? _selectedResult;

    public void Initialize(string? startFolder, string? initialPattern)
    {
        if (!string.IsNullOrWhiteSpace(startFolder) && Directory.Exists(startFolder))
        {
            Folder = startFolder;
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Folder = string.IsNullOrEmpty(home) ? Directory.GetCurrentDirectory() : home;
        }

        if (!string.IsNullOrWhiteSpace(initialPattern) && !initialPattern.Contains('\n') && !initialPattern.Contains('\r'))
        {
            Pattern = initialPattern;
        }
    }

    public FileSearchResult? SelectedResultModel() => SelectedResult?.ToModel();

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (IsSearching)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Pattern))
        {
            Status = Strings.FIF_TypeSomething;
            HasError = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(Folder) || !Directory.Exists(Folder))
        {
            Status = Strings.FIF_ChooseFolder;
            HasError = true;
            return;
        }

        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource();
        var version = ++_searchVersion;

        IsSearching = true;
        HasSearched = true;
        HasError = false;
        CurrentFile = string.Empty;
        SelectedResult = null;
        ClearGroups();
        Status = Strings.FIF_Searching;

        var options = new FileSearchOptions(
            Folder,
            Pattern,
            FileMasks,
            MatchCase,
            WholeWord,
            UseRegex,
            IncludeSubdirectories,
            SkipHiddenAndBuildFolders);

        var progress = new Progress<FileSearchProgress>(update => OnProgress(version, update));

        try
        {
            var summary = await _searchService.SearchAsync(options, progress, _cancellation.Token);

            // Stop accepting progress before publishing the final status; queued
            // progress callbacks must not overwrite the summary.
            IsSearching = false;

            if (summary.Error is not null)
            {
                Status = summary.Error;
                HasError = true;
                return;
            }

            BuildGroups(summary);
            Status = BuildStatus(summary);
            HasError = false;
        }
        catch (OperationCanceledException)
        {
            IsSearching = false;
            Status = Strings.FIF_Cancelled;
            HasError = false;
        }
        catch (Exception ex)
        {
            IsSearching = false;
            Status = string.Format(Strings.FIF_Failed, ex.Message);
            HasError = true;
        }
        finally
        {
            IsSearching = false;
            CurrentFile = string.Empty;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (!IsSearching)
        {
            return;
        }

        Status = Strings.FIF_Cancelling;
        _cancellation?.Cancel();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
    }

    partial void OnIsSearchingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanSearch));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnSelectedResultChanged(FileSearchResultViewModel? value)
    {
        OnPropertyChanged(nameof(CanOpenResult));
    }

    partial void OnHasSearchedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private void OnProgress(int version, FileSearchProgress progress)
    {
        if (!IsSearching || version != _searchVersion)
        {
            return;
        }

        Status = string.Format(Strings.FIF_Scanning, progress.FilesScanned, progress.MatchesFound);
        CurrentFile = string.IsNullOrEmpty(progress.CurrentFile)
            ? string.Empty
            : Path.GetFileName(progress.CurrentFile);
    }

    private void BuildGroups(FileSearchSummary summary)
    {
        ClearGroups();

        foreach (var group in summary.Results.GroupBy(result => result.FilePath))
        {
            var results = group.Select(result => new FileSearchResultViewModel(result)).ToList();
            Groups.Add(new FileSearchGroupViewModel(group.Key, results));
        }

        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(HasNoResults));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private void ClearGroups()
    {
        Groups.Clear();
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(HasNoResults));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private static string BuildStatus(FileSearchSummary summary)
    {
        var prefix = summary.Cancelled ? Strings.FIF_Cancelled + " " : string.Empty;
        var resultWord = summary.Results.Count == 1 ? Strings.FIF_ResultWord : Strings.FIF_ResultsWord;

        var fileWord = summary.FilesMatched == 1 ? Strings.FIF_FileWord : Strings.FIF_FilesWord;
        var message = summary.Results.Count == 0
            ? prefix + string.Format(Strings.FIF_NoMatchesSummary, summary.FilesScanned.ToString("N0"))
            : prefix + string.Format(
                Strings.FIF_Summary,
                summary.Results.Count.ToString("N0"),
                resultWord,
                summary.FilesMatched.ToString("N0"),
                fileWord,
                summary.FilesScanned.ToString("N0"),
                summary.FilesSkipped.ToString("N0"));

        if (summary.Truncated)
        {
            message += string.Format(Strings.FIF_Limited, FileSearchService.MaxResults.ToString("N0"));
        }

        return message;
    }
}
