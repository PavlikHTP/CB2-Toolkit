using CB2Toolkit.Core.Models;
using CB2Toolkit.Core.Syntax;

namespace CB2Toolkit.Core.Services;

public class SyntaxValidationService
{
    private static readonly Lazy<SyntaxValidationService> _instance = new(() => new SyntaxValidationService());
    public static SyntaxValidationService Instance => _instance.Value;

    private SyntaxValidationService()
    {
    }

    public List<SyntaxError> Validate(string source) => Analyze(source).Errors;

    public ValidationResult Analyze(string source, SpellCheckContext? spellCheck = null)
    {
        var errors = new List<SyntaxError>();
        if (string.IsNullOrEmpty(source))
        {
            return new ValidationResult(errors, new Dictionary<int, string>(),
                new Dictionary<int, int>(), Array.Empty<SymbolDeclaration>());
        }

        var result = AngelScriptLexer.Lex(source);

        foreach (var lexError in result.Errors)
        {
            errors.Add(new SyntaxError
            {
                Offset = lexError.Offset,
                Length = Math.Max(1, lexError.Length),
                Message = lexError.Message
            });
        }

        ValidateBrackets(result.Tokens, errors);
        ValidateMissingSemicolons(result.Tokens, errors);
        ValidateDeclarations(result.Tokens, errors);
        ValidateParameterLists(result.Tokens, errors);
        ValidateReturns(result.Tokens, errors);

        var analysis = AngelScriptAnalyzer.Analyze(source, spellCheck);
        errors.AddRange(analysis.Warnings);
        errors.AddRange(analysis.Hints);

        return new ValidationResult(errors, analysis.TypeHints, analysis.UsageToDeclaration, analysis.Symbols);
    }

    private static void ValidateBrackets(IReadOnlyList<Token> tokens, List<SyntaxError> errors)
    {
        var stack = new Stack<(char Char, Token Token)>();

        foreach (var token in tokens)
        {
            TokenKind kind = token.Kind;
            if (kind is not (TokenKind.OpenParen or TokenKind.CloseParen
                or TokenKind.OpenBracket or TokenKind.CloseBracket
                or TokenKind.OpenBrace or TokenKind.CloseBrace)) continue;

            char c = kind switch
            {
                TokenKind.OpenParen => '(',
                TokenKind.CloseParen => ')',
                TokenKind.OpenBracket => '[',
                TokenKind.CloseBracket => ']',
                TokenKind.OpenBrace => '{',
                _ => '}'
            };

            switch (c)
            {
                case '(':
                case '[':
                case '{':
                    stack.Push((c, token));
                    break;
                default:
                {
                    char expected = c switch { ')' => '(', ']' => '[', _ => '{' };
                    if (stack.Count == 0)
                    {
                        errors.Add(new SyntaxError
                        {
                            Offset = token.Offset,
                            Length = 1,
                            Message = $"Unexpected '{c}'"
                        });
                    }
                    else if (stack.Peek().Char != expected)
                    {
                        var open = stack.Pop();
                        errors.Add(new SyntaxError
                        {
                            Offset = token.Offset,
                            Length = 1,
                            Message = $"Mismatched '{c}', expected '{CloseFor(open.Char)}'"
                        });
                    }
                    else
                    {
                        stack.Pop();
                    }
                    break;
                }
            }
        }

        foreach (var (ch, token) in stack)
        {
            errors.Add(new SyntaxError
            {
                Offset = token.Offset,
                Length = 1,
                Message = $"Unclosed '{ch}', expected '{CloseFor(ch)}'"
            });
        }
    }

    private static char CloseFor(char open) => open switch { '(' => ')', '[' => ']', _ => '}' };

    private static void ValidateMissingSemicolons(IReadOnlyList<Token> tokens, List<SyntaxError> errors)
    {
        var lines = tokens
            .Where(t => t.Kind is not TokenKind.Whitespace and not TokenKind.EndOfFile)
            .GroupBy(t => t.Line)
            .Select(g => g.OrderBy(t => t.Offset).ToList())
            .ToList();

        bool pendingEnum = false;
        var blockKinds = new Stack<BlockKind>();
        var parenStack = new Stack<bool>();
        Token? previous = null;

        for (int i = 0; i < lines.Count; i++)
        {
            var codeTokens = lines[i].Where(t => !t.IsComment).ToList();
            if (codeTokens.Count == 0) continue;

            bool inNoSemicolonBlock = false;
            int? headerCloseOffset = null;

            foreach (Token t in codeTokens)
            {
                if (t.Kind == TokenKind.DeclarationKeyword && t.Text == "enum")
                {
                    pendingEnum = true;
                }
                else if (t.Kind == TokenKind.OpenBrace)
                {
                    BlockKind kind;
                    if (pendingEnum) kind = BlockKind.Enum;
                    else if (previous != null &&
                        (previous.Value.Kind == TokenKind.Operator && previous.Value.Text == "=" ||
                         previous.Value.Kind == TokenKind.OpenBrace))
                    {
                        kind = BlockKind.Initializer;
                    }
                    else
                    {
                        kind = BlockKind.Code;
                    }

                    pendingEnum = false;
                    blockKinds.Push(kind);
                }
                else if (t.Kind == TokenKind.CloseBrace)
                {
                    pendingEnum = false;
                    if (blockKinds.Count > 0) blockKinds.Pop();
                }
                else if (t.Kind == TokenKind.Semicolon)
                {
                    pendingEnum = false;
                }
                else if (t.Kind == TokenKind.OpenParen)
                {
                    parenStack.Push(previous is { Kind: TokenKind.ControlKeyword });
                }
                else if (t.Kind == TokenKind.CloseParen && parenStack.Count > 0)
                {
                    bool isControlHeader = parenStack.Pop();
                    if (isControlHeader) headerCloseOffset = t.Offset;
                }

                if (pendingEnum || (blockKinds.Count > 0 && blockKinds.Peek() != BlockKind.Code))
                {
                    inNoSemicolonBlock = true;
                }

                previous = t;
            }

            if (inNoSemicolonBlock) continue;

            var last = codeTokens[^1];

            if (SkipLine(lines, i, codeTokens)) continue;

            if (headerCloseOffset.HasValue && headerCloseOffset.Value == last.Offset) continue;

            errors.Add(new SyntaxError
            {
                Offset = last.Offset,
                Length = last.Length,
                Message = "Missing ';'"
            });
        }
    }

    private enum BlockKind
    {
        Code,
        Enum,
        Initializer
    }

    private static bool SkipLine(List<List<Token>> lines, int index, List<Token> codeTokens)
    {
        var first = codeTokens[0];
        var last = codeTokens[^1];

        if (last.Text.Contains('\n')) return true;

        if (last.Kind is TokenKind.Error or TokenKind.Preprocessor) return true;

        if (last.Kind is TokenKind.Semicolon or TokenKind.OpenBrace or TokenKind.CloseBrace) return true;

        if (first.Kind is TokenKind.DeclarationKeyword or TokenKind.Preprocessor) return true;

        if (IsContinuation(last)) return true;

        if (last.Kind is TokenKind.StringLiteral or TokenKind.MultilineStringLiteral
            && NextLineStartsWith(lines, index, TokenKind.StringLiteral, TokenKind.MultilineStringLiteral))
            return true;

        if (IsHeaderOnly(codeTokens)) return true;

        if (last.Kind is TokenKind.CloseParen or TokenKind.CloseBracket
            && NextLineStartsWith(lines, index, TokenKind.OpenBrace))
            return true;

        if (last.Kind is TokenKind.CloseParen or TokenKind.CloseBracket
            && NextLineStartsWith(lines, index, TokenKind.Dot))
            return true;

        if (last.Kind is TokenKind.CloseParen or TokenKind.CloseBracket
            && NextLineStartsWith(lines, index, TokenKind.Semicolon))
            return true;

        if (NextLineStartsWithContinuation(lines, index)) return true;

        return false;
    }

    private static readonly HashSet<string> LineStartContinuationOperators = new(StringComparer.Ordinal)
    {
        "=", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=", ">>=",
        "==", "!=", "<", ">", "<=", ">=", "&&", "||", "<<", ">>",
        "+", "-", "*", "/", "%", "&", "|", "^"
    };

    private static bool NextLineStartsWithContinuation(List<List<Token>> lines, int index)
    {
        for (int j = index + 1; j < lines.Count; j++)
        {
            var first = FirstCodeToken(lines[j]);
            if (first is null) continue;

            if (first.Value.Kind == TokenKind.Operator)
            {
                return LineStartContinuationOperators.Contains(first.Value.Text);
            }

            return first.Value.Kind is TokenKind.Question or TokenKind.Colon
                or TokenKind.Dot or TokenKind.Comma
                or TokenKind.CloseParen or TokenKind.CloseBracket;
        }

        return false;
    }

    private static bool IsContinuation(Token last)
    {
        switch (last.Kind)
        {
            case TokenKind.Operator:
                return last.Text is not "++" and not "--";
            case TokenKind.Dot:
            case TokenKind.Comma:
            case TokenKind.OpenParen:
            case TokenKind.OpenBracket:
            case TokenKind.Question:
            case TokenKind.Colon:
            case TokenKind.At:
                return true;
            default:
                return false;
        }
    }

    private static bool IsHeaderOnly(IReadOnlyList<Token> codeTokens)
    {
        bool isControlHeader = codeTokens[0].Text is "if" or "for" or "while" or "switch" or "catch"
            || (codeTokens[0].Text == "else" && codeTokens.Count > 1 && codeTokens[1].Text == "if");

        if (isControlHeader)
        {
            if (codeTokens.Count == 1) return true;

            int depth = 0;
            for (int i = 0; i < codeTokens.Count; i++)
            {
                switch (codeTokens[i].Kind)
                {
                    case TokenKind.OpenParen:
                        depth++;
                        break;
                    case TokenKind.CloseParen:
                        depth--;
                        if (depth == 0) return i == codeTokens.Count - 1;
                        break;
                }
            }

            return false;
        }

        if (codeTokens[0].Text is "else" or "do")
            return codeTokens.Count == 1;

        return false;
    }

    private static bool NextLineStartsWith(List<List<Token>> lines, int index, params TokenKind[] kinds)
    {
        for (int j = index + 1; j < lines.Count; j++)
        {
            var first = FirstCodeToken(lines[j]);
            if (first is null) continue;
            return kinds.Contains(first.Value.Kind);
        }

        return false;
    }

    private static Token? FirstCodeToken(List<Token> tokens)
    {
        foreach (var t in tokens)
        {
            if (!t.IsComment) return t;
        }

        return null;
    }

    private static void ValidateDeclarations(IReadOnlyList<Token> tokens, List<SyntaxError> errors)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            Token t = tokens[i];
            if (t.IsTrivia || t.Kind == TokenKind.Preprocessor) continue;
            if (t.Kind != TokenKind.Semicolon) continue;

            bool hasType = false;
            bool hasName = false;
            bool hasOther = false;
            int start = -1;

            for (int j = i - 1; j >= 0; j--)
            {
                Token p = tokens[j];
                if (p.IsTrivia || p.Kind == TokenKind.Preprocessor) continue;
                if (p.Kind is TokenKind.Semicolon or TokenKind.OpenBrace or TokenKind.CloseBrace) break;
                start = j;
                if (p.Kind == TokenKind.TypeKeyword)
                {
                    hasType = true;
                }
                else if (p.Kind == TokenKind.Identifier)
                {
                    hasName = true;
                }
                else if (p.Kind == TokenKind.ModifierKeyword)
                {
                }
                else if (p.Kind == TokenKind.Operator && (p.Text == "<" || p.Text == ">" || p.Text == "&"))
                {
                }
                else if (p.Kind is TokenKind.OpenBracket or TokenKind.CloseBracket
                    or TokenKind.IntegerLiteral or TokenKind.FloatLiteral)
                {
                }
                else
                {
                    hasOther = true;
                }
            }

            if (start < 0 || !hasType || hasName || hasOther) continue;

            errors.Add(new SyntaxError
            {
                Offset = tokens[start].Offset,
                Length = 1,
                Message = "Expected identifier in declaration"
            });
        }
    }

    private static void ValidateParameterLists(IReadOnlyList<Token> tokens, List<SyntaxError> errors)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind != TokenKind.OpenParen) continue;

            int close = FindMatchingParen(tokens, i);
            if (close < 0) continue;

            int prev = PrevNonTrivia(tokens, i - 1);
            if (prev >= 0 && tokens[prev].IsKeyword) continue;

            int next = NextNonTrivia(tokens, close + 1);
            bool isHeader = next >= 0 && tokens[next].Kind == TokenKind.OpenBrace;

            CheckParamSeparators(tokens, errors, i + 1, close - 1, isHeader);
        }
    }

    private static void CheckParamSeparators(IReadOnlyList<Token> tokens, List<SyntaxError> errors, int start, int end, bool isHeader)
    {
        Token? last = null;
        for (int i = start; i <= end; i++)
        {
            Token t = tokens[i];
            if (t.IsTrivia || t.Kind == TokenKind.Preprocessor) continue;

            if (t.Kind == TokenKind.OpenParen)
            {
                int close = FindMatchingParen(tokens, i);
                if (close < 0 || close > end) break;
                i = close;
                last = null;
                continue;
            }

            if (last.HasValue)
            {
                bool a = last.Value.Kind == TokenKind.Identifier || last.Value.Kind == TokenKind.TypeKeyword;
                bool b = t.Kind == TokenKind.Identifier || t.Kind == TokenKind.TypeKeyword;
                if (a && b && (!isHeader || t.Kind == TokenKind.TypeKeyword))
                {
                    errors.Add(new SyntaxError
                    {
                        Offset = t.Offset,
                        Length = 1,
                        Message = "Expected ','"
                    });
                }
            }

            last = t;
        }
    }

    private static void ValidateReturns(IReadOnlyList<Token> tokens, List<SyntaxError> errors)
    {
        var scopes = new Stack<(int EntryDepth, bool IsVoid)>();
        int braceDepth = 0;

        for (int i = 0; i < tokens.Count; i++)
        {
            Token t = tokens[i];
            if (t.IsTrivia || t.Kind == TokenKind.Preprocessor) continue;

            if (t.Kind == TokenKind.OpenBrace)
            {
                braceDepth++;
                continue;
            }
            if (t.Kind == TokenKind.CloseBrace)
            {
                braceDepth--;
                if (scopes.Count > 0 && scopes.Peek().EntryDepth > braceDepth) scopes.Pop();
                continue;
            }
            if (t.Kind == TokenKind.Identifier && i + 1 < tokens.Count && tokens[i + 1].Kind == TokenKind.OpenParen)
            {
                int close = FindMatchingParen(tokens, i + 1);
                if (close < 0) continue;
                int next = NextNonTrivia(tokens, close + 1);
                if (next < 0 || tokens[next].Kind != TokenKind.OpenBrace) continue;

                bool isVoid = true;
                bool foundType = false;
                for (int j = i - 1; j >= 0; j--)
                {
                    Token p = tokens[j];
                    if (p.IsTrivia || p.Kind == TokenKind.Preprocessor) continue;
                    if (p.Kind is TokenKind.Semicolon or TokenKind.OpenBrace or TokenKind.CloseBrace or TokenKind.Colon) break;
                    if (p.Kind == TokenKind.TypeKeyword)
                    {
                        foundType = true;
                        isVoid = p.Text == "void";
                        break;
                    }
                    if (p.Kind == TokenKind.Identifier)
                    {
                        foundType = true;
                        isVoid = false;
                        break;
                    }
                    if (p.Kind is TokenKind.ModifierKeyword or TokenKind.At) continue;
                    if (p.Kind == TokenKind.Operator && p.Text is "<" or ">" or "&") continue;
                    if (p.Kind is TokenKind.OpenBracket or TokenKind.CloseBracket) continue;
                    foundType = true;
                    break;
                }

                scopes.Push((braceDepth + 1, !foundType || isVoid));
                continue;
            }
            if (t.Kind == TokenKind.ControlKeyword && t.Text == "return")
            {
                if (scopes.Count == 0) continue;
                int valueIndex = NextNonTrivia(tokens, i + 1);
                bool hasValue = valueIndex >= 0 && tokens[valueIndex].Kind != TokenKind.Semicolon;
                (_, bool isVoid) = scopes.Peek();
                if (isVoid && hasValue)
                {
                    errors.Add(new SyntaxError
                    {
                        Offset = t.Offset,
                        Length = t.Length,
                        Message = "Void function cannot return a value"
                    });
                }
                else if (!isVoid && !hasValue)
                {
                    errors.Add(new SyntaxError
                    {
                        Offset = t.Offset,
                        Length = t.Length,
                        Message = "Function must return a value"
                    });
                }
            }
        }
    }

    private static int FindMatchingParen(IReadOnlyList<Token> tokens, int openIndex)
    {
        int depth = 0;
        for (int i = openIndex; i < tokens.Count; i++)
        {
            if (tokens[i].Kind == TokenKind.OpenParen) depth++;
            else if (tokens[i].Kind == TokenKind.CloseParen)
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private static int NextNonTrivia(IReadOnlyList<Token> tokens, int index)
    {
        for (int i = index; i < tokens.Count; i++)
        {
            if (!tokens[i].IsTrivia && tokens[i].Kind != TokenKind.Preprocessor) return i;
        }
        return -1;
    }

    private static int PrevNonTrivia(IReadOnlyList<Token> tokens, int index)
    {
        for (int i = index; i >= 0; i--)
        {
            if (!tokens[i].IsTrivia && tokens[i].Kind != TokenKind.Preprocessor) return i;
        }
        return -1;
    }
}
