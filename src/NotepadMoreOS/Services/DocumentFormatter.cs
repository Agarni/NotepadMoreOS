using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace NotepadMoreOS.Services;

internal static partial class DocumentFormatter
{
    public static bool Supports(string extension) => extension is ".json" or ".jsonc" or ".xml";

    public static string? Format(string source, string extension, string indent, string newline)
    {
        if (!Supports(extension))
            return null;

        try
        {
            return extension == ".xml"
                ? FormatXml(source, indent, newline)
                : FormatJson(source, extension == ".jsonc", indent, newline);
        }
        catch (Exception ex) when (ex is JsonException or XmlException or ArgumentException)
        {
            return null;
        }
    }

    private static string FormatJson(string source, bool allowComments, string indent, string newline)
    {
        var options = new JsonDocumentOptions
        {
            CommentHandling = allowComments ? JsonCommentHandling.Skip : JsonCommentHandling.Disallow,
            AllowTrailingCommas = allowComments
        };
        using var document = JsonDocument.Parse(source, options);

        var output = new StringBuilder();
        var depth = 0;
        var previous = string.Empty;
        var startLine = true;

        foreach (Match match in JsonTokens().Matches(source))
        {
            var token = match.Value;
            if (token is "}" or "]")
            {
                depth--;
                if (previous is not ("{" or "["))
                    startLine = true;
            }

            if (startLine && !(token is "}" or "]" && previous is "{" or "["))
            {
                if (output.Length > 0)
                    output.Append(newline);
                for (var level = 0; level < depth; level++)
                    output.Append(indent);
            }
            else if (previous == ":" || token.StartsWith("/*", StringComparison.Ordinal)
                || token.StartsWith("//", StringComparison.Ordinal))
            {
                output.Append(' ');
            }

            output.Append(token);
            if (token is "{" or "[")
                depth++;

            startLine = token is "{" or "[" or "," || token.StartsWith("/", StringComparison.Ordinal);
            previous = token;
        }

        var formatted = output.Append(newline).ToString();
        using var result = JsonDocument.Parse(formatted, options);
        return JsonElement.DeepEquals(document.RootElement, result.RootElement)
            ? formatted
            : throw new JsonException();
    }

    private static string FormatXml(string source, string indent, string newline)
    {
        using var input = new StringReader(source);
        using var reader = XmlReader.Create(input, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = null,
            MaxCharactersFromEntities = 1_000_000,
            IgnoreWhitespace = false
        });
        var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        var output = new StringBuilder();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings
        {
            Indent = !document.DescendantNodes().OfType<XText>().Any(text =>
                string.IsNullOrWhiteSpace(text.Value) && text.Parent?.Elements().Any() == true),
            IndentChars = indent,
            NewLineChars = newline,
            NewLineHandling = NewLineHandling.None,
            OmitXmlDeclaration = true
        }))
        {
            document.Save(writer);
        }

        return (document.Declaration is null ? string.Empty : document.Declaration + newline)
            + output + newline;
    }

    [GeneratedRegex(""" "(?:\\.|[^"\\])*" | //[^\r\n]* | /\*[\s\S]*?\*/ | [{}\[\],:] | [^\s{}\[\],:"/]+ """,
        RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex JsonTokens();
}
