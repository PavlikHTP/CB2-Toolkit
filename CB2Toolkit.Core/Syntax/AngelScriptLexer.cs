namespace CB2Toolkit.Core.Syntax;

public sealed class AngelScriptLexer
{
    private static readonly Dictionary<string, TokenKind> Keywords = new(StringComparer.Ordinal)
    {
        ["new"] = TokenKind.Keyword,
        ["delete"] = TokenKind.Keyword,
        ["this"] = TokenKind.Keyword,
        ["super"] = TokenKind.Keyword,
        ["cast"] = TokenKind.Keyword,

        ["void"] = TokenKind.TypeKeyword,
        ["bool"] = TokenKind.TypeKeyword,
        ["int"] = TokenKind.TypeKeyword,
        ["int8"] = TokenKind.TypeKeyword,
        ["int16"] = TokenKind.TypeKeyword,
        ["int32"] = TokenKind.TypeKeyword,
        ["int64"] = TokenKind.TypeKeyword,
        ["uint"] = TokenKind.TypeKeyword,
        ["uint8"] = TokenKind.TypeKeyword,
        ["uint16"] = TokenKind.TypeKeyword,
        ["uint32"] = TokenKind.TypeKeyword,
        ["uint64"] = TokenKind.TypeKeyword,
        ["float"] = TokenKind.TypeKeyword,
        ["double"] = TokenKind.TypeKeyword,
        ["string"] = TokenKind.TypeKeyword,
        ["array"] = TokenKind.TypeKeyword,
        ["dictionary"] = TokenKind.TypeKeyword,
        ["any"] = TokenKind.TypeKeyword,
        ["auto"] = TokenKind.TypeKeyword,

        ["if"] = TokenKind.ControlKeyword,
        ["else"] = TokenKind.ControlKeyword,
        ["for"] = TokenKind.ControlKeyword,
        ["while"] = TokenKind.ControlKeyword,
        ["do"] = TokenKind.ControlKeyword,
        ["switch"] = TokenKind.ControlKeyword,
        ["case"] = TokenKind.ControlKeyword,
        ["default"] = TokenKind.ControlKeyword,
        ["break"] = TokenKind.ControlKeyword,
        ["continue"] = TokenKind.ControlKeyword,
        ["return"] = TokenKind.ControlKeyword,

        ["class"] = TokenKind.DeclarationKeyword,
        ["interface"] = TokenKind.DeclarationKeyword,
        ["enum"] = TokenKind.DeclarationKeyword,
        ["namespace"] = TokenKind.DeclarationKeyword,
        ["funcdef"] = TokenKind.DeclarationKeyword,
        ["typedef"] = TokenKind.DeclarationKeyword,
        ["import"] = TokenKind.DeclarationKeyword,
        ["from"] = TokenKind.DeclarationKeyword,
        ["using"] = TokenKind.DeclarationKeyword,

        ["private"] = TokenKind.ModifierKeyword,
        ["protected"] = TokenKind.ModifierKeyword,
        ["public"] = TokenKind.ModifierKeyword,
        ["const"] = TokenKind.ModifierKeyword,
        ["final"] = TokenKind.ModifierKeyword,
        ["override"] = TokenKind.ModifierKeyword,
        ["shared"] = TokenKind.ModifierKeyword,
        ["mixin"] = TokenKind.ModifierKeyword,
        ["external"] = TokenKind.ModifierKeyword,
        ["in"] = TokenKind.ModifierKeyword,
        ["out"] = TokenKind.ModifierKeyword,
        ["inout"] = TokenKind.ModifierKeyword,
        ["ref"] = TokenKind.ModifierKeyword,

        ["true"] = TokenKind.LiteralKeyword,
        ["false"] = TokenKind.LiteralKeyword,
        ["null"] = TokenKind.LiteralKeyword,

        ["and"] = TokenKind.OperatorKeyword,
        ["or"] = TokenKind.OperatorKeyword,
        ["xor"] = TokenKind.OperatorKeyword,
        ["not"] = TokenKind.OperatorKeyword,
        ["is"] = TokenKind.OperatorKeyword,
        ["notis"] = TokenKind.OperatorKeyword,
        ["typeid"] = TokenKind.OperatorKeyword
    };

    private static readonly string[] MultiCharOperators =
    {
        "...", "<<=", ">>=",
        "->", "::",
        "==", "!=", "<=", ">=", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=",
        "&&", "||", "++", "--", "<<", ">>"
    };

    private static readonly HashSet<char> SingleCharOperators = new()
    {
        '!', '~', '&', '|', '^', '+', '-', '*', '/', '%', '<', '>', '='
    };

    private readonly string _text;
    private readonly List<Token> _tokens = new();
    private readonly List<LexError> _errors = new();
    private int _pos;
    private int _line = 1;
    private int _column = 1;
    private bool _lineHasCode;
    private int _angleDepth;

    private AngelScriptLexer(string text)
    {
        _text = text;
    }

    public static LexResult Lex(string text)
    {
        AngelScriptLexer lexer = new(text ?? string.Empty);
        lexer.Scan();
        return new LexResult(lexer._tokens, lexer._errors);
    }

    public static bool IsKeyword(string text) => Keywords.ContainsKey(text);

    private void Scan()
    {
        if (_text.Length > 0 && _text[0] == '\uFEFF')
        {
            _pos = 1;
            _column = 2;
        }

        while (_pos < _text.Length)
        {
            char c = _text[_pos];

            if (c is ' ' or '\t' or '\r' or '\n')
            {
                ScanWhitespace();
            }
            else if (c == '/' && Peek(1) == '/')
            {
                ScanLineComment();
            }
            else if (c == '/' && Peek(1) == '*')
            {
                ScanBlockComment();
            }
            else if (c == '#')
            {
                if (_lineHasCode)
                {
                    ReportError(_pos, 1, _line, _column, "Unexpected character '#'");
                    AddToken(TokenKind.Error, _pos, _line, _column, 1);
                    MarkCode();
                    Advance();
                }
                else
                {
                    ScanPreprocessor();
                }
            }
            else if (c == '"')
            {
                if (Peek(1) == '"' && Peek(2) == '"')
                {
                    ScanMultilineString();
                }
                else
                {
                    ScanString('"');
                }
            }
            else if (c == '\'')
            {
                ScanString('\'');
            }
            else if (IsAsciiDigit(c) || (c == '.' && IsAsciiDigit(Peek(1))))
            {
                ScanNumber();
            }
            else if (IsAsciiLetter(c) || c == '_')
            {
                ScanIdentifier();
            }
            else if (TryScanOperator())
            {
            }
            else if (TryScanPunctuation())
            {
            }
            else
            {
                ReportError(_pos, 1, _line, _column, $"Unexpected character '{c}'");
                AddToken(TokenKind.Error, _pos, _line, _column, 1);
                MarkCode();
                Advance();
            }
        }

        AddToken(TokenKind.EndOfFile, _pos, _line, _column, 0);
    }

    private void MarkCode() => _lineHasCode = true;

    private void ScanWhitespace()
    {
        int start = _pos;
        int startLine = _line;
        int startColumn = _column;

        while (_pos < _text.Length && _text[_pos] is ' ' or '\t' or '\r' or '\n')
        {
            Advance();
        }

        if (_text.AsSpan(start, _pos - start).Contains('\n'))
        {
            _lineHasCode = false;
        }

        AddToken(TokenKind.Whitespace, start, startLine, startColumn, _pos - start);
    }

    private void ScanLineComment()
    {
        int start = _pos;
        int startLine = _line;
        int startColumn = _column;

        while (_pos < _text.Length && _text[_pos] != '\n')
        {
            Advance();
        }

        MarkCode();
        AddToken(TokenKind.LineComment, start, startLine, startColumn, _pos - start);
    }

    private void ScanBlockComment()
    {
        int start = _pos;
        int startLine = _line;
        int startColumn = _column;
        Advance();
        Advance();

        while (_pos < _text.Length && !(_text[_pos] == '*' && Peek(1) == '/'))
        {
            Advance();
        }

        bool terminated = _pos < _text.Length;
        if (terminated)
        {
            Advance();
            Advance();
        }
        else
        {
            ReportError(start, _pos - start, startLine, startColumn, "Unterminated block comment");
        }

        MarkCode();
        AddToken(terminated ? TokenKind.BlockComment : TokenKind.Error, start, startLine, startColumn, _pos - start);
    }

    private void ScanPreprocessor()
    {
        int start = _pos;
        int startLine = _line;
        int startColumn = _column;

        while (_pos < _text.Length && _text[_pos] is not (' ' or '\t' or '\r' or '\n'))
        {
            Advance();
        }

        bool isInclude = _pos - start == "#include".Length &&
                         string.CompareOrdinal(_text, start, "#include", 0, "#include".Length) == 0;

        if (!isInclude)
        {
            while (_pos < _text.Length && _text[_pos] != '\n')
            {
                Advance();
            }
        }

        MarkCode();
        AddToken(TokenKind.Preprocessor, start, startLine, startColumn, _pos - start);
    }

    private void ScanString(char quote)
    {
        int start = _pos;
        int startLine = _line;
        int startColumn = _column;
        Advance();

        bool terminated = false;
        while (_pos < _text.Length)
        {
            char c = _text[_pos];
            if (c == '\\')
            {
                Advance();
                if (_pos < _text.Length) Advance();
            }
            else if (c == quote)
            {
                Advance();
                terminated = true;
                break;
            }
            else if (c is '\r' or '\n')
            {
                break;
            }
            else
            {
                Advance();
            }
        }

        if (!terminated)
        {
            ReportError(start, _pos - start, startLine, startColumn,
                quote == '\'' ? "Unterminated character literal" : "Unterminated string literal");
        }

        TokenKind kind = !terminated
            ? TokenKind.Error
            : quote == '\'' ? TokenKind.CharacterLiteral : TokenKind.StringLiteral;

        MarkCode();
        AddToken(kind, start, startLine, startColumn, _pos - start);
    }

    private void ScanMultilineString()
    {
        int start = _pos;
        int startLine = _line;
        int startColumn = _column;
        Advance();
        Advance();
        Advance();

        bool terminated = false;
        while (_pos < _text.Length)
        {
            char c = _text[_pos];
            if (c == '\\')
            {
                Advance();
                if (_pos < _text.Length) Advance();
            }
            else if (c == '"' && Peek(1) == '"' && Peek(2) == '"')
            {
                Advance();
                Advance();
                Advance();
                terminated = true;
                break;
            }
            else
            {
                Advance();
            }
        }

        if (!terminated)
        {
            ReportError(start, _pos - start, startLine, startColumn, "Unterminated multiline string");
        }

        MarkCode();
        AddToken(terminated ? TokenKind.MultilineStringLiteral : TokenKind.Error, start, startLine, startColumn, _pos - start);
    }

    private void ScanNumber()
    {
        int start = _pos;
        int startLine = _line;
        int startColumn = _column;
        bool isFloat = false;

        if (_text[_pos] == '0' && (Peek(1) == 'x' || Peek(1) == 'X'))
        {
            Advance();
            Advance();
            if (!IsHexDigit(Peek()))
            {
                ReportError(start, _pos - start, startLine, startColumn, "Invalid hexadecimal literal");
            }
            while (IsHexDigit(Peek())) Advance();
        }
        else if (_text[_pos] == '0' && (Peek(1) == 'b' || Peek(1) == 'B'))
        {
            Advance();
            Advance();
            if (Peek() is not '0' and not '1')
            {
                ReportError(start, _pos - start, startLine, startColumn, "Invalid binary literal");
            }
            while (Peek() is '0' or '1') Advance();
        }
        else if (_text[_pos] == '0' && (Peek(1) == 'o' || Peek(1) == 'O'))
        {
            Advance();
            Advance();
            if (Peek() < '0' || Peek() > '7')
            {
                ReportError(start, _pos - start, startLine, startColumn, "Invalid octal literal");
            }
            while (Peek() >= '0' && Peek() <= '7') Advance();
        }
        else
        {
            while (IsAsciiDigit(Peek())) Advance();

            if (Peek() == '.' && Peek(1) != '.')
            {
                Advance();
                while (IsAsciiDigit(Peek())) Advance();
                isFloat = true;
            }

            if (IsExponentAt(_pos))
            {
                Advance();
                if (Peek() is '+' or '-') Advance();
                while (IsAsciiDigit(Peek())) Advance();
                isFloat = true;
            }
        }

        while (Peek() is 'f' or 'F' or 'u' or 'U' or 'l' or 'L')
        {
            if (Peek() is 'f' or 'F') isFloat = true;
            Advance();
        }

        if (Peek() == '_' || IsAsciiLetter(Peek()))
        {
            ReportError(start, _pos - start, startLine, startColumn, "Invalid numeric literal");
        }

        MarkCode();
        AddToken(isFloat ? TokenKind.FloatLiteral : TokenKind.IntegerLiteral, start, startLine, startColumn, _pos - start);
    }

    private static bool IsExponentAt(string text, int index)
    {
        if (index >= text.Length || (text[index] != 'e' && text[index] != 'E')) return false;
        index++;
        if (index < text.Length && text[index] is '+' or '-') index++;
        return index < text.Length && IsAsciiDigit(text[index]);
    }

    private bool IsExponentAt(int index) => IsExponentAt(_text, index);

    private static bool IsHexDigit(char c) => char.IsAsciiHexDigit(c);

    private static bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

    private static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';

    private static bool IsAsciiLetterOrDigit(char c) => IsAsciiLetter(c) || IsAsciiDigit(c);

    private void ScanIdentifier()
    {
        int start = _pos;
        int startLine = _line;
        int startColumn = _column;

        while (_pos < _text.Length && (IsAsciiLetterOrDigit(_text[_pos]) || _text[_pos] == '_'))
        {
            Advance();
        }

        string word = _text.Substring(start, _pos - start);
        MarkCode();
        AddToken(Keywords.TryGetValue(word, out TokenKind kind) ? kind : TokenKind.Identifier, start, startLine, startColumn, _pos - start);
    }

    private bool TryScanOperator()
    {
        if (_angleDepth > 0 && _text[_pos] == '>')
        {
            AddToken(TokenKind.Operator, _pos, _line, _column, 1);
            MarkCode();
            _angleDepth--;
            Advance();
            return true;
        }

        foreach (string op in MultiCharOperators)
        {
            if (_pos + op.Length <= _text.Length &&
                string.CompareOrdinal(_text, _pos, op, 0, op.Length) == 0)
            {
                AddToken(TokenKind.Operator, _pos, _line, _column, op.Length);
                MarkCode();
                Advance(op.Length);
                return true;
            }
        }

        if (SingleCharOperators.Contains(_text[_pos]))
        {
            char op = _text[_pos];
            AddToken(TokenKind.Operator, _pos, _line, _column, 1);
            MarkCode();
            if (op == '<') _angleDepth++;
            Advance();
            return true;
        }

        return false;
    }

    private bool TryScanPunctuation()
    {
        char c = _text[_pos];
        TokenKind? kind = c switch
        {
            '(' => TokenKind.OpenParen,
            ')' => TokenKind.CloseParen,
            '[' => TokenKind.OpenBracket,
            ']' => TokenKind.CloseBracket,
            '{' => TokenKind.OpenBrace,
            '}' => TokenKind.CloseBrace,
            ';' => TokenKind.Semicolon,
            ',' => TokenKind.Comma,
            '.' => TokenKind.Dot,
            '?' => TokenKind.Question,
            ':' => TokenKind.Colon,
            '@' => TokenKind.At,
            _ => null
        };

        if (kind is null) return false;

        AddToken(kind.Value, _pos, _line, _column, 1);
        MarkCode();
        Advance();
        return true;
    }

    private char Peek(int ahead = 0)
    {
        int index = _pos + ahead;
        return index < _text.Length ? _text[index] : '\0';
    }

    private void Advance(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Advance();
        }
    }

    private char Advance()
    {
        char c = _text[_pos++];
        if (c == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }
        return c;
    }

    private void ReportError(int offset, int length, int line, int column, string message)
    {
        _errors.Add(new LexError
        {
            Offset = offset,
            Length = length,
            Line = line,
            Column = column,
            Message = message
        });
    }

    private void AddToken(TokenKind kind, int offset, int line, int column, int length)
    {
        _tokens.Add(new Token(kind, _text.Substring(offset, length), offset, line, column));
    }
}
