namespace NotepadMoreOS.Models;

public enum LineEnding
{
    CrLf,
    Lf,
    Cr
}

public sealed record LineEndingOption(LineEnding Value, string Name, string ShortName)
{
    public override string ToString() => ShortName;
}

public static class LineEndingCatalog
{
    public static IReadOnlyList<LineEndingOption> All { get; } =
    [
        new(LineEnding.CrLf, "Windows (CRLF)", "CRLF"),
        new(LineEnding.Lf, "Unix (LF)", "LF"),
        new(LineEnding.Cr, "Classic Mac (CR)", "CR")
    ];

    public static LineEndingOption Get(LineEnding value) =>
        All.First(option => option.Value == value);

    public static string ToSequence(this LineEnding lineEnding) => lineEnding switch
    {
        LineEnding.CrLf => "\r\n",
        LineEnding.Lf => "\n",
        _ => "\r"
    };

    /// <summary>Detects the dominant line ending in a text block.</summary>
    public static LineEnding Detect(string text)
    {
        var crlf = 0;
        var lf = 0;
        var cr = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    crlf++;
                    i++;
                }
                else
                {
                    cr++;
                }
            }
            else if (text[i] == '\n')
            {
                lf++;
            }
        }

        if (crlf == 0 && lf == 0 && cr == 0)
        {
            return Environment.NewLine == "\r\n" ? LineEnding.CrLf : LineEnding.Lf;
        }

        if (crlf >= lf && crlf >= cr)
        {
            return LineEnding.CrLf;
        }

        return lf >= cr ? LineEnding.Lf : LineEnding.Cr;
    }

    /// <summary>Converts every line break in <paramref name="text"/> to the requested sequence.</summary>
    public static string Normalize(string text, LineEnding lineEnding)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var sequence = lineEnding.ToSequence();
        return sequence == "\n" ? normalized : normalized.Replace("\n", sequence);
    }
}
