using System.Globalization;
using System.Text;

namespace CB2Toolkit.Core.Utilities.Extensions;

public static class StringExtensions
{
    public static double ToDouble(this string val)
    {
        if (string.IsNullOrWhiteSpace(val)) return 0.0;
        string cleaned = val.Trim().TrimEnd('f', 'F');
        if (double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
            return result;
        return 0.0;
    }

    public static int ToInt(this string val, int defaultVal = 0)
    {
        if (string.IsNullOrWhiteSpace(val)) return defaultVal;
        string cleaned = val.Trim().TrimEnd('f', 'F');
        if (int.TryParse(cleaned, out int result)) return result;
        if (double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleResult))
            return (int)doubleResult;
        return defaultVal;
    }

    public static string Unquote(this string val)
    {
        if (string.IsNullOrWhiteSpace(val)) return string.Empty;
        return val.Trim().Trim('"');
    }

    public static string[] SplitArgs(this string argsLine)
    {
        if (string.IsNullOrWhiteSpace(argsLine)) return Array.Empty<string>();
        
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        foreach (char c in argsLine)
        {
            if (c == '\"') inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        result.Add(current.ToString());
        return result.ToArray();
    }
}