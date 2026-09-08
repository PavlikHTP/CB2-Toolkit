using System.Windows;
using System.Windows.Media;
using CB2Toolkit.Core.Models;
using CB2Toolkit.Core.Models.Enums;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace CB2Toolkit.CodeEditor.Renderers;

public class ErrorColorizer : DocumentColorizingTransformer
{
    private static readonly TextDecorationCollection CachedUnderlineDecorations;
    private static readonly TextDecorationCollection WarningUnderlineDecorations;
    private static readonly TextDecorationCollection HintUnderlineDecorations;

    static ErrorColorizer()
    {
        var underline = new TextDecoration
        {
            Location = TextDecorationLocation.Underline,
            Pen = new Pen(Brushes.Red, 1.5)
            {
                DashStyle = DashStyles.Dot
            },
            PenOffset = 2.5,
            PenOffsetUnit = TextDecorationUnit.Pixel
        };

        var decorations = new TextDecorationCollection { underline };
        decorations.Freeze();
        CachedUnderlineDecorations = decorations;

        var warningUnderline = new TextDecoration
        {
            Location = TextDecorationLocation.Underline,
            Pen = new Pen(Brushes.Orange, 1.5)
            {
                DashStyle = DashStyles.Dot
            },
            PenOffset = 2.5,
            PenOffsetUnit = TextDecorationUnit.Pixel
        };

        var warningDecorations = new TextDecorationCollection { warningUnderline };
        warningDecorations.Freeze();
        WarningUnderlineDecorations = warningDecorations;

        var hintUnderline = new TextDecoration
        {
            Location = TextDecorationLocation.Underline,
            Pen = new Pen(Brushes.Gray, 1.2)
            {
                DashStyle = DashStyles.Dash
            },
            PenOffset = 2.5,
            PenOffsetUnit = TextDecorationUnit.Pixel
        };

        var hintDecorations = new TextDecorationCollection { hintUnderline };
        hintDecorations.Freeze();
        HintUnderlineDecorations = hintDecorations;

        ErrorBackground = new SolidColorBrush(Color.FromArgb(0x26, 0xF4, 0x87, 0x87));
        ErrorBackground.Freeze();

        WarningBackground = new SolidColorBrush(Color.FromArgb(0x2B, 0xF5, 0x9E, 0x0B));
        WarningBackground.Freeze();

        HintBackground = new SolidColorBrush(Color.FromArgb(0x18, 0x9C, 0xA3, 0xAF));
        HintBackground.Freeze();
    }

    private static readonly Brush ErrorBackground;
    private static readonly Brush WarningBackground;
    private static readonly Brush HintBackground;

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

            var severity = error.Severity;
            var decorations = severity switch
            {
                DiagnosticSeverity.Warning => WarningUnderlineDecorations,
                DiagnosticSeverity.Hint => HintUnderlineDecorations,
                _ => CachedUnderlineDecorations
            };
            var background = severity switch
            {
                DiagnosticSeverity.Warning => WarningBackground,
                DiagnosticSeverity.Hint => HintBackground,
                _ => ErrorBackground
            };

            ChangeLinePart(start, end, element =>
            {
                element.TextRunProperties.SetTextDecorations(decorations);
                element.TextRunProperties.SetBackgroundBrush(background);
            });
        }
    }
}
