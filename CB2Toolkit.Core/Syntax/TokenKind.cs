namespace CB2Toolkit.Core.Syntax;

public enum TokenKind
{
    // Trivia — tokens ignored by syntax validation but kept for editors.
    Whitespace,
    LineComment,
    BlockComment,
    Preprocessor,

    // Names
    Identifier,

    // Keywords split into semantic groups
    Keyword,          // this, super, new, delete, cast, ...
    TypeKeyword,      // void, bool, int, uint, float, string, array, ...
    ControlKeyword,   // if, else, for, while, do, switch, case, default, break, continue, return
    DeclarationKeyword, // class, interface, enum, namespace, funcdef, import, ...
    ModifierKeyword,  // private, protected, public, const, final, override, in, out, ref, ...
    LiteralKeyword,   // true, false, null
    OperatorKeyword,  // and, or, xor, not, is, notis, typeid

    // Literals
    IntegerLiteral,
    FloatLiteral,
    StringLiteral,
    MultilineStringLiteral,
    CharacterLiteral,

    // Operators (text carries the actual operator, e.g. "==", "+=", "->")
    Operator,

    // Punctuation — dedicated kinds so validators/colorizers don't string-match
    OpenParen,
    CloseParen,
    OpenBracket,
    CloseBracket,
    OpenBrace,
    CloseBrace,
    Semicolon,
    Comma,
    Dot,
    Question,
    Colon,
    At,

    // Special
    Error,
    EndOfFile
}
