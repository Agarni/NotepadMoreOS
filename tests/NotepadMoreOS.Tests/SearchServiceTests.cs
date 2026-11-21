using AvaloniaEdit.Document;
using NotepadMoreOS.Models;
using NotepadMoreOS.Services;

namespace NotepadMoreOS.Tests;

public sealed class SearchServiceTests
{
    private readonly SearchService _search = new();

    private static TextDocument Document(string text) => new(text);

    private static SearchOptions Options(
        string pattern,
        bool matchCase = false,
        bool wholeWord = false,
        bool regex = false,
        int selectionStart = -1,
        int selectionLength = 0,
        bool extended = false) =>
        new(pattern, matchCase, wholeWord, regex, selectionStart, selectionLength, extended);

    [Fact]
    public void FindAll_IsCaseInsensitiveByDefault()
    {
        var matches = _search.FindAll(Document("Cat cat CAT"), Options("cat")).Matches;

        Assert.Equal(3, matches.Count);
    }

    [Fact]
    public void FindAll_RespectsMatchCase()
    {
        var matches = _search.FindAll(Document("Cat cat CAT"), Options("cat", matchCase: true)).Matches;

        Assert.Single(matches);
        Assert.Equal(4, matches[0].Offset);
    }

    [Fact]
    public void FindAll_RespectsWholeWord()
    {
        var matches = _search.FindAll(Document("cat scatter cat concatenate"), Options("cat", wholeWord: true)).Matches;

        Assert.Equal(2, matches.Count);
        Assert.Equal(0, matches[0].Offset);
        Assert.Equal(12, matches[1].Offset);
    }

    [Fact]
    public void FindAll_ReportsLineColumnAndPreview()
    {
        var matches = _search.FindAll(Document("one\ntwo\none"), Options("one")).Matches;

        Assert.Equal(2, matches.Count);
        Assert.Equal((1, 1), (matches[0].Line, matches[0].Column));
        Assert.Equal((3, 1), (matches[1].Line, matches[1].Column));
        Assert.Equal("one", matches[1].Preview);
    }

    [Fact]
    public void FindAll_SupportsRegex()
    {
        var matches = _search.FindAll(Document("id=10\nid=200"), Options(@"id=(\d+)", regex: true)).Matches;

        Assert.Equal(2, matches.Count);
        Assert.Equal("id=10", matches[0].Preview);
        Assert.Equal("id=200", matches[1].Preview);
    }

    [Fact]
    public void FindAll_RestrictsToSelection()
    {
        var matches = _search.FindAll(Document("one one one"), Options("one", selectionStart: 4, selectionLength: 7)).Matches;

        Assert.Equal(2, matches.Count);
        Assert.All(matches, match => Assert.True(match.Offset >= 4));
    }

    [Fact]
    public void FindAll_InvalidRegex_ReturnsEmptyAndIsReportedInvalid()
    {
        var options = Options("[", regex: true);

        Assert.False(_search.IsPatternValid(options));
        Assert.Empty(_search.FindAll(Document("anything"), options).Matches);
    }

    [Fact]
    public void FindNext_WrapsWhenRequested()
    {
        var document = Document("one two one");

        var next = _search.FindNext(document, Options("one"), fromOffset: 5, wrap: true);
        var noWrap = _search.FindNext(document, Options("one"), fromOffset: 20, wrap: false);

        Assert.NotNull(next);
        Assert.Equal(8, next!.Offset);
        Assert.Null(noWrap);
    }

    [Fact]
    public void FindPrevious_FindsMatchBeforeOffset()
    {
        var document = Document("one two one");

        var previous = _search.FindPrevious(document, Options("one"), fromOffset: 8, wrap: true);

        Assert.NotNull(previous);
        Assert.Equal(0, previous!.Offset);
    }

    [Fact]
    public void ReplaceAll_TreatsReplacementLiterallyForPlainText()
    {
        var (text, count) = _search.ReplaceAll(Document("price"), Options("price"), "$1");

        Assert.Equal("$1", text);
        Assert.Equal(1, count);
    }

    [Fact]
    public void ReplaceAll_ExpandsRegexGroups()
    {
        var (text, count) = _search.ReplaceAll(
            Document("first=1\nsecond=2"),
            Options(@"(\w+)=(\w+)", regex: true),
            "$2=$1");

        Assert.Equal("1=first\n2=second", text);
        Assert.Equal(2, count);
    }

    [Fact]
    public void ReplaceAll_OnlyReplacesInsideSelection()
    {
        var (text, count) = _search.ReplaceAll(
            Document("one one one"),
            Options("one", selectionStart: 4, selectionLength: 7),
            "two");

        Assert.Equal("one two two", text);
        Assert.Equal(2, count);
    }

    [Fact]
    public void IsMatch_RequiresWholeSelectionToMatch()
    {
        var options = Options(@"\d+", regex: true);

        Assert.True(_search.IsMatch("123", options));
        Assert.False(_search.IsMatch("123a", options));
    }

    [Fact]
    public void FindAll_SkipsZeroLengthRegexMatches()
    {
        var matches = _search.FindAll(Document("abc"), Options("^", regex: true)).Matches;

        Assert.Empty(matches);
    }

    [Theory]
    [InlineData(@"a\tb", "a\tb")]
    [InlineData(@"a\r\nb", "a\r\nb")]
    [InlineData(@"a\\b", @"a\b")]
    [InlineData(@"a\0b", "a\0b")]
    [InlineData(@"\x41\x42", "AB")]
    [InlineData(@"\u00E9", "é")]
    [InlineData(@"\o101", "A")]
    [InlineData(@"a\qb", @"a\qb")]
    public void UnescapeExtended_ExpandsSupportedEscapes(string input, string expected)
    {
        Assert.Equal(expected, SearchService.UnescapeExtended(input));
    }

    [Fact]
    public void FindAll_ExtendedModeFindsTabsAndLineBreaks()
    {
        var matches = _search.FindAll(Document("one\ttwo\tthree"), Options(@"\t", extended: true)).Matches;

        Assert.Equal(2, matches.Count);
        Assert.All(matches, match => Assert.Equal(1, match.Length));
    }

    [Fact]
    public void FindAll_ExtendedModeTreatsPatternLiterallyOtherwise()
    {
        // "a.b" must not match "axb" in extended mode: only escapes are special.
        var options = Options("a.b", extended: true);

        Assert.Empty(_search.FindAll(Document("axb"), options).Matches);
        Assert.Single(_search.FindAll(Document("a.b"), options).Matches);
    }

    [Fact]
    public void ReplaceAll_ExtendedModeExpandsEscapesInReplacement()
    {
        var (text, count) = _search.ReplaceAll(
            Document("a|b|c"),
            Options("|", extended: true),
            @"\t");

        Assert.Equal("a\tb\tc", text);
        Assert.Equal(2, count);
    }

    [Fact]
    public void ReplaceAll_ExtendedModeKeepsDollarSignLiteral()
    {
        var (text, _) = _search.ReplaceAll(Document("cost"), Options("cost", extended: true), "$1");

        Assert.Equal("$1", text);
    }
}
