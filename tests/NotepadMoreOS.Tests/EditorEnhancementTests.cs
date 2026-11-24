using NotepadMoreOS.Services;
using Xunit;

namespace NotepadMoreOS.Tests;

public class EditorEnhancementTests
{
    [Theory]
    [InlineData("v1.7.5", true)]
    [InlineData("1.8.0", true)]
    [InlineData("v1.8.0-beta", false)]
    [InlineData("1.8", false)]
    [InlineData("v1.8.0.1", false)]
    [InlineData("", false)]
    public void ParsesOnlyThreePartStableReleaseTags(string tag, bool valid)
        => Assert.Equal(valid, UpdateCheckService.TryParseVersion(tag, out _));

    [Fact]
    public void JsonFoldsIgnoreStringsAndCommentsAndNestInOrder()
    {
        const string text = "{\n  \"value\": \"} [ \\\"\",\n  // }\n  \"items\": [\n    1 /* ] */\n  ]\n}";
        var folds = JsonFoldingService.Find(text);
        Assert.Equal(2, folds.Count);
        Assert.Equal(0, folds[0].StartOffset);
        Assert.Equal(text.Length, folds[0].EndOffset);
        Assert.Equal(text.IndexOf("[\n", StringComparison.Ordinal), folds[1].StartOffset);
    }

    [Fact]
    public void JsonFoldsDoNotPairMismatchedBrackets()
        => Assert.Empty(JsonFoldingService.Find("{\n]\n}"));
}
