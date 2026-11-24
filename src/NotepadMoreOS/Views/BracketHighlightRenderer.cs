using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace NotepadMoreOS.Views;

internal sealed class BracketHighlightRenderer : IBackgroundRenderer
{
    public KnownLayer Layer => KnownLayer.Selection;
    public (int First, int Second)? Pair { get; set; }
    public IBrush Brush { get; set; } = Brushes.Transparent;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (Pair is not { } pair || !textView.VisualLinesValid) return;
        foreach (var offset in new[] { pair.First, pair.Second })
        {
            var builder = new BackgroundGeometryBuilder { CornerRadius = 2 };
            builder.AddSegment(textView, new TextSegment { StartOffset = offset, Length = 1 });
            var geometry = builder.CreateGeometry();
            if (geometry is not null) drawingContext.DrawGeometry(Brush, null, geometry);
        }
    }
}
