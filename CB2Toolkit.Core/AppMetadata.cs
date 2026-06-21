using System.Reflection;

namespace CB2Toolkit.Core;

public static class AppMetadata
{
    public static string Title => "CB2 Toolkit";
    
    public static string VersionString => 
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "1.0.0";
    
    public static Version CurrentVersion  => GetSafeVersion();

    public static string AppDataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        Title
    );

    public static long AddonEditorMaxFileSizeBytes => 10485760;

    public static string AddonEditorRulesText =>
        "Rules & Limits:\n\n" +
        "• Root Pathing: Always package files from the content root. Do not include the 'server' folder itself.\n" +
        "• File Size: Individual files cannot exceed 256 MB.\n" +
        "• Archive Size: A single .cbpak file is limited to 1.5 GB.\n" +
        "• Security: Executable files (.exe, .dll, etc.) are strictly prohibited.\n" +
        "• File Hash: If you enter an incorrect hash, the file will be downloaded on every connection.";

    public static HashSet<string> SupportedExtensions => new(StringComparer.OrdinalIgnoreCase)
    {
        ".as",
        ".json",
        ".txt"
    };

    public static string GithubOwner => "PavlikHTP";
    public static string GithubRepo => "CB2-Toolkit";
    public static string GithubUrl => $"https://github.com/{GithubOwner}/{GithubRepo}";
    
    private static Version GetSafeVersion()
    {
        return Version.TryParse(VersionString.Split('-')[0], out var parsed) 
            ? parsed 
            : new Version(1, 0, 0);
    }
}