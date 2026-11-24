using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;

namespace NotepadMoreOS.Views;

internal sealed class IndentGuideRenderer : IBackgroundRenderer
{
    public KnownLayer Layer => KnownLayer.Background;
    public IPen Pen { get; set; } = new Pen(Brushes.Transparent);
    public int IndentSize { get; set; } = 4;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid || textView.Document is not { } document) return;
        var tabSize = Math.Max(1, IndentSize);
        foreach (var visual in textView.VisualLines)
        {
            var line = visual.FirstDocumentLine;
            var columns = 0;
            for (var i = 0; i < line.Length; i++)
            {
                var c = document.GetCharAt(line.Offset + i);
                if (c == ' ') columns++;
                else if (c == '\t') columns += tabSize - columns % tabSize;
                else break;
            }
            if (line.Length == 0) continue;
            var y = visual.VisualTop - textView.VerticalOffset;
            for (var col = tabSize; col <= columns; col += tabSize)
            {
                var x = col * textView.WideSpaceWidth - textView.HorizontalOffset;
                drawingContext.DrawLine(Pen, new Point(x, y), new Point(x, y + visual.Height));
            }
        }
    }
}
