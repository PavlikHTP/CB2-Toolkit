using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CB2Toolkit.Core.Services;

public class UpdateService
{
    private static readonly Lazy<UpdateService> _instance = new(() => new UpdateService());
    public static UpdateService Instance => _instance.Value;

    private readonly HttpClient _httpClient;

    public event Action<string, string>? OnUpdateAvailable;

    private UpdateService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CB2Toolkit-Updater");
    }

    public async Task CheckForUpdatesAsync()
    {
        try
        {
            string url = $"https://api.github.com/repos/{AppMetadata.GithubOwner}/{AppMetadata.GithubRepo}/releases/latest";
            HttpResponseMessage response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            string json = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("tag_name", out JsonElement tagProperty))
            {
                string tagName = tagProperty.GetString() ?? string.Empty;
                string cleanVersion = tagName.TrimStart('v', 'V').Split('-')[0];

                if (Version.TryParse(cleanVersion, out Version? latestVersion))
                {
                    Version currentVersion = AppMetadata.CurrentVersion;

                    if (latestVersion > currentVersion)
                    {
                        string downloadUrl = AppMetadata.GithubUrl;
                        if (root.TryGetProperty("html_url", out JsonElement urlProperty))
                        {
                            downloadUrl = urlProperty.GetString() ?? AppMetadata.GithubUrl;
                        }

                        OnUpdateAvailable?.Invoke(tagName, downloadUrl);
                    }
                }
            }
        }
        catch
        {
        }
    }
}