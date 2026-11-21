using NotepadMoreOS.Resources;
using System.Collections.ObjectModel;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotepadMoreOS.Models;
using NotepadMoreOS.Services;

namespace NotepadMoreOS.ViewModels;

public partial class FindReplaceViewModel : ObservableObject
{
    private readonly SearchService _searchService;
    private DocumentViewModel? _document;
    private int _scopeStart;
    private int _scopeLength;

    public FindReplaceViewModel(SearchService searchService)
    {
        _searchService = searchService;
    }

    public ObservableCollection<SearchResultItem> Results { get; } = [];

    public event EventHandler? CloseRequested;
    public event EventHandler? FocusRequested;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _replaceText = string.Empty;
    [ObservableProperty] private bool _matchCase;
    [ObservableProperty] private bool _wholeWord;
    [ObservableProperty] private bool _useRegex;
    [ObservableProperty] private bool _useExtendedEscapes;
    [ObservableProperty] private bool _inSelection;
    [ObservableProperty] private bool _wrapAround = true;
    [ObservableProperty] private bool _isReplaceMode;
    [ObservableProperty] private bool _isResultsOpen;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _hasError;

    public void SetTarget(DocumentViewModel? document)
    {
        if (_document is not null)
        {
            _document.Document.Changed -= OnDocumentChanged;
        }

        _document = document;

        if (_document is not null)
        {
            _document.Document.Changed += OnDocumentChanged;
        }

        _scopeStart = 0;
        _scopeLength = 0;
        Results.Clear();
        IsResultsOpen = false;
        Status = string.Empty;
        HasError = false;
    }

    public void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);

    public void SelectResult(SearchResultItem? result)
    {
        if (result is null || _document?.Host is not { } host)
        {
            return;
        }

        host.Select(result.Offset, result.Length);
        host.ScrollToLine(result.Line);
    }

    [RelayCommand]
    private void FindNext() => Find(reverse: false);

    [RelayCommand]
    private void FindPrevious() => Find(reverse: true);

    [RelayCommand]
    private void FindAll()
    {
        if (!TryPrepare(out var document, out _))
        {
            return;
        }

        var options = BuildOptions();
        if (!Validate(options))
        {
            return;
        }

        if (!options.HasPattern)
        {
            Status = Strings.Find_TypeSomething;
            HasError = false;
            return;
        }

        var found = _searchService.FindAll(document.Document, options);
        Results.Clear();

        foreach (var match in found.Matches)
        {
            Results.Add(new SearchResultItem(match.Offset, match.Length, match.Line, match.Column, match.Preview));
        }

        IsResultsOpen = found.Matches.Count > 0;
        var suffix = found.Truncated ? " (first 10,000 shown)" : string.Empty;
        Status = found.Matches.Count switch
        {
            0 => Strings.Find_NoMatches,
            1 => Strings.Find_OneMatch,
            _ => found.Truncated
                ? string.Format(Strings.Find_ManyMatchesTruncated, found.Matches.Count)
                : string.Format(Strings.Find_ManyMatches, found.Matches.Count)
        };
        HasError = false;
    }

    [RelayCommand]
    private void CloseResults() => IsResultsOpen = false;

    [RelayCommand]
    private void Replace()
    {
        if (!TryPrepare(out var document, out var host))
        {
            return;
        }

        var options = BuildOptions();
        if (!Validate(options))
        {
            return;
        }

        if (!options.HasPattern)
        {
            Status = Strings.Find_TypeSomething;
            HasError = false;
            return;
        }

        var gotSelectedMatch = host.SelectionLength > 0 &&
                               _searchService.IsMatch(
                                   document.Document.GetText(host.SelectionStart, host.SelectionLength),
                                   options with { SelectionStart = -1, SelectionLength = 0 });

        if (!gotSelectedMatch)
        {
            Find(reverse: false);
            return;
        }

        var replacementText = options.UseExtendedEscapes
            ? SearchService.UnescapeExtended(ReplaceText)
            : ReplaceText;

        document.Document.UndoStack.StartUndoGroup();
        try
        {
            var offset = host.SelectionStart;
            var length = host.SelectionLength;
            document.Document.Replace(offset, length, replacementText);
            host.Select(offset, replacementText.Length);
        }
        finally
        {
            document.Document.UndoStack.EndUndoGroup();
        }

        Status = Strings.Find_ReplacedOne;
        HasError = false;
        Find(reverse: false);
    }

    [RelayCommand]
    private void ReplaceAll()
    {
        if (!TryPrepare(out var document, out _))
        {
            return;
        }

        var options = BuildOptions();
        if (!Validate(options))
        {
            return;
        }

        if (!options.HasPattern)
        {
            Status = Strings.Find_TypeSomething;
            HasError = false;
            return;
        }

        var (newText, count) = _searchService.ReplaceAll(document.Document, options, ReplaceText);
        if (count == 0)
        {
            Status = Strings.Find_NoMatches;
            HasError = false;
            return;
        }

        if (!string.Equals(newText, document.Document.Text, StringComparison.Ordinal))
        {
            document.Document.UndoStack.StartUndoGroup();
            try
            {
                document.Document.Replace(0, document.Document.TextLength, newText);
            }
            finally
            {
                document.Document.UndoStack.EndUndoGroup();
            }
        }

        Status = count == 1 ? Strings.Find_ReplacedOne : string.Format(Strings.Find_ReplacedMany, count);
        HasError = false;
        Results.Clear();
        IsResultsOpen = false;
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void RequestFocusAgain() => RequestFocus();

    private void Find(bool reverse)
    {
        if (!TryPrepare(out var document, out var host))
        {
            return;
        }

        var options = BuildOptions();
        if (!Validate(options))
        {
            return;
        }

        if (!options.HasPattern)
        {
            Status = Strings.Find_TypeSomething;
            HasError = false;
            return;
        }

        var textDocument = document.Document;
        var from = host.SelectionLength > 0
            ? (reverse ? host.SelectionStart : host.SelectionStart + host.SelectionLength)
            : host.CaretOffset;

        var found = _searchService.FindAll(textDocument, options);
        if (found.Matches.Count == 0)
        {
            Status = Strings.Find_NoMatches;
            HasError = false;
            return;
        }

        var index = reverse
            ? LastIndexBefore(found.Matches, from)
            : FirstIndexAtOrAfter(found.Matches, from);

        if (index < 0)
        {
            if (!WrapAround)
            {
                Status = reverse ? Strings.Find_ReachedTop : Strings.Find_ReachedEnd;
                return;
            }

            index = reverse ? found.Matches.Count - 1 : 0;
        }

        var match = found.Matches[index];
        host.Select(match.Offset, match.Length);
        host.ScrollToLine(match.Line);

        var suffix = found.Truncated ? "+" : string.Empty;
        Status = string.Format(Strings.Find_MatchPosition, index + 1, found.Matches.Count, suffix);
        HasError = false;
    }

    private static int FirstIndexAtOrAfter(IReadOnlyList<SearchMatch> matches, int offset)
    {
        for (var i = 0; i < matches.Count; i++)
        {
            if (matches[i].Offset >= offset)
            {
                return i;
            }
        }

        return -1;
    }

    private static int LastIndexBefore(IReadOnlyList<SearchMatch> matches, int offset)
    {
        for (var i = matches.Count - 1; i >= 0; i--)
        {
            if (matches[i].Offset < offset)
            {
                return i;
            }
        }

        return -1;
    }

    private bool TryPrepare(out DocumentViewModel document, out IEditorHost host)
    {
        if (_document?.Host is null)
        {
            Status = Strings.Find_NoDocument;
            HasError = true;
            document = null!;
            host = null!;
            return false;
        }

        document = _document;
        host = _document.Host;
        return true;
    }

    private bool Validate(SearchOptions options)
    {
        if (!_searchService.IsPatternValid(options))
        {
            Status = Strings.Find_InvalidRegex;
            HasError = true;
            return false;
        }

        return true;
    }

    private SearchOptions BuildOptions()
    {
        var scoped = InSelection && _scopeLength > 0;
        return new SearchOptions(
            SearchText,
            MatchCase,
            WholeWord,
            UseRegex,
            scoped ? _scopeStart : -1,
            scoped ? _scopeLength : 0,
            UseExtendedEscapes);
    }

    private void CaptureScope()
    {
        if (!InSelection || _document is null)
        {
            _scopeStart = 0;
            _scopeLength = 0;
            return;
        }

        if (_document.Host is { SelectionLength: > 0 } host)
        {
            _scopeStart = host.SelectionStart;
            _scopeLength = host.SelectionLength;
        }
        else
        {
            _scopeStart = 0;
            _scopeLength = _document.Document.TextLength;
        }
    }

    private void InvalidateResults()
    {
        Results.Clear();
        IsResultsOpen = false;
        Status = string.Empty;
        HasError = false;
    }

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        // Matches found earlier may no longer be valid after an edit.
        if (Results.Count > 0)
        {
            Results.Clear();
            IsResultsOpen = false;
        }
    }

    partial void OnSearchTextChanged(string value) => RefreshAfterCriteriaChange();
    partial void OnMatchCaseChanged(bool value) => RefreshAfterCriteriaChange();
    partial void OnWholeWordChanged(bool value) => RefreshAfterCriteriaChange();
    partial void OnUseRegexChanged(bool value)
    {
        // Regex and extended escapes are alternative ways to interpret the pattern.
        if (value && UseExtendedEscapes)
        {
            UseExtendedEscapes = false;
        }

        RefreshAfterCriteriaChange();
    }

    partial void OnUseExtendedEscapesChanged(bool value)
    {
        if (value && UseRegex)
        {
            UseRegex = false;
        }

        RefreshAfterCriteriaChange();
    }
    partial void OnInSelectionChanged(bool value)
    {
        CaptureScope();
        RefreshAfterCriteriaChange();
    }

    /// <summary>
    /// Criteria changed: stale results are discarded and the live total is
    /// recomputed so the count appears before the user navigates.
    /// </summary>
    private void RefreshAfterCriteriaChange()
    {
        InvalidateResults();
        UpdateMatchSummary();
    }

    private void UpdateMatchSummary()
    {
        if (_document?.Host is null || string.IsNullOrEmpty(SearchText))
        {
            return;
        }

        var options = BuildOptions();
        if (!_searchService.IsPatternValid(options))
        {
            Status = Strings.Find_InvalidRegex;
            HasError = true;
            return;
        }

        if (!options.HasPattern)
        {
            return;
        }

        var found = _searchService.CountMatches(_document.Document, options);
        Status = found.Count switch
        {
            0 => Strings.Find_NoMatches,
            1 => Strings.Find_OneMatch,
            _ => found.Truncated
                ? string.Format(Strings.Find_ManyMatchesPlus, found.Count)
                : string.Format(Strings.Find_ManyMatches, found.Count)
        };
        HasError = false;
    }
}
