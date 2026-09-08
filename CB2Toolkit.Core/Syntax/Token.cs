namespace CB2Toolkit.Core.Syntax;

public readonly struct Token
{
    public TokenKind Kind { get; }
    public string Text { get; }
    public int Offset { get; }
    public int Line { get; }
    public int Column { get; }
    public int Length => Text.Length;

    public bool IsTrivia => Kind is TokenKind.Whitespace or TokenKind.LineComment or TokenKind.BlockComment;
    public bool IsComment => Kind is TokenKind.LineComment or TokenKind.BlockComment;
    public bool IsKeyword => Kind is TokenKind.Keyword
        or TokenKind.TypeKeyword
        or TokenKind.ControlKeyword
        or TokenKind.DeclarationKeyword
        or TokenKind.ModifierKeyword
        or TokenKind.LiteralKeyword
        or TokenKind.OperatorKeyword;

    public bool IsIdentifierOrKeyword => Kind is TokenKind.Identifier or TokenKind.Keyword
        or TokenKind.TypeKeyword
        or TokenKind.ControlKeyword
        or TokenKind.DeclarationKeyword
        or TokenKind.ModifierKeyword
        or TokenKind.LiteralKeyword
        or TokenKind.OperatorKeyword;

    public bool IsLiteral => Kind is TokenKind.IntegerLiteral
        or TokenKind.FloatLiteral
        or TokenKind.StringLiteral
        or TokenKind.MultilineStringLiteral
        or TokenKind.CharacterLiteral
        or TokenKind.LiteralKeyword;

    public bool IsOperator => Kind == TokenKind.Operator;

    public Token(TokenKind kind, string text, int offset, int line, int column)
    {
        Kind = kind;
        Text = text;
        Offset = offset;
        Line = line;
        Column = column;
    }

    public override string ToString() => $"{Kind} {Line}:{Column} '{Text}'";
}
