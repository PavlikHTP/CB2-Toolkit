using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Rendering;

namespace CB2Toolkit.CodeEditor.Syntax;

public class ArrowFoldingMargin : AbstractMargin
{
    private readonly FoldingManager _manager;

    private static readonly Brush CachedFoldedBrush;
    private static readonly Brush CachedUnfoldedBrush;

    static ArrowFoldingMargin()
    {
        CachedFoldedBrush = new SolidColorBrush(Color.FromRgb(206, 126, 59));
        CachedFoldedBrush.Freeze();

        CachedUnfoldedBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160));
        CachedUnfoldedBrush.Freeze();
    }

    public ArrowFoldingMargin(FoldingManager manager)
    {
        _manager = manager;
    }

    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        if (oldTextView != null) oldTextView.VisualLinesChanged -= TextViewVisualLinesChanged;
        base.OnTextViewChanged(oldTextView, newTextView);
        if (newTextView != null) newTextView.VisualLinesChanged += TextViewVisualLinesChanged;
        InvalidateMeasure();
    }

    private void TextViewVisualLinesChanged(object sender, EventArgs e)
    {
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(16, 0);
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        InvalidateVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (TextView == null || !TextView.VisualLinesValid || _manager == null) return;

        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, RenderSize.Width, TextView.ActualHeight));

        var foldings = _manager.AllFoldings;

        foreach (var visualLine in TextView.VisualLines)
        {
            int lineStart = visualLine.FirstDocumentLine.Offset;
            int lineEnd = visualLine.LastDocumentLine.EndOffset;

            foreach (var folding in foldings)
            {
                if (folding.StartOffset >= lineStart && folding.StartOffset <= lineEnd)
                {
                    if (!folding.IsFolded && !IsMouseOver) continue;

                    double y = visualLine.GetVisualPosition(0, VisualYPosition.TextTop).Y - TextView.VerticalOffset;
                    double height = visualLine.Height;
                    double centerY = y + height / 2;
                    double centerX = 8;

                    Brush brush = folding.IsFolded ? CachedFoldedBrush : CachedUnfoldedBrush;

                    var geometry = new StreamGeometry();
                    using (var ctx = geometry.Open())
                    {
                        if (folding.IsFolded)
                        {
                            ctx.BeginFigure(new Point(centerX - 2, centerY - 4), true, true);
                            ctx.LineTo(new Point(centerX + 3, centerY), true, false);
                            ctx.LineTo(new Point(centerX - 2, centerY + 4), true, false);
                        }
                        else
                        {
                            ctx.BeginFigure(new Point(centerX - 4, centerY - 2), true, true);
                            ctx.LineTo(new Point(centerX + 4, centerY - 2), true, false);
                            ctx.LineTo(new Point(centerX, centerY + 2), true, false);
                        }
                    }
                    geometry.Freeze();
                    drawingContext.DrawGeometry(brush, null, geometry);
                }
            }
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (TextView == null || !TextView.VisualLinesValid || _manager == null) return;

        double visualY = e.GetPosition(TextView).Y + TextView.VerticalOffset;
        var visualLine = TextView.GetVisualLineFromVisualTop(visualY);
        if (visualLine != null)
        {
            int lineStart = visualLine.FirstDocumentLine.Offset;
            int lineEnd = visualLine.LastDocumentLine.EndOffset;

            foreach (var folding in _manager.AllFoldings)
            {
                if (folding.StartOffset >= lineStart && folding.StartOffset <= lineEnd)
                {
                    folding.IsFolded = !folding.IsFolded;
                    e.Handled = true;
                    TextView.InvalidateVisual();
                    break;
                }
            }
        }
    }
}
