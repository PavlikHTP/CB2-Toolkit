namespace CB2Toolkit.Core.Models;

public sealed record ValidationResult(
    List<SyntaxError> Errors,
    IReadOnlyDictionary<int, string> TypeHints,
    IReadOnlyDictionary<int, int> UsageToDeclaration,
    IReadOnlyList<SymbolDeclaration> Symbols);
