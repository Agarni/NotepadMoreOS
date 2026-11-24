using NotepadMoreOS.Services;
using Xunit;

namespace NotepadMoreOS.Tests;

public class BracketMatchingServiceTests
{
    [Theory]
    [InlineData("{\n [()]\n}", 0, 0, 8)]
    [InlineData("{\n [()]\n}", 5, 4, 5)]
    [InlineData("abc (x)", 7, 4, 6)]
    public void FindsNestedAndMultilinePairs(string text, int caret, int first, int second)
    {
        Assert.Equal((first, second), BracketMatchingService.Find(text, caret));
    }

    [Theory]
    [InlineData("([)]", 0)]
    [InlineData("{abc", 0)]
    [InlineData("plain text", 3)]
    public void DoesNotMatchInvalidOrMissingPairs(string text, int caret)
    {
        Assert.Null(BracketMatchingService.Find(text, caret));
    }
}
