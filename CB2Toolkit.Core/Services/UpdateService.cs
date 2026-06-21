using System.Text.Json;

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
 
            string url = $"https://api.github.com/repos/{AppMetadata.GithubOwner}/{AppMetadata.GithubRepo}/releases";
            HttpResponseMessage response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            string json = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(json);
            
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var latestRelease = doc.RootElement[0];

                if (latestRelease.TryGetProperty("tag_name", out JsonElement tagProperty))
                {
                    string tagName = tagProperty.GetString() ?? string.Empty;
                    

                    string cleanTag = tagName.TrimStart('v', 'V'); 
                    string[] parts = cleanTag.Split('-');
                    string versionPart = parts[0];
                    bool isRemotePrerelease = parts.Length > 1; 

                    if (Version.TryParse(versionPart, out Version? latestVersion))
                    {
                        Version currentVersion = AppMetadata.CurrentVersion;

                        bool hasUpdate = false;
                        
                        if (latestVersion > currentVersion)
                        {
                            hasUpdate = true;
                        }
                        else if (latestVersion == currentVersion && !isRemotePrerelease)
                        {
                            hasUpdate = true; 
                        }

                        if (hasUpdate)
                        {
                            string downloadUrl = AppMetadata.GithubUrl;
                            if (latestRelease.TryGetProperty("html_url", out JsonElement urlProperty))
                            {
                                downloadUrl = urlProperty.GetString() ?? AppMetadata.GithubUrl;
                            }

                            OnUpdateAvailable?.Invoke(tagName, downloadUrl);
                        }
                    }
                }
            }
        }
        catch
        {
    
        }
    }
}