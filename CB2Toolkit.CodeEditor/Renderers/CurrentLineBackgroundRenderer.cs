using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;

namespace CB2Toolkit.CodeEditor.Renderers;

public class CurrentLineBackgroundRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;

    private static readonly Brush CachedBrush;

    static CurrentLineBackgroundRenderer()
    {
        CachedBrush = new SolidColorBrush(Color.FromArgb(12, 255, 255, 255));
        CachedBrush.Freeze();
    }

    public CurrentLineBackgroundRenderer(TextEditor editor)
    {
        _editor = editor;
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_editor.Document == null || !textView.VisualLinesValid) return;

        var currentLine = _editor.Document.GetLineByOffset(_editor.CaretOffset);
        var visualLine = textView.GetVisualLine(currentLine.LineNumber);

        if (visualLine != null)
        {
            double y = visualLine.GetVisualPosition(0, VisualYPosition.TextTop).Y - textView.VerticalOffset;
            double height = visualLine.Height;

            drawingContext.DrawRectangle(CachedBrush, null, new Rect(0, y, textView.ActualWidth, height));
        }
    }
}
