using System.Text.RegularExpressions;
using CB2Toolkit.Core.Models;

namespace CB2Toolkit.Core.Services;

public class EditorSearchEngine
{
    public List<GlobalSearchResult> RunGlobalSearch(string projectDir, string textToFind, bool regexEnabled, bool wholeWordEnabled, bool matchCaseEnabled, IEnumerable<string> supportedExtensions)
    {
        var results = new List<GlobalSearchResult>();
        if (string.IsNullOrEmpty(projectDir) || string.IsNullOrEmpty(textToFind))
            return results;

        string pattern = regexEnabled ? textToFind : Regex.Escape(textToFind);
        if (wholeWordEnabled)
        {
            pattern = @"\b" + pattern + @"\b";
        }

        RegexOptions options = matchCaseEnabled ? RegexOptions.None : RegexOptions.IgnoreCase;
        var extensions = supportedExtensions.Select(e => e.ToLowerInvariant()).ToList();

        if (!Directory.Exists(projectDir))
            return results;

        var files = Directory.GetFiles(projectDir, "*.*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

        foreach (var file in files)
        {
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (Regex.IsMatch(lines[i], pattern, options))
                {
                    results.Add(new GlobalSearchResult
                    {
                        FilePath = file,
                        LineNumber = i + 1,
                        LineText = lines[i].Trim()
                    });
                }
            }
        }
        return results;
    }

    public int FindMatchOffset(string docText, string textToFind, int selectionStart, int selectionLength, bool backward, bool regexEnabled, bool wholeWordEnabled, bool matchCaseEnabled, out int matchLength)
    {
        matchLength = 0;
        if (string.IsNullOrEmpty(textToFind) || string.IsNullOrEmpty(docText))
            return -1;

        string pattern = regexEnabled ? textToFind : Regex.Escape(textToFind);
        if (wholeWordEnabled)
        {
            pattern = @"\b" + pattern + @"\b";
        }

        RegexOptions options = matchCaseEnabled ? RegexOptions.None : RegexOptions.IgnoreCase;

        try
        {
            var matches = Regex.Matches(docText, pattern, options);
            if (matches.Count == 0)
                return -1;

            Match? targetMatch = null;

            if (!backward)
            {
                int anchor = selectionStart + (selectionLength > 0 ? 1 : 0);
                foreach (Match m in matches)
                {
                    if (m.Index >= anchor)
                    {
                        targetMatch = m;
                        break;
                    }
                }
                if (targetMatch == null)
                    targetMatch = matches[0];
            }
            else
            {
                int anchor = selectionStart;
                for (int i = matches.Count - 1; i >= 0; i--)
                {
                    if (matches[i].Index < anchor)
                    {
                        targetMatch = matches[i];
                        break;
                    }
                }
                if (targetMatch == null)
                    targetMatch = matches[matches.Count - 1];
            }

            if (targetMatch != null)
            {
                matchLength = targetMatch.Length;
                return targetMatch.Index;
            }
        }
        catch
        {
        }

        return -1;
    }
}