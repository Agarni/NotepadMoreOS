using System.Text.Json;
using System.Xml.Linq;
using NotepadMoreOS.Services;

namespace NotepadMoreOS.Tests;

public class DocumentFormatterTests
{
    [Fact]
    public void JsonFormatsNestedObjectsAndArrays()
    {
        const string source = "{\"items\":[{\"name\":\"a/b\"},{}],\"empty\":[]}";
        var result = DocumentFormatter.Format(source, ".json", "  ", "\n");

        Assert.Equal("{\n  \"items\": [\n    {\n      \"name\": \"a/b\"\n    },\n    {}\n  ],\n  \"empty\": []\n}\n", result);
        Assert.True(JsonElement.DeepEquals(JsonDocument.Parse(source).RootElement, JsonDocument.Parse(result!).RootElement));
    }

    [Fact]
    public void JsoncPreservesCommentsAndTrailingCommas()
    {
        const string source = "{ // heading\n \"url\": \"https://example.org/a/*b\", /* note */ \"values\": [1, 2,],\n}";
        var result = DocumentFormatter.Format(source, ".jsonc", "\t", "\r\n");

        Assert.NotNull(result);
        Assert.Contains("// heading", result);
        Assert.Contains("/* note */", result);
        Assert.Contains("https://example.org/a/*b", result);
        Assert.Contains("\r\n\t\"values\"", result);
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        Assert.True(JsonElement.DeepEquals(JsonDocument.Parse(source, options).RootElement, JsonDocument.Parse(result, options).RootElement));
    }

    [Theory]
    [InlineData("{\"a\":}", ".json")]
    [InlineData("{\"a\": 1 // missing end", ".jsonc")]
    [InlineData("<root><item></root>", ".xml")]
    public void InvalidDocumentsAreRejected(string source, string extension)
    {
        Assert.Null(DocumentFormatter.Format(source, extension, "  ", "\n"));
    }

    [Fact]
    public void XmlPreservesMixedContentAndSignificantWhitespace()
    {
        const string source = "<root><p>before <b>bold</b> after</p><code xml:space=\"preserve\">  <x/>  </code><items><item/><item/></items></root>";
        var result = DocumentFormatter.Format(source, ".xml", "  ", "\n");

        Assert.NotNull(result);
        var original = XDocument.Parse(source, LoadOptions.PreserveWhitespace);
        var formatted = XDocument.Parse(result, LoadOptions.PreserveWhitespace);
        Assert.Equal(original.Root!.Element("p")!.ToString(SaveOptions.DisableFormatting), formatted.Root!.Element("p")!.ToString(SaveOptions.DisableFormatting));
        Assert.Equal(original.Root.Element("code")!.ToString(SaveOptions.DisableFormatting), formatted.Root.Element("code")!.ToString(SaveOptions.DisableFormatting));
        Assert.Equal(2, formatted.Root.Element("items")!.Elements("item").Count());
    }

    [Fact]
    public void CompactXmlIsIndented()
    {
        var result = DocumentFormatter.Format("<root><items><item/></items></root>", ".xml", "  ", "\n");
        Assert.Contains("\n    <item", result);
    }

    [Fact]
    public void XmlPreservesWhitespaceBetweenElements()
    {
        const string source = "<root><x/>   <y/></root>";
        var result = DocumentFormatter.Format(source, ".xml", "  ", "\n");
        Assert.NotNull(result);
        Assert.Contains("   ", result);
        Assert.Equal("   ", XDocument.Parse(result, LoadOptions.PreserveWhitespace).Root!.Nodes().OfType<XText>().Single().Value);
    }
}
