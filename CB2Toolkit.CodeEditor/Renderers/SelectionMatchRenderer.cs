using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace CB2Toolkit.CodeEditor.Renderers;

public class SelectionMatchRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    private readonly List<ISegment> _matches = new();
    private readonly Brush _brush;

    public SelectionMatchRenderer(TextEditor editor)
    {
        _editor = editor;
        _editor.TextArea.SelectionChanged += TextArea_SelectionChanged;

        _brush = new SolidColorBrush(Color.FromArgb(0x3D, 0x4D, 0x7C, 0xFE));
        _brush.Freeze();
    }

    public KnownLayer Layer => KnownLayer.Background;

    private void TextArea_SelectionChanged(object? sender, EventArgs e)
    {
        _matches.Clear();

        var selection = _editor.TextArea.Selection;
        if (selection.IsEmpty)
        {
            _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
            return;
        }

        string selectedText = selection.GetText();

        if (string.IsNullOrWhiteSpace(selectedText) || selectedText.Length < 2)
        {
            _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
            return;
        }

        string docText = _editor.Text;
        int index = docText.IndexOf(selectedText, StringComparison.Ordinal);

        while (index != -1)
        {
            _matches.Add(new MatchSegment(index, selectedText.Length));
            index = docText.IndexOf(selectedText, index + selectedText.Length, StringComparison.Ordinal);
        }

        _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_matches.Count == 0 || textView == null || drawingContext == null || !textView.VisualLinesValid)
            return;

        var visualLines = textView.VisualLines;
        if (visualLines.Count == 0) return;

        int firstVisibleOffset = visualLines[0].FirstDocumentLine.Offset;
        int lastVisibleOffset = visualLines[^1].LastDocumentLine.EndOffset;

        foreach (var match in _matches)
        {
            if (match.EndOffset < firstVisibleOffset || match.Offset > lastVisibleOffset)
                continue;

            var builder = new BackgroundGeometryBuilder
            {
                AlignToWholePixels = true,
                CornerRadius = 3
            };

            builder.AddSegment(textView, match);
            
            var geometry = builder.CreateGeometry();
            if (geometry != null)
            {
                drawingContext.DrawGeometry(_brush, null, geometry);
            }
        }
    }

    private struct MatchSegment : ISegment
    {
        public int Offset { get; }
        public int Length { get; }
        public int EndOffset => Offset + Length;

        public MatchSegment(int offset, int length)
        {
            Offset = offset;
            Length = length;
        }
    }
}