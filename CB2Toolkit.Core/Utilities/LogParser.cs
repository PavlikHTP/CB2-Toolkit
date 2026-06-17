using System.Text.RegularExpressions;
using CB2Toolkit.Core.Models;
using CB2Toolkit.Core.Services;

namespace CB2Toolkit.Core.Utilities;

public static class LogParser
{
    public static LogNavigationTarget? ParseLogLine(string logText)
    {
        if (string.IsNullOrEmpty(logText)) return null;

        var fileMatch = Regex.Match(logText, @"^\[(.*?)\]");
        var lineMatch = Regex.Match(logText, @"\((\d+)(?:,\s*(\d+))?\)");

        if (!lineMatch.Success) return null;

        int line = int.Parse(lineMatch.Groups[1].Value);
        int col = lineMatch.Groups[2].Success ? int.Parse(lineMatch.Groups[2].Value) : 1;
        string filePath = string.Empty;

        if (fileMatch.Success)
        {
            filePath = fileMatch.Groups[1].Value;

            if (!Path.IsPathRooted(filePath) && !string.IsNullOrEmpty(ProjectService.Instance.CurrentFolderPath))
            {
                filePath = Path.GetFullPath(Path.Combine(ProjectService.Instance.CurrentFolderPath, filePath));
            }
        }

        return new LogNavigationTarget(filePath, line, col);
    }
}