using System.Windows;
using System.Windows.Media;
using CB2Toolkit.Core.Models;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace CB2Toolkit.CodeEditor.Renderers;

public class ErrorColorizer : DocumentColorizingTransformer
{
    public List<SyntaxError> Errors { get; set; } = new List<SyntaxError>();

    protected override void ColorizeLine(DocumentLine line)
    {
        if (Errors == null || Errors.Count == 0) return;

        int lineStart = line.Offset;
        int lineEnd = line.EndOffset;

        foreach (var error in Errors)
        {
            if (error.Offset + error.Length < lineStart || error.Offset > lineEnd) continue;

            int start = Math.Max(lineStart, error.Offset);
            int end = Math.Min(lineEnd, error.Offset + error.Length);

            if (start >= end) continue;

            ChangeLinePart(start, end, element =>
            {
                var underline = new TextDecoration
                {
                    Location = TextDecorationLocation.Underline,
                    Pen = new Pen(Brushes.Red, 1.0) 
                    { 
                        DashStyle = DashStyles.Dot 
                    },
                    PenOffset = 2.5,
                    PenOffsetUnit = TextDecorationUnit.Pixel
                };
                
                element.TextRunProperties.SetTextDecorations(new TextDecorationCollection { underline });
            });
        }
    }
}