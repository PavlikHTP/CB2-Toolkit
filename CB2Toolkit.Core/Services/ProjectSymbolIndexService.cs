using CB2Toolkit.Core.Syntax;
using CB2Toolkit.Core.Utilities;

namespace CB2Toolkit.Core.Services;

public class ProjectSymbolIndexService
{
    public static ProjectSymbolIndexService Instance { get; } = new();

    private readonly object _lock = new();
    private HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<char, List<string>> _buckets = new();
    private bool _hasIndex;

    public bool HasIndex
    {
        get
        {
            lock (_lock) return _hasIndex;
        }
    }

    private ProjectSymbolIndexService()
    {
    }

    public void Rebuild(string projectDir)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(projectDir) && Directory.Exists(projectDir))
        {
            try
            {
                foreach (string file in Directory.EnumerateFiles(projectDir, "*", SearchOption.AllDirectories))
                {
                    if (!AppMetadata.SupportedExtensions.Contains(Path.GetExtension(file))) continue;
                    CollectIdentifiers(file, names);
                }
            }
            catch
            {
            }
        }

        lock (_lock)
        {
            _names = names;
            _buckets = BuildBuckets(names);
            _hasIndex = true;
        }
    }

    public void AddFile(string filePath)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectIdentifiers(filePath, names);
        if (names.Count == 0) return;

        lock (_lock)
        {
            foreach (var name in names) _names.Add(name);
            _buckets = BuildBuckets(_names);
        }
    }

    public void RemoveFile(string filePath)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectIdentifiers(filePath, names);
        if (names.Count == 0) return;

        lock (_lock)
        {
            foreach (var name in names) _names.Remove(name);
            _buckets = BuildBuckets(_names);
        }
    }

    public void RenameFile(string oldPath, string newPath)
    {
        RemoveFile(oldPath);
        AddFile(newPath);
    }

    private static void CollectIdentifiers(string filePath, HashSet<string> names)
    {
        try
        {
            string text = File.ReadAllText(filePath);
            var lex = AngelScriptLexer.Lex(text);
            foreach (var token in lex.Tokens)
            {
                if (token.Kind != TokenKind.Identifier || token.Text.Length < 3) continue;
                names.Add(token.Text);
            }
        }
        catch
        {
        }
    }

    private static Dictionary<char, List<string>> BuildBuckets(HashSet<string> names)
    {
        var buckets = new Dictionary<char, List<string>>();
        foreach (var name in names)
        {
            char key = char.ToLowerInvariant(name[0]);
            if (!buckets.TryGetValue(key, out var list))
            {
                list = new List<string>();
                buckets[key] = list;
            }

            list.Add(name);
        }

        return buckets;
    }

    public bool IsKnown(string name)
    {
        lock (_lock)
        {
            return !_hasIndex || _names.Contains(name);
        }
    }

    public string? FindSuggestion(string name)
    {
        if (name.Length < 3) return null;

        lock (_lock)
        {
            if (!_hasIndex) return null;
            string lower = name.ToLowerInvariant();
            if (!_buckets.TryGetValue(lower[0], out var candidates)) return null;

            return EditDistance.FindBestSuggestion(lower, candidates);
        }
    }
}
