using System.Reflection;
using CB2Toolkit.Core.Utilities;

namespace CB2Toolkit.Core.Services;

public class SpellCheckDictionaryService
{
    public static SpellCheckDictionaryService Instance { get; } = new();

    public static string DictionariesFolder => Path.Combine(AppMetadata.AppDataFolder, "Dictionaries");

    private const int MaxDictionaryWords = 2_000_000;

    private readonly object _lock = new();
    private HashSet<string> _words = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, List<string>> _buckets = new();
    private bool _loaded;
    private bool _loadFailed;

    public bool Enabled { get; set; } = true;

    private static readonly string[] RuSuffixes =
    {
        "ами", "ями", "ого", "его", "ому", "ему", "ыми", "ими",
        "ая", "яя", "ое", "ее", "ые", "ие", "ой", "ый", "ий", "ей",
        "ов", "ев", "ам", "ям", "ах", "ях", "ом", "ем", "ыи", "ии",
        "ую", "юю", "а", "я", "ы", "и", "е", "у", "ю", "о", "ь"
    };

    private static readonly string[] EnSuffixes =
    {
        "ing", "ied", "ies", "tion", "ness", "ment", "ers", "est",
        "ed", "es", "ly", "er", "s"
    };

    private SpellCheckDictionaryService()
    {
    }

    public void EnsureDictionariesExist()
    {
        try
        {
            Directory.CreateDirectory(DictionariesFolder);
            EnsureDefaultFile("en.txt");
            EnsureDefaultFile("ru.txt");
        }
        catch
        {
        }
    }

    private void EnsureDefaultFile(string fileName)
    {
        string path = Path.Combine(DictionariesFolder, fileName);
        if (File.Exists(path)) return;

        string resourceName = $"CB2Toolkit.Core.Dictionaries.{fileName}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream == null) return;

        using var target = File.Create(path);
        stream.CopyTo(target);
    }

    private int _isLoading;

    public void Load()
    {
        if (Interlocked.CompareExchange(ref _isLoading, 1, 0) != 0) return;

        try
        {
            EnsureDictionariesExist();

            string[] files;
            try
            {
                files = Directory.GetFiles(DictionariesFolder, "*.txt");
            }
            catch
            {
                files = Array.Empty<string>();
            }

            var perFile = files.AsParallel()
                .WithDegreeOfParallelism(Math.Max(2, Environment.ProcessorCount))
                .Select(LoadFile)
                .Where(set => set != null)
                .ToList();

            var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var set in perFile)
            {
                words.UnionWith(set!);
            }

            if (words.Count > MaxDictionaryWords)
            {
                words = words.Take(MaxDictionaryWords).ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            var buckets = BuildBuckets(words);

            lock (_lock)
            {
                _words = words;
                _buckets = buckets;
                _loaded = true;
                _loadFailed = false;
            }
        }
        finally
        {
            _isLoading = 0;
        }
    }

    private static HashSet<string>? LoadFile(string path)
    {
        var words = new List<string>();
        try
        {
            string text = File.ReadAllText(path);

            if (text.Length > 4 * 1024 * 1024)
            {
                string[] chunks = SplitIntoChunks(text, Environment.ProcessorCount);
                var partials = chunks.AsParallel()
                    .WithDegreeOfParallelism(Environment.ProcessorCount)
                    .Select(ParseChunk)
                    .ToList();
                foreach (var partial in partials)
                {
                    words.AddRange(partial);
                }
            }
            else
            {
                ParseInto(text, words);
            }
        }
        catch
        {
            return null;
        }

        var set = new HashSet<string>(words, StringComparer.OrdinalIgnoreCase);
        return set;
    }

    private static string[] SplitIntoChunks(string text, int chunkCount)
    {
        int length = text.Length;
        if (chunkCount <= 1 || length <= 0) return new[] { text };

        var chunks = new string[chunkCount];
        int target = length / chunkCount;
        int start = 0;

        for (int i = 0; i < chunkCount - 1; i++)
        {
            int pos = Math.Min(start + target, length);
            while (pos < length && text[pos] != '\n') pos++;
            pos = pos < length ? pos + 1 : length;
            chunks[i] = text[start..pos];
            start = pos;
        }

        chunks[chunkCount - 1] = text[start..];
        return chunks;
    }

    private static List<string> ParseChunk(string chunk)
    {
        var words = new List<string>();
        ParseInto(chunk, words);
        return words;
    }

    private static void ParseInto(string text, List<string> words)
    {
        int start = 0;
        int i = 0;
        while (i <= text.Length)
        {
            if (i == text.Length || text[i] == '\n')
            {
                int end = i;
                if (end > start && text[end - 1] == '\r') end--;

                if (end - start >= 3)
                {
                    char first = text[start];
                    if (first != '#')
                    {
                        words.Add(text[start..end].ToLowerInvariant());
                    }
                }

                start = i + 1;
            }

            i++;
        }
    }

    private static Dictionary<string, List<string>> BuildBuckets(HashSet<string> words)
    {
        var buckets = new Dictionary<string, List<string>>();
        foreach (var word in words)
        {
            string key = BucketKey(word);
            if (!buckets.TryGetValue(key, out var list))
            {
                list = new List<string>();
                buckets[key] = list;
            }

            list.Add(word);
        }

        return buckets;
    }

    private static string BucketKey(string word)
    {
        int len = word.Length > 1 ? 2 : 1;
        return word[..len].ToLowerInvariant();
    }

    public bool IsReady => _loaded && !_loadFailed;

    public bool IsKnown(string word)
    {
        if (word.Length < 3) return true;
        if (!Enabled) return true;

        lock (_lock)
        {
            if (!_loaded) return true;
            return IsKnownInternal(word);
        }
    }

    private bool IsKnownInternal(string word)
    {
        string lower = word.ToLowerInvariant();

        if (_words.Contains(lower)) return true;

        foreach (string suffix in SuffixCandidates(lower))
        {
            if (lower.Length - suffix.Length < 3) continue;
            if (!lower.EndsWith(suffix, StringComparison.Ordinal)) continue;

            string stem = lower[..^suffix.Length];
            if (_words.Contains(stem)) return true;

            if (suffix is "ies" or "ied" && _words.Contains(stem + "y")) return true;
        }

        return false;
    }

    private static IEnumerable<string> SuffixCandidates(string lower)
    {
        foreach (string suffix in EnSuffixes)
        {
            if (lower.EndsWith(suffix, StringComparison.Ordinal)) yield return suffix;
        }

        foreach (string suffix in RuSuffixes)
        {
            if (lower.EndsWith(suffix, StringComparison.Ordinal)) yield return suffix;
        }
    }

    public string? FindSuggestion(string word)
    {
        if (word.Length < 3 || !Enabled) return null;

        lock (_lock)
        {
            if (!_loaded) return null;

            string lower = word.ToLowerInvariant();
            if (!_buckets.TryGetValue(BucketKey(lower), out var candidates)) return null;

            return EditDistance.FindBestSuggestion(lower, candidates);
        }
    }

    public bool AddCustomWord(string word)
    {
        string trimmed = word.Trim().ToLowerInvariant();
        if (trimmed.Length < 3) return false;

        foreach (char c in trimmed)
        {
            if (!char.IsLetter(c) && c != '-' && c != '\'') return false;
        }

        lock (_lock)
        {
            if (_loaded && _words.Contains(trimmed)) return false;

            _words.Add(trimmed);
            string key = BucketKey(trimmed);
            if (_buckets.TryGetValue(key, out var list))
                list.Add(trimmed);
            else
                _buckets[key] = new List<string> { trimmed };
        }

        try
        {
            Directory.CreateDirectory(DictionariesFolder);
            File.AppendAllText(Path.Combine(DictionariesFolder, "custom.txt"), trimmed + Environment.NewLine);
        }
        catch
        {
        }

        return true;
    }
}
