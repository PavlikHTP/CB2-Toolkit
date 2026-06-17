using System.Reflection;

namespace CB2Toolkit.Core;

public static class AppMetadata
{
    public static string Title => "CB2 Toolkit";
    
    public static string VersionString { get; } = 
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "1.0.0";
    
    public static Version CurrentVersion { get; } = GetSafeVersion();

    public static string AppDataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        Title
    );

    public static long AddonEditorMaxFileSizeBytes { get; } = 10485760;
    
    public static string AddonEditorRulesText { get; } = "Rules & Limits:\n\n" +
        "• Root Pathing: Always package files from the content root. Do not include the 'server' folder itself.\n" +
        "• File Size: Individual files cannot exceed 256 MB.\n" +
        "• Archive Size: A single .cbpak file is limited to 1.5 GB.\n" +
        "• Security: Executable files (.exe, .dll, etc.) are strictly prohibited.\n" +
        "• File Hash: If you enter an incorrect hash, the file will be downloaded on every connection.";

    public static HashSet<string> SupportedExtensions { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ".as",
        ".json",
        ".txt"
    };
    
    private static Version GetSafeVersion()
    {
        return Version.TryParse(VersionString.Split('-')[0], out var parsed) 
            ? parsed 
            : new Version(1, 0, 0);
    }
}