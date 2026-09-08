using System.Text.RegularExpressions;
using CB2Toolkit.Core.Models;
using CB2Toolkit.Core.Models.Enums;

namespace CB2Toolkit.Core.Syntax;

public sealed class AngelScriptAnalyzer
{
    private static readonly Regex WordRegex = new(@"[A-Za-zА-Яа-яЁё]{3,}", RegexOptions.Compiled);

    public sealed record AnalyzeResult(
        List<SyntaxError> Warnings,
        IReadOnlyDictionary<int, string> TypeHints,
        IReadOnlyDictionary<int, int> UsageToDeclaration,
        IReadOnlyList<SymbolDeclaration> Symbols,
        List<SyntaxError> Hints);

    private sealed class ScopeBlock
    {
        public bool InFunction { get; set; }
        public bool IsClassBody { get; set; }
        public bool IsNamespace { get; set; }
        public string? ClassName { get; set; }
        public readonly List<LocalVar> Vars = new();
    }

    private sealed class LocalVar
    {
        public string Name = "";
        public int Offset;
        public int Length;
        public bool IsParam;
        public bool IsUsed;
    }

    private sealed class FieldVar
    {
        public string Name = "";
        public string ClassName = "";
        public int Offset;
        public int Length;
        public bool IsUsed;
    }

    public static AnalyzeResult Analyze(string source, SpellCheckContext? spellCheck = null)
    {
        var lex = AngelScriptLexer.Lex(source ?? string.Empty);
        List<Token> tokens = lex.Tokens
            .Where(t => !t.IsTrivia && t.Kind != TokenKind.Preprocessor && t.Kind != TokenKind.EndOfFile)
            .ToList();

        var warnings = new List<SyntaxError>();
        var hints = new List<SyntaxError>();
        var typeHints = new Dictionary<int, string>();
        var usageToDeclaration = new Dictionary<int, int>();
        var symbols = new List<SymbolDeclaration>();
        var allFields = new List<FieldVar>();
        var allVars = new List<LocalVar>();
        if (tokens.Count == 0)
            return new AnalyzeResult(warnings, typeHints, usageToDeclaration, symbols, hints);

        var blocks = new Stack<ScopeBlock>();
        var parenIsForHeader = new Stack<bool>();
        var declaredNameOffsets = new HashSet<int>();
        int parenDepth = 0;
        Token? prev = null;

        for (int i = 0; i < tokens.Count; i++)
        {
            Token t = tokens[i];

            switch (t.Kind)
            {
                case TokenKind.OpenParen:
                    parenIsForHeader.Push(prev is { Kind: TokenKind.ControlKeyword } && prev.Value.Text == "for");
                    parenDepth++;
                    break;
                case TokenKind.CloseParen:
                    if (parenDepth > 0)
                    {
                        parenDepth--;
                        parenIsForHeader.Pop();
                    }
                    break;
                case TokenKind.OpenBrace:
                    PushScope(tokens, i, prev, blocks, typeHints, symbols, allVars);
                    break;
                case TokenKind.CloseBrace:
                    if (blocks.Count > 0) AddWarnings(blocks.Pop(), warnings);
                    break;
                case TokenKind.DeclarationKeyword:
                    if (t.Text is "class" or "interface" &&
                        i + 1 < tokens.Count && tokens[i + 1].Kind == TokenKind.Identifier)
                    {
                        symbols.Add(new SymbolDeclaration(tokens[i + 1].Text, tokens[i + 1].Offset,
                            tokens[i + 1].Length, tokens[i + 1].Line, "class"));
                    }
                    break;
                case TokenKind.TypeKeyword:
                    TryScanDeclaration(tokens, i, prev, blocks, parenDepth, parenIsForHeader,
                        declaredNameOffsets, typeHints, symbols, allFields, allVars);
                    break;
                case TokenKind.Identifier:
                    ResolveUsage(tokens, i, prev, blocks, declaredNameOffsets, usageToDeclaration, allFields);
                    break;
            }

            prev = t;
        }

        while (blocks.Count > 0)
        {
            AddWarnings(blocks.Pop(), warnings);
        }

        foreach (var field in allFields)
        {
            if (field.IsUsed) continue;

            warnings.Add(new SyntaxError
            {
                Offset = field.Offset,
                Length = field.Length,
                Message = $"Field '{field.Name}' is declared but never used",
                Severity = DiagnosticSeverity.Warning
            });
        }

        if (spellCheck != null)
        {
            AddSpellCheckHints(tokens, declaredNameOffsets, allVars, symbols, spellCheck, hints, lex.Tokens);
        }

        return new AnalyzeResult(warnings, typeHints, usageToDeclaration, symbols, hints);
    }

    private static void AddWarnings(ScopeBlock block, List<SyntaxError> warnings)
    {
        foreach (var local in block.Vars)
        {
            if (local.IsParam || local.IsUsed) continue;

            warnings.Add(new SyntaxError
            {
                Offset = local.Offset,
                Length = local.Length,
                Message = $"Variable '{local.Name}' is declared but never used",
                Severity = DiagnosticSeverity.Warning
            });
        }
    }

    private static void PushScope(List<Token> tokens, int braceIndex, Token? prev, Stack<ScopeBlock> blocks,
        Dictionary<int, string> typeHints, List<SymbolDeclaration> symbols, List<LocalVar> allVars)
    {
        bool isFunctionBody = false;
        bool isClassBody = false;
        bool isNamespace = false;
        string? className = null;
        int functionOpenParen = -1;

        if (prev is { Kind: TokenKind.CloseParen })
        {
            functionOpenParen = FindMatchingOpenParen(tokens, braceIndex - 1);
            if (functionOpenParen > 0)
            {
                int k = functionOpenParen - 1;
                while (k >= 0 &&
                       (tokens[k].Kind == TokenKind.CloseBracket ||
                        (tokens[k].Kind == TokenKind.Operator && tokens[k].Text is ">" or ">>" or "&" or "::")))
                {
                    k--;
                }

                if (k >= 0 && tokens[k].Kind == TokenKind.Identifier)
                {
                    isFunctionBody = true;
                    symbols.Add(new SymbolDeclaration(tokens[k].Text, tokens[k].Offset,
                        tokens[k].Length, tokens[k].Line, "function"));
                }
            }
        }
        else if (prev is { Kind: TokenKind.Identifier } && prev.Value.Text is "get" or "set")
        {
            isFunctionBody = true;
        }
        else if (prev is { Kind: TokenKind.Identifier })
        {
            int k = braceIndex - 1;
            while (k >= 0 &&
                   (tokens[k].Kind == TokenKind.Identifier ||
                    (tokens[k].Kind == TokenKind.Operator && tokens[k].Text is ":" or "," or "<" or ">" or "&" or "::") ||
                    tokens[k].Kind is TokenKind.OpenBracket or TokenKind.CloseBracket or TokenKind.Colon))
            {
                k--;
            }

            if (k >= 0 && tokens[k].Kind == TokenKind.DeclarationKeyword)
            {
                if (tokens[k].Text is "class" or "interface")
                {
                    isClassBody = true;
                    for (int m = k + 1; m < braceIndex; m++)
                    {
                        if (tokens[m].Kind == TokenKind.Identifier)
                        {
                            className = tokens[m].Text;
                            break;
                        }
                    }
                }
                else if (tokens[k].Text == "namespace")
                {
                    isNamespace = true;
                }
            }
        }

        var block = new ScopeBlock
        {
            InFunction = isFunctionBody || (blocks.Count > 0 && blocks.Peek().InFunction),
            IsClassBody = isClassBody,
            IsNamespace = isNamespace,
            ClassName = className
        };

        if (isFunctionBody && functionOpenParen > 0)
        {
            ExtractParameters(tokens, functionOpenParen, braceIndex - 1, block, typeHints, allVars);
        }

        blocks.Push(block);
    }

    private static void ExtractParameters(List<Token> tokens, int openIndex, int closeIndex, ScopeBlock block,
        Dictionary<int, string> typeHints, List<LocalVar> allVars)
    {
        int segmentStart = openIndex + 1;
        int paren = 0;

        for (int i = openIndex + 1; i < closeIndex; i++)
        {
            Token t = tokens[i];
            if (t.Kind == TokenKind.OpenParen) { paren++; continue; }
            if (t.Kind == TokenKind.CloseParen) { paren--; continue; }
            if (t.Kind == TokenKind.Comma && paren == 0)
            {
                TryAddParam(tokens, segmentStart, i, block, typeHints, allVars);
                segmentStart = i + 1;
            }
        }

        TryAddParam(tokens, segmentStart, closeIndex, block, typeHints, allVars);
    }

    private static void TryAddParam(List<Token> tokens, int start, int end, ScopeBlock block,
        Dictionary<int, string> typeHints, List<LocalVar> allVars)
    {
        int paren = 0;
        int nameIndex = -1;
        bool hasType = false;

        for (int i = start; i < end; i++)
        {
            Token t = tokens[i];
            if (t.Kind == TokenKind.OpenParen) { paren++; continue; }
            if (t.Kind == TokenKind.CloseParen) { paren--; continue; }
            if (paren != 0) continue;
            if (t.Kind == TokenKind.TypeKeyword) hasType = true;
            if (t.Kind == TokenKind.Identifier) nameIndex = i;
        }

        if (nameIndex < 0 || !hasType) return;

        string type = string.Concat(tokens.GetRange(start, nameIndex - start).Select(x => x.Text));

        var param = new LocalVar
        {
            Name = tokens[nameIndex].Text,
            Offset = tokens[nameIndex].Offset,
            Length = tokens[nameIndex].Length,
            IsParam = true
        };
        block.Vars.Add(param);
        allVars.Add(param);
        typeHints[tokens[nameIndex].Offset] = type;
    }

    private static void TryScanDeclaration(List<Token> tokens, int i, Token? prev, Stack<ScopeBlock> blocks,
        int parenDepth, Stack<bool> parenIsForHeader, HashSet<int> declaredNameOffsets,
        Dictionary<int, string> typeHints, List<SymbolDeclaration> symbols, List<FieldVar> allFields,
        List<LocalVar> allVars)
    {
        if (blocks.Count == 0)
        {
            if (parenDepth > 0) return;

            bool positionOkTop = prev is null || prev.Value.Kind is TokenKind.Semicolon or TokenKind.OpenBrace
                or TokenKind.CloseBrace or TokenKind.ModifierKeyword;
            if (!positionOkTop) return;

            int jj = i + 1;
            while (jj < tokens.Count && tokens[jj].Kind == TokenKind.Identifier)
            {
                if (jj + 1 < tokens.Count && tokens[jj + 1].Kind == TokenKind.OpenParen) break;
                symbols.Add(new SymbolDeclaration(tokens[jj].Text, tokens[jj].Offset,
                    tokens[jj].Length, tokens[jj].Line, "global"));
                declaredNameOffsets.Add(tokens[jj].Offset);
                jj++;
                if (jj < tokens.Count && tokens[jj].Kind == TokenKind.Comma) { jj++; continue; }
                break;
            }

            return;
        }

        ScopeBlock top = blocks.Peek();
        bool inFunction = top.InFunction;
        bool inClassBody = top.IsClassBody && !inFunction;
        bool isGlobal = !inFunction && !inClassBody && (top.IsNamespace || blocks.Count == 1);

        if (!inFunction && !inClassBody && !isGlobal) return;

        if (parenDepth > 0 && (parenIsForHeader.Count == 0 || !parenIsForHeader.Peek())) return;

        bool positionOk = prev is null
            || prev.Value.Kind is TokenKind.Semicolon or TokenKind.OpenBrace or TokenKind.CloseBrace or TokenKind.ModifierKeyword
            || (prev.Value.Kind == TokenKind.OpenParen && parenDepth > 0 && parenIsForHeader.Count > 0 && parenIsForHeader.Peek());
        if (!positionOk) return;

        int j = i + 1;
        var typeParts = new List<string> { tokens[i].Text };
        int angle = 0;
        int bracket = 0;

        while (j < tokens.Count)
        {
            Token tk = tokens[j];
            if (tk.Kind == TokenKind.Operator && tk.Text == "<")
            {
                angle++;
                typeParts.Add(tk.Text);
                j++;
                continue;
            }

            if (tk.Kind == TokenKind.Operator && tk.Text == ">")
            {
                if (angle > 0) angle--;
                typeParts.Add(tk.Text);
                j++;
                continue;
            }

            if (angle > 0)
            {
                typeParts.Add(tk.Text);
                j++;
                continue;
            }

            if (tk.Kind == TokenKind.OpenBracket)
            {
                bracket++;
                typeParts.Add(tk.Text);
                j++;
                continue;
            }

            if (tk.Kind == TokenKind.CloseBracket)
            {
                if (bracket > 0) bracket--;
                typeParts.Add(tk.Text);
                j++;
                continue;
            }

            if (bracket > 0)
            {
                typeParts.Add(tk.Text);
                j++;
                continue;
            }

            if (tk.Kind == TokenKind.TypeKeyword)
            {
                typeParts.Add(tk.Text);
                j++;
                continue;
            }

            if (tk.Kind == TokenKind.Operator && tk.Text is "&" or "::")
            {
                typeParts.Add(tk.Text);
                j++;
                continue;
            }

            break;
        }

        string typeText = string.Concat(typeParts);

        while (j < tokens.Count && tokens[j].Kind == TokenKind.Identifier)
        {
            if (j + 1 < tokens.Count && tokens[j + 1].Kind == TokenKind.OpenParen) break;

            Token name = tokens[j];

            if (inClassBody)
            {
                allFields.Add(new FieldVar
                {
                    Name = name.Text,
                    ClassName = top.ClassName ?? "",
                    Offset = name.Offset,
                    Length = name.Length
                });
                symbols.Add(new SymbolDeclaration(name.Text, name.Offset, name.Length, name.Line, "field"));
            }
            else if (inFunction)
            {
                var local = new LocalVar
                {
                    Name = name.Text,
                    Offset = name.Offset,
                    Length = name.Length
                };
                top.Vars.Add(local);
                allVars.Add(local);
            }
            else if (isGlobal)
            {
                symbols.Add(new SymbolDeclaration(name.Text, name.Offset, name.Length, name.Line, "global"));
            }

            declaredNameOffsets.Add(name.Offset);
            typeHints[name.Offset] = typeText;
            j++;

            if (j < tokens.Count && tokens[j].Kind == TokenKind.Comma)
            {
                j++;
                continue;
            }

            break;
        }
    }

    private static void ResolveUsage(List<Token> tokens, int i, Token? prev, Stack<ScopeBlock> blocks,
        HashSet<int> declaredNameOffsets, Dictionary<int, int> usageToDeclaration, List<FieldVar> allFields)
    {
        Token t = tokens[i];
        if (declaredNameOffsets.Contains(t.Offset)) return;

        if (prev is { Kind: TokenKind.Dot })
        {
            MarkFieldsUsed(allFields, null, t.Text, true);
            return;
        }

        if (i + 1 < tokens.Count && tokens[i + 1].Kind == TokenKind.OpenParen) return;

        foreach (var block in blocks)
        {
            for (int v = block.Vars.Count - 1; v >= 0; v--)
            {
                LocalVar local = block.Vars[v];
                if (local.Name == t.Text && local.Offset < t.Offset)
                {
                    local.IsUsed = true;
                    usageToDeclaration[t.Offset] = local.Offset;
                    return;
                }
            }
        }

        MarkFieldsUsed(allFields, CurrentClassName(blocks), t.Text, false);
    }

    private static string? CurrentClassName(Stack<ScopeBlock> blocks)
    {
        foreach (var block in blocks)
        {
            if (block.ClassName != null) return block.ClassName;
        }

        return null;
    }

    private static void MarkFieldsUsed(List<FieldVar> allFields, string? className, string name, bool memberAccess)
    {
        foreach (var field in allFields)
        {
            if (field.IsUsed) continue;
            if (memberAccess || (className != null && field.ClassName == className))
            {
                if (field.Name == name) field.IsUsed = true;
            }
        }
    }

    private static void AddSpellCheckHints(IReadOnlyList<Token> codeTokens, HashSet<int> declaredNameOffsets,
        List<LocalVar> allVars, List<SymbolDeclaration> symbols, SpellCheckContext ctx, List<SyntaxError> hints,
        IReadOnlyList<Token> allTokens)
    {
        const int maxHints = 50;

        var symbolNames = new HashSet<string>(symbols.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);
        var localNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in allVars) localNames.Add(v.Name);

        if (ctx.CheckCode)
        {
            foreach (var t in codeTokens)
            {
                if (hints.Count >= maxHints) break;
                if (t.Kind != TokenKind.Identifier) continue;
                if (declaredNameOffsets.Contains(t.Offset)) continue;
                if (localNames.Contains(t.Text)) continue;
                if (symbolNames.Contains(t.Text)) continue;
                if (ctx.IsCodeKnown(t.Text)) continue;

                string? suggestion = ctx.CodeSuggestion(t.Text);
                if (string.IsNullOrEmpty(suggestion)) continue;

                hints.Add(new SyntaxError
                {
                    Offset = t.Offset,
                    Length = t.Length,
                    Message = $"Unknown identifier '{t.Text}'. Did you mean '{suggestion}'?",
                    Severity = DiagnosticSeverity.Hint
                });
            }
        }

        if (ctx.CheckText)
        {
            foreach (var t in allTokens)
            {
                if (hints.Count >= maxHints) break;
                if (t.Kind is not (TokenKind.LineComment or TokenKind.BlockComment
                    or TokenKind.StringLiteral or TokenKind.MultilineStringLiteral)) continue;

                string content = t.Text;
                int cutStart = 0;
                int cutEnd = 0;

                switch (t.Kind)
                {
                    case TokenKind.LineComment when content.StartsWith("//"):
                        cutStart = 2;
                        break;
                    case TokenKind.BlockComment when content.StartsWith("/*") && content.EndsWith("*/"):
                        cutStart = 2;
                        cutEnd = 2;
                        break;
                    case TokenKind.StringLiteral when content.Length >= 2:
                        cutStart = 1;
                        cutEnd = 1;
                        break;
                    case TokenKind.MultilineStringLiteral when content.Length >= 6:
                        cutStart = 3;
                        cutEnd = 3;
                        break;
                }

                if (cutStart + cutEnd >= content.Length) continue;

                string text = content.Substring(cutStart, content.Length - cutStart - cutEnd);

                foreach (Match m in WordRegex.Matches(text))
                {
                    if (hints.Count >= maxHints) break;

                    string word = m.Value;
                    if (ctx.IsWordKnown(word)) continue;

                    string? suggestion = ctx.WordSuggestion(word);
                    if (string.IsNullOrEmpty(suggestion)) continue;

                    hints.Add(new SyntaxError
                    {
                        Offset = t.Offset + cutStart + m.Index,
                        Length = word.Length,
                        Message = $"Possible typo: '{word}'. Did you mean '{suggestion}'?",
                        Severity = DiagnosticSeverity.Hint
                    });
                }
            }
        }
    }

    private static int FindMatchingOpenParen(List<Token> tokens, int closeIndex)
    {
        int depth = 0;
        for (int i = closeIndex; i >= 0; i--)
        {
            if (tokens[i].Kind == TokenKind.CloseParen) depth++;
            else if (tokens[i].Kind == TokenKind.OpenParen)
            {
                depth--;
                if (depth == 0) return i;
            }
        }

        return -1;
    }
}
