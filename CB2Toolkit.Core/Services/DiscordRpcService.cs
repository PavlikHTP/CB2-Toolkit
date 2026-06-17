using DiscordRPC;
using DiscordRPC.Logging;

namespace CB2Toolkit.Core.Services;

public static class DiscordRpcService
{
    private const string ClientId = "1514556474226114581";
    private const string LargeImageKey = "logo";
    private const string MenuDetails = "In Main Menu";
    private const string EditingDetailsFormat = "Editing {0}";
    private const string VersionStateFormat = "V {0}";
    private const string ImageTextFormat = "{0} v{1}";

    private static DiscordRpcClient? _client;
    private static Timestamps? _sessionTimestamps;

    public static void Initialize()
    {
        _client = new DiscordRpcClient(ClientId);
        _client.Logger = new ConsoleLogger { Level = LogLevel.Warning };
        _client.Initialize();

        _sessionTimestamps = Timestamps.Now;

        UpdateToMainMenu();
    }

    public static void UpdateToMainMenu()
    {
        if (_client == null || !_client.IsInitialized) return;

        _client.SetPresence(new RichPresence
        {
            Details = MenuDetails,
            State = string.Format(VersionStateFormat, AppMetadata.VersionString),
            Timestamps = _sessionTimestamps,
            Assets = new Assets
            {
                LargeImageKey = LargeImageKey,
                LargeImageText = string.Format(ImageTextFormat, AppMetadata.Title, AppMetadata.VersionString)
            }
        });
    }

    public static void UpdateToEditing(string fileName)
    {
        if (_client == null || !_client.IsInitialized) return;

        _client.SetPresence(new RichPresence
        {
            Details = string.Format(EditingDetailsFormat, fileName),
            State = string.Format(VersionStateFormat, AppMetadata.VersionString),
            Timestamps = _sessionTimestamps,
            Assets = new Assets
            {
                LargeImageKey = LargeImageKey,
                LargeImageText = string.Format(ImageTextFormat, AppMetadata.Title, AppMetadata.VersionString)
            }
        });
    }

    public static void Shutdown()
    {
        _client?.Dispose();
    }
}