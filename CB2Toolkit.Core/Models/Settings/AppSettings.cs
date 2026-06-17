using CB2Toolkit.Core.Models.Settings.Enums;

namespace CB2Toolkit.Core.Models.Settings;

public class AppSettings
{
    public FetchPrioritySource FetchPriority { get; set; } = FetchPrioritySource.GitHub;
    
    public string GitHubNewsUrl { get; set; } = "https://raw.githubusercontent.com/pavlikHTP/CB2Toolkit/main/news.txt";
    public string PastebinNewsUrl { get; set; } = "https://pastebin.com/raw/WhsswfeZ";
    
    public string HighlightGitHubUrl { get; set; } = "https://raw.githubusercontent.com/pavlikHTP/CB2Toolkit/main/angelscript.xshd";
    public string HighlightPastebinUrl { get; set; } = "https://pastebin.com/raw/ebDBExtv";
    
    public string CompletionGitHubUrl { get; set; } = "https://raw.githubusercontent.com/pavlikHTP/CB2Toolkit/main/completion.json";
    public string CompletionPastebinUrl { get; set; } = "https://pastebin.com/raw/greetings";
    
    public string AngelScriptCompilerPath { get; set; } = "";

    public double EditorFontSize { get; set; } = 13.0;
    public List<string> RecentAngelScriptFolders { get; set; } = new();
    public List<string> RecentAddonFolders { get; set; } = new();

    public List<string> ExpandedAngelScriptFolders { get; set; } = new();
    public List<string> ExpandedAddonFolders { get; set; } = new();
    public string LastOpenedAngelScriptFilePath { get; set; } = string.Empty;
    public string CustomAngelScriptCompilePath { get; set; } = string.Empty;
    public HotkeySettings Hotkeys { get; set; } = new();
}