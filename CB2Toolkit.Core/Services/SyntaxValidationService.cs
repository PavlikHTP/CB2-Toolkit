using System.Text;
using System.Text.RegularExpressions;
using CB2Toolkit.Core.Models;

namespace CB2Toolkit.Core.Services;

public class SyntaxValidationService
{
    private static readonly Lazy<SyntaxValidationService> _instance = new(() => new SyntaxValidationService());
    public static SyntaxValidationService Instance => _instance.Value;

    private SyntaxValidationService()
    {
    }

    public List<SyntaxError> Validate(List<(string Text, int Offset)> lines)
    {
        var errors = new List<SyntaxError>();
        if (lines == null || lines.Count == 0) return errors;

        var sb = new StringBuilder();
        var linePositions = new List<(int Start, int Length)>();
        
        foreach (var line in lines)
        {
            linePositions.Add((line.Offset, line.Text.Length));
            sb.Append(line.Text).Append('\n');
        }

        string fullText = sb.ToString();
        string maskedText = MaskCodeNoise(fullText);

        string[] maskedLines = maskedText.Split('\n');

        for (int i = 0; i < lines.Count; i++)
        {
            string originalLine = lines[i].Text;
            string maskedLine = maskedLines[i];
            int currentLineOffset = lines[i].Offset;

            string trimmedMasked = maskedLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(trimmedMasked)) continue;

            ValidateArgumentsInLine(maskedLine, currentLineOffset, errors);

            string cleanTrimmed = trimmedMasked.Trim();
            if (cleanTrimmed.EndsWith("{") || cleanTrimmed.EndsWith("}") || cleanTrimmed.EndsWith(":") || cleanTrimmed.EndsWith(";")) continue;
            if (cleanTrimmed.StartsWith("if") || cleanTrimmed.StartsWith("else") || cleanTrimmed.StartsWith("while") || cleanTrimmed.StartsWith("for") || cleanTrimmed.StartsWith("switch")) continue;
            if (cleanTrimmed.StartsWith("namespace") || cleanTrimmed.StartsWith("class") || cleanTrimmed.StartsWith("interface") || cleanTrimmed.StartsWith("enum") || cleanTrimmed.StartsWith("using") || cleanTrimmed.StartsWith("include") || cleanTrimmed.StartsWith("#")) continue;
            if (cleanTrimmed.StartsWith("[") && cleanTrimmed.EndsWith("]")) continue;

            char lastChar = cleanTrimmed[cleanTrimmed.Length - 1];
            if (lastChar == '+' || lastChar == '-' || lastChar == '*' || lastChar == '/' || lastChar == '%' ||
                lastChar == '=' || lastChar == '&' || lastChar == '|' || lastChar == '^' || lastChar == '?' ||
                lastChar == ',' || lastChar == '(' || lastChar == '[' || lastChar == '<' || lastChar == '>')
            {
                continue;
            }

            bool isFollowedByContinuation = false;
            for (int j = i + 1; j < lines.Count; j++)
            {
                string nextLineTrimmed = maskedLines[j].Trim();
                if (string.IsNullOrEmpty(nextLineTrimmed)) continue;

                if (nextLineTrimmed.StartsWith("{") || nextLineTrimmed.StartsWith(".") ||
                    nextLineTrimmed.StartsWith("+") || nextLineTrimmed.StartsWith("-") ||
                    nextLineTrimmed.StartsWith("*") || nextLineTrimmed.StartsWith("/") ||
                    nextLineTrimmed.StartsWith("&&") || nextLineTrimmed.StartsWith("||") ||
                    nextLineTrimmed.StartsWith(")") || nextLineTrimmed.StartsWith("]") ||
                    nextLineTrimmed.StartsWith("}"))
                {
                    isFollowedByContinuation = true;
                }
                break;
            }

            if (isFollowedByContinuation) continue;

            int lastCharIndex = originalLine.LastIndexOf(cleanTrimmed[cleanTrimmed.Length - 1]);
            if (lastCharIndex >= 0)
            {
                errors.Add(new SyntaxError
                {
                    Offset = currentLineOffset + lastCharIndex,
                    Length = 1,
                    Message = "Missing semicolon ';'"
                });
            }
        }

        return errors;
    }

    private string MaskCodeNoise(string text)
    {
        char[] buffer = text.ToCharArray();
        int i = 0;
        int length = buffer.Length;

        while (i < length)
        {
            if (i < length - 1 && buffer[i] == '/' && buffer[i + 1] == '/')
            {
                buffer[i] = ' ';
                buffer[i + 1] = ' ';
                i += 2;
                while (i < length && buffer[i] != '\n')
                {
                    buffer[i] = ' ';
                    i++;
                }
            }
            else if (i < length - 1 && buffer[i] == '/' && buffer[i + 1] == '*')
            {
                buffer[i] = ' ';
                buffer[i + 1] = ' ';
                i += 2;
                while (i < length)
                {
                    if (i < length - 1 && buffer[i] == '*' && buffer[i + 1] == '/')
                    {
                        buffer[i] = ' ';
                        buffer[i + 1] = ' ';
                        i += 2;
                        break;
                    }
                    if (buffer[i] != '\n')
                    {
                        buffer[i] = ' ';
                    }
                    i++;
                }
            }
            else if (buffer[i] == '"')
            {
                i++;
                while (i < length && buffer[i] != '"')
                {
                    if (buffer[i] == '\\' && i < length - 1)
                    {
                        if (buffer[i] != '\n') buffer[i] = ' ';
                        i++;
                    }
                    if (buffer[i] != '\n') buffer[i] = ' ';
                    i++;
                }
                i++;
            }
            else
            {
                i++;
            }
        }

        return new string(buffer);
    }

    private void ValidateArgumentsInLine(string maskedLine, int lineOffset, List<SyntaxError> errors)
    {
        string trimmed = maskedLine.Trim();
        if (trimmed.StartsWith("if") || trimmed.StartsWith("for") || trimmed.StartsWith("while") || trimmed.StartsWith("switch") || trimmed.StartsWith("include") || trimmed.StartsWith("#")) return;

        var matches = Regex.Matches(maskedLine, @"\(([^)]+)\)");
        foreach (Match match in matches)
        {
            string content = match.Groups[1].Value;
            if (Regex.IsMatch(content, @"\b\w+\s+\w+\s+\w+\b") || Regex.IsMatch(content, @"\b\w+\s+\w+(?!\s*,\s*)\s+\w+\s+\w+\b"))
            {
                errors.Add(new SyntaxError
                {
                    Offset = lineOffset + match.Groups[1].Index,
                    Length = match.Groups[1].Length,
                    Message = "Arguments must be separated by commas"
                });
            }
        }
    }
}