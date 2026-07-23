using System.Text;

namespace CB2Toolkit.Core.Utilities;

public static class CommandTokenizer
{
    public static (string commandName, string[] args, Dictionary<string, string?> flags) Parse(string rawInput)
    {
        var tokens = Tokenize(rawInput);
        if (tokens.Count == 0)
        {
            return (string.Empty, Array.Empty<string>(), new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase));
        }

        var commandName = tokens[0];
        var argsList = new List<string>();
        var flags = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        for (int i = 1; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.StartsWith("--"))
            {
                var flagContent = token[2..];
                var eqIndex = flagContent.IndexOf('=');
                if (eqIndex >= 0)
                {
                    flags[flagContent[..eqIndex]] = flagContent[(eqIndex + 1)..];
                }
                else
                {
                    flags[flagContent] = null;
                }
            }
            else if (token.StartsWith("-") && token.Length > 1)
            {
                var flagContent = token[1..];
                var eqIndex = flagContent.IndexOf('=');
                if (eqIndex >= 0)
                {
                    flags[flagContent[..eqIndex]] = flagContent[(eqIndex + 1)..];
                }
                else
                {
                    flags[flagContent] = null;
                }
            }
            else
            {
                argsList.Add(token);
            }
        }

        return (commandName, argsList.ToArray(), flags);
    }

    private static List<string> Tokenize(string input)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(input)) return result;

        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (sb.Length > 0)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        if (sb.Length > 0)
        {
            result.Add(sb.ToString());
        }

        return result;
    }
}