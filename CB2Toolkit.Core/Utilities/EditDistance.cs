namespace CB2Toolkit.Core.Utilities;

public static class EditDistance
{
    public static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        int[] prev = new int[b.Length + 1];
        int[] curr = new int[b.Length + 1];

        for (int j = 0; j <= b.Length; j++) prev[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }

            (prev, curr) = (curr, prev);
        }

        return prev[b.Length];
    }

    public static int MaxDistance(int wordLength) => wordLength >= 5 ? 2 : 1;

    public static string? FindBestSuggestion(string word, List<string> candidates)
    {
        int maxDistance = MaxDistance(word.Length);
        string? best = null;
        int bestScore = int.MaxValue;

        foreach (var candidate in candidates)
        {
            if (candidate.Length < word.Length - 2 || candidate.Length > word.Length + 2) continue;
            if (string.Equals(candidate, word, StringComparison.OrdinalIgnoreCase)) continue;

            int distance = Levenshtein(word, candidate);
            if (distance > maxDistance) continue;

            int score = distance * 10 + Math.Abs(candidate.Length - word.Length);
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
            else if (score == bestScore && best != null)
            {
                if (PrefixMatchCount(word, candidate) > PrefixMatchCount(word, best))
                    best = candidate;
            }
        }

        return best;
    }

    private static int PrefixMatchCount(string a, string b)
    {
        int n = Math.Min(a.Length, b.Length);
        int count = 0;
        while (count < n && a[count] == b[count]) count++;
        return count;
    }
}
