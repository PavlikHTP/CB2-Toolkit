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

        RegexOptions options = RegexOptions.Compiled | (matchCaseEnabled ? RegexOptions.None : RegexOptions.IgnoreCase);
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ext in supportedExtensions)
        {
            extensions.Add(ext);
        }

        if (!Directory.Exists(projectDir))
            return results;

        var regex = new Regex(pattern, options);

        string[] files;
        try
        {
            files = Directory.GetFiles(projectDir, "*.*", SearchOption.AllDirectories);
        }
        catch
        {
            return results;
        }

        foreach (var file in files)
        {
            if (!extensions.Contains(Path.GetExtension(file)))
                continue;

            try
            {
                using var reader = new StreamReader(file);
                int lineNumber = 0;
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    if (regex.IsMatch(line))
                    {
                        results.Add(new GlobalSearchResult
                        {
                            FilePath = file,
                            LineNumber = lineNumber,
                            LineText = line.Trim()
                        });
                    }
                }
            }
            catch
            {
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

        RegexOptions options = RegexOptions.Compiled | (matchCaseEnabled ? RegexOptions.None : RegexOptions.IgnoreCase);

        try
        {
            var regex = new Regex(pattern, options);

            if (!backward)
            {
                int anchor = selectionStart + (selectionLength > 0 ? 1 : 0);
                var match = regex.Match(docText, anchor);
                if (!match.Success)
                    match = regex.Match(docText);

                if (match.Success)
                {
                    matchLength = match.Length;
                    return match.Index;
                }
            }
            else
            {
                int anchor = selectionStart;
                Match? targetMatch = null;

                foreach (Match m in regex.Matches(docText))
                {
                    if (m.Index < anchor)
                    {
                        targetMatch = m;
                    }
                    else
                    {
                        break;
                    }
                }

                if (targetMatch == null)
                {
                    var lastMatch = regex.Match(docText);
                    if (lastMatch.Success)
                    {
                        foreach (Match m in regex.Matches(docText))
                        {
                            targetMatch = m;
                        }
                    }
                }

                if (targetMatch != null)
                {
                    matchLength = targetMatch.Length;
                    return targetMatch.Index;
                }
            }
        }
        catch
        {
        }

        return -1;
    }
}
