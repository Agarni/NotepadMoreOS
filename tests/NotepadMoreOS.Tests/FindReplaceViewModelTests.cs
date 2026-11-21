using AvaloniaEdit.Document;
using NotepadMoreOS.Models;
using NotepadMoreOS.Services;
using NotepadMoreOS.ViewModels;

namespace NotepadMoreOS.Tests;

public sealed class FindReplaceViewModelTests
{
    private static (DocumentViewModel Document, FakeEditorHost Host, FindReplaceViewModel Find) Create(string text)
    {
        var document = TestEnvironment.CreateDocument();
        document.Document.Text = text;
        var host = new FakeEditorHost(document.Document);
        document.Host = host;
        var find = new FindReplaceViewModel(new SearchService());
        find.SetTarget(document);
        return (document, host, find);
    }

    [Fact]
    public void FindNext_SelectsFirstMatch_AndReportsPosition()
    {
        var (document, host, find) = Create("var a = 1;\nvar b = 2;\nvar c = 3;");
        find.SearchText = "var";

        find.FindNextCommand.Execute(null);

        Assert.Equal("Match 1 of 3", find.Status);
        Assert.Equal(0, host.SelectionStart);
        Assert.Equal(3, host.SelectionLength);
        document.Dispose();
    }

    [Fact]
    public void FindNext_AdvancesAndWraps()
    {
        var (document, host, find) = Create("one\none");
        find.SearchText = "one";

        find.FindNextCommand.Execute(null);
        find.FindNextCommand.Execute(null);
        find.FindNextCommand.Execute(null);

        Assert.Equal(0, host.SelectionStart);
        Assert.Equal("Match 1 of 2", find.Status);
        document.Dispose();
    }

    [Fact]
    public void FindPrevious_WrapsToLastMatch()
    {
        var (document, host, find) = Create("one\none");
        find.SearchText = "one";
        host.Select(0, 0);

        find.FindPreviousCommand.Execute(null);

        Assert.Equal(4, host.SelectionStart);
        Assert.Equal("Match 2 of 2", find.Status);
        document.Dispose();
    }

    [Fact]
    public void FindAll_PopulatesResults()
    {
        var (document, _, find) = Create("one two one");
        find.SearchText = "one";

        find.FindAllCommand.Execute(null);

        Assert.Equal(2, find.Results.Count);
        Assert.True(find.IsResultsOpen);
        Assert.Equal("2 matches", find.Status);
        document.Dispose();
    }

    [Fact]
    public void ReplaceAll_ReplacesEveryMatchAndKeepsUndoWorking()
    {
        var (document, _, find) = Create("var a = 1;\nvar b = 2;");
        find.SearchText = "var";
        find.ReplaceText = "let";

        find.ReplaceAllCommand.Execute(null);

        Assert.Equal("let a = 1;\nlet b = 2;", document.Document.Text);
        Assert.Equal("Replaced 2 occurrences", find.Status);
        Assert.True(document.IsDirty);

        document.Document.UndoStack.Undo();
        Assert.Equal("var a = 1;\nvar b = 2;", document.Document.Text);
        document.Dispose();
    }

    [Fact]
    public void InSelection_RestrictsFindAllToCapturedScope()    {
        var (document, host, find) = Create("one one one");
        host.Select(0, 7);
        find.InSelection = true;
        find.SearchText = "one";

        find.FindAllCommand.Execute(null);

        Assert.Equal(2, find.Results.Count);
        Assert.All(find.Results, result => Assert.True(result.Offset < 7));
        document.Dispose();
    }

    [Fact]
    public void InSelection_CountsOnlyTheCapturedScope()
    {
        var (document, host, find) = Create("one one one");
        host.Select(0, 7);

        find.InSelection = true;
        find.SearchText = "one";

        Assert.Equal("2 matches", find.Status);
        document.Dispose();
    }

    [Fact]
    public void InvalidRegex_ReportsError()
    {
        var (document, _, find) = Create("anything");
        find.SearchText = "[";
        find.UseRegex = true;

        find.FindNextCommand.Execute(null);

        Assert.True(find.HasError);
        Assert.Equal("Invalid regular expression", find.Status);
        document.Dispose();
    }

    [Fact]
    public void TypingSearchText_ReportsMatchCountBeforeNavigating()
    {
        var (document, _, find) = Create("var a = 1;\nvar b = 2;\nvar c = 3;");

        find.SearchText = "var";

        Assert.Equal("3 matches", find.Status);
        Assert.False(find.HasError);
        document.Dispose();
    }

    [Fact]
    public void TypingSearchText_ReportsSingleMatchAndNoMatches()
    {
        var (document, _, find) = Create("alpha beta");

        find.SearchText = "alpha";
        Assert.Equal("1 match", find.Status);

        find.SearchText = "gamma";
        Assert.Equal("No matches", find.Status);

        find.SearchText = string.Empty;
        Assert.Equal(string.Empty, find.Status);
        document.Dispose();
    }

    [Fact]
    public void ChangingOptions_RecomputesLiveCount()
    {
        var (document, _, find) = Create("One one ONE");

        find.SearchText = "one";
        Assert.Equal("3 matches", find.Status);

        find.MatchCase = true;
        Assert.Equal("1 match", find.Status);

        find.WholeWord = true;
        Assert.Equal("1 match", find.Status);
        document.Dispose();
    }

    [Fact]
    public void InvalidRegex_ReportsErrorWhileTyping()
    {
        var (document, _, find) = Create("anything");
        find.UseRegex = true;

        find.SearchText = "[";

        Assert.True(find.HasError);
        Assert.Equal("Invalid regular expression", find.Status);
        document.Dispose();
    }

    [Fact]
    public void ExtendedModeAndRegex_AreMutuallyExclusive()
    {
        var (document, _, find) = Create("a\tb");

        find.UseExtendedEscapes = true;
        find.SearchText = @"\t";
        Assert.Equal("1 match", find.Status);

        find.UseRegex = true;
        Assert.False(find.UseExtendedEscapes);

        find.UseExtendedEscapes = true;
        Assert.False(find.UseRegex);
        Assert.Equal("1 match", find.Status);
        document.Dispose();
    }

    [Fact]
    public void ExtendedMode_ReplacesEscapedTextAndUnescapesReplacement()
    {
        var (document, _, find) = Create("a|b");
        find.UseExtendedEscapes = true;
        find.SearchText = "|";
        find.ReplaceText = @"\t";

        find.ReplaceCommand.Execute(null);
        find.ReplaceCommand.Execute(null);

        Assert.Equal("a\tb", document.Document.Text);
        document.Dispose();
    }

    [Fact]
    public void Close_RaisesCloseRequested()
    {
        var (document, _, find) = Create("anything");
        var raised = false;
        find.CloseRequested += (_, _) => raised = true;

        find.CloseCommand.Execute(null);

        Assert.True(raised);
        document.Dispose();
    }

    private sealed class FakeEditorHost(TextDocument document) : IEditorHost
    {
        public TextDocument Document { get; } = document;
        public int CaretOffset { get; set; }
        public int SelectionStart { get; private set; }
        public int SelectionLength { get; private set; }
        public bool CanUndo => Document.UndoStack.CanUndo;
        public bool CanRedo => Document.UndoStack.CanRedo;

        public void Select(int start, int length)
        {
            SelectionStart = start;
            SelectionLength = length;
            CaretOffset = start;
        }

        public void ScrollToLine(int line) { }
        public void Focus() { }
        public void Undo() => Document.UndoStack.Undo();
        public void Redo() => Document.UndoStack.Redo();
        public void Cut() { }
        public void Copy() { }
        public void Paste() { }
        public void Delete() { }
        public void SelectAll() => Select(0, Document.TextLength);
        public void DuplicateLine() { }
        public void DeleteLine() { }
        public void ToggleComment(string prefix) { }
    }
}
