using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace CB2Toolkit.CodeEditor.Renderers;

public class IndentationGuideRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    private readonly Pen _pen;

    public IndentationGuideRenderer(TextEditor editor)
    {
        _editor = editor;
        var brush = new SolidColorBrush(Color.FromRgb(39, 39, 43));
        brush.Freeze();
        _pen = new Pen(brush, 1)
        {
            DashStyle = DashStyles.Dot
        };
        _pen.Freeze();
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (textView == null || drawingContext == null || !textView.VisualLinesValid)
            return;

        var visualLines = textView.VisualLines;
        if (visualLines.Count == 0) return;

        int indentSize = _editor.Options.IndentationSize;
        double spaceWidth = textView.WideSpaceWidth;

        foreach (var visualLine in visualLines)
        {
            var docLine = visualLine.FirstDocumentLine;
            if (docLine == null) continue;

            int currentIndent = GetResolvedIndent(docLine);
            int prevIndent = docLine.PreviousLine != null ? GetResolvedIndent(docLine.PreviousLine) : 0;
            int nextIndent = docLine.NextLine != null ? GetResolvedIndent(docLine.NextLine) : 0;

            Point zeroPos = visualLine.GetVisualPosition(1, VisualYPosition.LineTop);
            double yTop = visualLine.VisualTop - textView.ScrollOffset.Y;
            double yBottom = (visualLine.VisualTop + visualLine.Height) - textView.ScrollOffset.Y;

            for (int i = 1; i < currentIndent; i++)
            {
                if (i >= prevIndent && i >= nextIndent)
                    continue;

                int visualColumn = (i * indentSize) + 1;
                double x = zeroPos.X + (visualColumn - 1) * spaceWidth - textView.ScrollOffset.X;

                drawingContext.DrawLine(_pen, new Point(x, yTop), new Point(x, yBottom));
            }
        }
    }

    private int GetRawIndent(DocumentLine line)
    {
        if (line == null) return 0;
        string text = _editor.Document.GetText(line.Offset, line.Length);
        if (string.IsNullOrWhiteSpace(text)) return -1;

        int leadingSpaces = 0;
        int indentSize = _editor.Options.IndentationSize;
        foreach (char c in text)
        {
            if (c == ' ') leadingSpaces++;
            else if (c == '\t') leadingSpaces += indentSize;
            else break;
        }
        return leadingSpaces / indentSize;
    }

    private int GetResolvedIndent(DocumentLine line)
    {
        if (line == null) return 0;
        int raw = GetRawIndent(line);
        if (raw != -1) return raw;

        int prevIndent = 0;
        var p = line.PreviousLine;
        int count = 0;
        while (p != null && count < 50)
        {
            int r = GetRawIndent(p);
            if (r != -1) { prevIndent = r; break; }
            p = p.PreviousLine;
            count++;
        }

        int nextIndent = 0;
        var n = line.NextLine;
        count = 0;
        while (n != null && count < 50)
        {
            int r = GetRawIndent(n);
            if (r != -1) { nextIndent = r; break; }
            n = n.NextLine;
            count++;
        }

        return Math.Max(prevIndent, nextIndent);
    }
}