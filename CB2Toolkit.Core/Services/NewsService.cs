using System.Text.RegularExpressions;
using CB2Toolkit.Core.Models;
using CB2Toolkit.Core.Models.Enums;
using CB2Toolkit.Core.Models.Settings;

namespace CB2Toolkit.Core.Services;

public partial class NewsService
{
    private static readonly HttpClient _httpClient = new();

    [GeneratedRegex(@"\[NEWS_BLOCK\](.*?)\[/NEWS_BLOCK\]", RegexOptions.Singleline)]
    private static partial Regex NewsBlockMatcher();

    [GeneratedRegex(@"\[(?<tag>TAG|DATE|TITLE|PREVIEW|CONTENT|VERSION)\](?<content>.*?)\[/\k<tag>\]", RegexOptions.Singleline)]
    private static partial Regex NewsTagMatcher();

    public async Task<List<NewsItem>> FetchNewsAsync(AppSettings settings)
    {
        LoggerService.Instance.LogDebug($"News fetch started, priority {settings.FetchPriority}");
        string primaryUrl = settings.FetchPriority == FetchPrioritySource.GitHub ? settings.GitHubNewsUrl : settings.PastebinNewsUrl;
        string secondaryUrl = settings.FetchPriority == FetchPrioritySource.GitHub ? settings.PastebinNewsUrl : settings.GitHubNewsUrl;

        string rawText = await TryFetchWithRetriesAsync(primaryUrl, 3);

        if (string.IsNullOrEmpty(rawText))
        {
            LoggerService.Instance.LogWarn($"News primary source failed, trying secondary");
            rawText = await TryFetchWithRetriesAsync(secondaryUrl, 3);
        }

        if (string.IsNullOrEmpty(rawText))
        {
            LoggerService.Instance.LogError($"News fetch failed for both sources");
            return new List<NewsItem>();
        }

        var parsed = ParseNews(rawText);
        LoggerService.Instance.LogDebug($"News fetch done, items: {parsed.Count}");
        return parsed;
    }

    private async Task<string> TryFetchWithRetriesAsync(string url, int maxRetries)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await _httpClient.GetStringAsync(url);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogDebug($"News fetch attempt {i + 1}/{maxRetries} failed for {url}: {ex.Message}");
                if (i < maxRetries - 1)
                {
                    await Task.Delay(500);
                }
            }
        }
        return string.Empty;
    }

    private List<NewsItem> ParseNews(string rawText)
    {
        var list = new List<NewsItem>();
        if (string.IsNullOrWhiteSpace(rawText)) return list;

        var blocks = NewsBlockMatcher().Matches(rawText);

        foreach (Match block in blocks)
        {
            string contentBlock = block.Groups[1].Value;

            string tag = string.Empty;
            string date = string.Empty;
            string title = string.Empty;
            string preview = string.Empty;
            string content = string.Empty;
            string version = "1.0.0";

            var matches = NewsTagMatcher().Matches(contentBlock);

            foreach (Match match in matches)
            {
                string tagName = match.Groups["tag"].Value;
                string value = match.Groups["content"].Value.Trim();

                switch (tagName)
                {
                    case "TAG": tag = value; break;
                    case "DATE": date = value; break;
                    case "TITLE": title = value; break;
                    case "PREVIEW": preview = value; break;
                    case "CONTENT": content = value; break;
                    case "VERSION": version = value; break;
                }
            }

            if (!string.IsNullOrEmpty(tag) && !string.IsNullOrEmpty(title))
            {
                list.Add(new NewsItem(tag, date, title, preview, content, version));
            }
        }

        return list;
    }
}