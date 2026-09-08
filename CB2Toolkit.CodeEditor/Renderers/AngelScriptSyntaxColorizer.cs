using System.Windows.Media;
using CB2Toolkit.Core.Syntax;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace CB2Toolkit.CodeEditor.Renderers;

public class AngelScriptSyntaxColorizer : DocumentColorizingTransformer
{
    private static readonly Brush OperatorBrush = CreateBrush("#C586C0");

    private readonly TextDocument _document;
    private LexResult? _cached;

    public AngelScriptSyntaxColorizer(TextDocument document)
    {
        _document = document;
        _document.TextChanged += (_, _) => _cached = null;
    }

    public void Invalidate() => _cached = null;

    protected override void ColorizeLine(DocumentLine line)
    {
        if (_cached == null)
        {
            _cached = AngelScriptLexer.Lex(_document.Text);
        }

        var tokens = _cached.Tokens;
        int lineStart = line.Offset;
        int lineEnd = line.EndOffset;

        int index = LowerBound(tokens, lineStart);
        for (; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (token.Offset >= lineEnd) break;
            if (token.Kind is not (TokenKind.Operator or TokenKind.Question or TokenKind.Colon)) continue;

            int start = Math.Max(lineStart, token.Offset);
            int end = Math.Min(lineEnd, token.Offset + token.Length);

            ChangeLinePart(start, end, e => e.TextRunProperties.SetForegroundBrush(OperatorBrush));
        }
    }

    private static int LowerBound(IReadOnlyList<Token> tokens, int offset)
    {
        int low = 0;
        int high = tokens.Count;

        while (low < high)
        {
            int mid = (low + high) / 2;
            if (tokens[mid].Offset < offset) low = mid + 1;
            else high = mid;
        }

        return low;
    }

    private static Brush CreateBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
