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
        if (!textView.VisualLinesValid)
            return;

        var visualLines = textView.VisualLines;
        if (visualLines.Count == 0) return;

        int indentSize = _editor.Options.IndentationSize;
        double spaceWidth = textView.WideSpaceWidth;

        foreach (var visualLine in visualLines)
        {
            var docLine = visualLine.FirstDocumentLine;
            if (docLine == null) continue;

            int effectiveIndent = GetEffectiveIndent(docLine);
            effectiveIndent = GetStructuralIndent(docLine, effectiveIndent);
            
            if (effectiveIndent <= 0) continue;

            Point zeroPos = visualLine.GetVisualPosition(1, VisualYPosition.LineTop);
            double yTop = visualLine.VisualTop - textView.ScrollOffset.Y;
            double yBottom = (visualLine.VisualTop + visualLine.Height) - textView.ScrollOffset.Y;

            for (int i = 1; i < effectiveIndent; i++)
            {
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
            if (c == ' ' || c == '\u00A0') leadingSpaces++;
            else if (c == '\t') leadingSpaces += indentSize;
            else if (char.IsWhiteSpace(c)) leadingSpaces++;
            else break;
        }
        return leadingSpaces / indentSize;
    }

    private int GetEffectiveIndent(DocumentLine line)
    {
        if (line == null) return 0;

        int raw = GetRawIndent(line);
        if (raw != -1) return raw;

        int count = 0;
        var next = line.NextLine;
        while (next != null && count < 100)
        {
            int nextRaw = GetRawIndent(next);
            if (nextRaw != -1) return nextRaw;
            next = next.NextLine;
            count++;
        }

        count = 0;
        var prev = line.PreviousLine;
        while (prev != null && count < 100)
        {
            int prevRaw = GetRawIndent(prev);
            if (prevRaw != -1) return prevRaw;
            prev = prev.PreviousLine;
            count++;
        }

        return 0;
    }

    private int GetStructuralIndent(DocumentLine line, int effectiveIndent)
    {
        if (line == null) return 0;

        var prev = line.PreviousLine;
        while (prev != null)
        {
            int prevRaw = GetRawIndent(prev);
            if (prevRaw != -1)
            {
                if (prevRaw < effectiveIndent)
                {
                    return prevRaw + 1;
                }
            }
            prev = prev.PreviousLine;
        }
        return effectiveIndent;
    }
}