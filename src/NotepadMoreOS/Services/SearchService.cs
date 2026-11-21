using System.Text;
using System.Text.RegularExpressions;
using AvaloniaEdit.Document;
using NotepadMoreOS.Models;

namespace NotepadMoreOS.Services;

public sealed record SearchResults(IReadOnlyList<SearchMatch> Matches, bool Truncated);

public sealed record SearchCount(int Count, bool Truncated);

/// <summary>Plain-text and regular-expression search over an AvaloniaEdit document.</summary>
public sealed class SearchService
{
    public const int MaxMatches = 10_000;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    public bool IsPatternValid(SearchOptions options) =>
        !options.HasPattern || BuildRegex(options) is not null;

    internal Regex? CreateMatcher(SearchOptions options) => BuildRegex(options);

    public SearchResults FindAll(TextDocument document, SearchOptions options)
    {
        if (!options.HasPattern)
        {
            return new SearchResults([], false);
        }

        var text = document.Text;
        var scope = GetScope(text, options);
        if (scope.Length == 0)
        {
            return new SearchResults([], false);
        }

        var regex = BuildRegex(options);
        if (regex is null)
        {
            return new SearchResults([], false);
        }

        var matches = new List<SearchMatch>();
        var truncated = false;

        try
        {
            foreach (Match match in regex.Matches(text.Substring(scope.Start, scope.Length)))
            {
                if (match.Length == 0)
                {
                    continue;
                }

                if (matches.Count >= MaxMatches)
                {
                    truncated = true;
                    break;
                }

                var offset = scope.Start + match.Index;
                var line = document.GetLineByOffset(offset);
                var column = offset - line.Offset + 1;
                matches.Add(new SearchMatch(
                    offset,
                    match.Length,
                    line.LineNumber,
                    column,
                    BuildPreview(text, line.Offset, line.Length, offset - line.Offset, match.Length)));
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return new SearchResults([], false);
        }

        return new SearchResults(matches, truncated);
    }

    /// <summary>
    /// Counts matches without building previews or match records, so the find bar
    /// can show a live total while the user types.
    /// </summary>
    public SearchCount CountMatches(TextDocument document, SearchOptions options)
    {
        if (!options.HasPattern)
        {
            return new SearchCount(0, false);
        }

        var text = document.Text;
        var scope = GetScope(text, options);
        if (scope.Length == 0)
        {
            return new SearchCount(0, false);
        }

        var regex = BuildRegex(options);
        if (regex is null)
        {
            return new SearchCount(0, false);
        }

        var source = scope.Start == 0 && scope.Length == text.Length
            ? text
            : text.Substring(scope.Start, scope.Length);

        var count = 0;
        var truncated = false;

        try
        {
            foreach (Match match in regex.Matches(source))
            {
                if (match.Length == 0)
                {
                    continue;
                }

                if (count >= MaxMatches)
                {
                    truncated = true;
                    break;
                }

                count++;
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return new SearchCount(0, false);
        }

        return new SearchCount(count, truncated);
    }

    public SearchMatch? FindNext(TextDocument document, SearchOptions options, int fromOffset, bool wrap)    {
        var matches = FindAll(document, options).Matches;
        if (matches.Count == 0)
        {
            return null;
        }

        var next = matches.FirstOrDefault(match => match.Offset >= fromOffset);
        if (next is not null)
        {
            return next;
        }

        return wrap ? matches[0] : null;
    }

    public SearchMatch? FindPrevious(TextDocument document, SearchOptions options, int fromOffset, bool wrap)
    {
        var matches = FindAll(document, options).Matches;
        if (matches.Count == 0)
        {
            return null;
        }

        for (var i = matches.Count - 1; i >= 0; i--)
        {
            if (matches[i].Offset < fromOffset)
            {
                return matches[i];
            }
        }

        return wrap ? matches[^1] : null;
    }

    /// <summary>Replaces every match and returns the new document text plus the replacement count.</summary>
    public (string Text, int Count) ReplaceAll(TextDocument document, SearchOptions options, string replacement)
    {
        if (!options.HasPattern)
        {
            return (document.Text, 0);
        }

        var text = document.Text;
        var scope = GetScope(text, options);
        var regex = BuildRegex(options);
        if (regex is null || scope.Length == 0)
        {
            return (text, 0);
        }

        var subject = text.Substring(scope.Start, scope.Length);
        var effectiveReplacement = PrepareReplacement(replacement, options);

        try
        {
            var count = regex.Matches(subject).Count;
            if (count == 0)
            {
                return (text, 0);
            }

            var replaced = regex.Replace(subject, effectiveReplacement);
            var result = string.Concat(text.AsSpan(0, scope.Start), replaced, text.AsSpan(scope.Start + scope.Length));
            return (result, count);
        }
        catch (RegexMatchTimeoutException)
        {
            return (text, 0);
        }
    }

    public bool IsMatch(string text, SearchOptions options)
    {
        var regex = BuildRegex(options);
        if (regex is null)
        {
            return false;
        }

        try
        {
            var match = regex.Match(text);
            return match.Success && match.Index == 0 && match.Length == text.Length;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static (int Start, int Length) GetScope(string text, SearchOptions options)
    {
        if (!options.ScopeToSelection)
        {
            return (0, text.Length);
        }

        var start = Math.Clamp(options.SelectionStart, 0, text.Length);
        var length = Math.Clamp(options.SelectionLength, 0, text.Length - start);
        return (start, length);
    }

    internal static Regex? BuildRegex(SearchOptions options)
    {
        try
        {
            var pattern = options.UseRegex
                ? options.Pattern
                : Regex.Escape(options.UseExtendedEscapes
                    ? UnescapeExtended(options.Pattern)
                    : options.Pattern);

            if (options.WholeWord)
            {
                pattern = $@"(?<![\p{{L}}\p{{N}}_])(?:{pattern})(?![\p{{L}}\p{{N}}_])";
            }

            var regexOptions = RegexOptions.Multiline | RegexOptions.CultureInvariant;
            if (!options.MatchCase)
            {
                regexOptions |= RegexOptions.IgnoreCase;
            }

            return new Regex(pattern, regexOptions, RegexTimeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Expands Notepad++-style extended escapes (\t, \r, \n, \0, \xHH, \uHHHH,
    /// \oNNN, \\ and the C control escapes) into literal characters.
    /// Unknown escapes stay untouched.
    /// </summary>
    public static string UnescapeExtended(string pattern)
    {
        if (pattern.IndexOf('\\') < 0)
        {
            return pattern;
        }

        var builder = new StringBuilder(pattern.Length);

        for (var i = 0; i < pattern.Length; i++)
        {
            var current = pattern[i];
            if (current != '\\' || i == pattern.Length - 1)
            {
                builder.Append(current);
                continue;
            }

            switch (pattern[i + 1])
            {
                case 'a':
                    builder.Append('\a');
                    i++;
                    continue;
                case 'b':
                    builder.Append('\b');
                    i++;
                    continue;
                case 'f':
                    builder.Append('\f');
                    i++;
                    continue;
                case 'n':
                    builder.Append('\n');
                    i++;
                    continue;
                case 'r':
                    builder.Append('\r');
                    i++;
                    continue;
                case 't':
                    builder.Append('\t');
                    i++;
                    continue;
                case 'v':
                    builder.Append('\v');
                    i++;
                    continue;
                case '\\':
                    builder.Append('\\');
                    i++;
                    continue;
                case '0':
                    builder.Append('\0');
                    i++;
                    continue;
                case 'x' when TryReadNumber(pattern, i + 2, maxDigits: 2, numberBase: 16, out var hex, out var hexDigits):
                    builder.Append((char)hex);
                    i += 1 + hexDigits;
                    continue;
                case 'u' when TryReadNumber(pattern, i + 2, maxDigits: 4, numberBase: 16, out var unicode, out var unicodeDigits) &&
                               unicodeDigits == 4:
                    builder.Append((char)unicode);
                    i += 1 + unicodeDigits;
                    continue;
                case 'o' when TryReadNumber(pattern, i + 2, maxDigits: 3, numberBase: 8, out var octal, out var octalDigits):
                    builder.Append((char)octal);
                    i += 1 + octalDigits;
                    continue;
                default:
                    // Unknown escape: keep the backslash so nothing is lost silently.
                    builder.Append(current);
                    builder.Append(pattern[i + 1]);
                    i++;
                    continue;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Resolves the replacement text for the active search mode. Regex mode keeps
    /// group references ($1); the other modes treat '$' literally and expand
    /// extended escapes when that mode is enabled.
    /// </summary>
    public static string PrepareReplacement(string replacement, SearchOptions options)
    {
        if (options.UseRegex)
        {
            return replacement;
        }

        var text = options.UseExtendedEscapes ? UnescapeExtended(replacement) : replacement;
        return text.Replace("$", "$$");
    }

    private static bool TryReadNumber(
        string pattern,
        int start,
        int maxDigits,
        int numberBase,
        out int value,
        out int digits)
    {
        value = 0;
        digits = 0;

        while (digits < maxDigits && start + digits < pattern.Length)
        {
            var digit = DigitValue(pattern[start + digits], numberBase);
            if (digit < 0)
            {
                break;
            }

            value = value * numberBase + digit;
            digits++;
        }

        return digits > 0;
    }

    private static int DigitValue(char value, int numberBase)
    {
        var digit = value switch
        {
            >= '0' and <= '9' => value - '0',
            >= 'a' and <= 'f' => value - 'a' + 10,
            >= 'A' and <= 'F' => value - 'A' + 10,
            _ => -1
        };

        return digit >= 0 && digit < numberBase ? digit : -1;
    }

    internal static string BuildPreview(string text, int lineOffset, int lineLength, int matchInLine, int matchLength)
    {
        var builder = new StringBuilder();
        var line = text.AsSpan(lineOffset, lineLength);

        const int maxLength = 160;
        var start = 0;
        var end = line.Length;

        if (end > maxLength)
        {
            start = Math.Max(0, matchInLine - 50);
            end = Math.Min(line.Length, start + maxLength);
        }

        if (start > 0)
        {
            builder.Append("…");
        }

        for (var i = start; i < end; i++)
        {
            var c = line[i];
            builder.Append(c switch
            {
                '\t' => "    ",
                _ when char.IsControl(c) => ' ',
                _ => c
            });
        }

        if (end < line.Length)
        {
            builder.Append('…');
        }

        return builder.ToString();
    }
}
