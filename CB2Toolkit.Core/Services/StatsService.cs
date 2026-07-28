using System.Net.Http.Json;

namespace CB2Toolkit.Core.Services;

public class StatsService
{
    private static readonly Lazy<StatsService> _instance = new(() => new StatsService());
    public static StatsService Instance => _instance.Value;

    private const string BaseUrl = "https://cb2toolkit-stats.pavel-kadomtsev.workers.dev";
    private readonly HttpClient _httpClient;

    private StatsService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(5);
    }

    public async Task PingAsync()
    {
        try
        {
            string id = AppMetadata.MachineId;
            string v = Uri.EscapeDataString(AppMetadata.VersionString);
            string os = Uri.EscapeDataString(Environment.OSVersion.ToString());

            using var response = await _httpClient.GetAsync($"{BaseUrl}/ping?id={id}&v={v}&os={os}");
        }
        catch
        {
        }
    }

    public async Task<int> GetTotalUsersAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<StatsResponse>($"{BaseUrl}/stats");
            return result?.TotalUsers ?? 0;
        }
        catch
        {
            return -1;
        }
    }

    private record StatsResponse(int TotalUsers);
}