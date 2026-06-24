using CB2Toolkit.Core.Models.Enums;

namespace CB2Toolkit.Core.Models.Settings;

public class AppSettings
{
    public FetchPrioritySource FetchPriority { get; set; } = FetchPrioritySource.GitHub;
    
    public string GitHubNewsUrl { get; set; } = "https://gist.githubusercontent.com/PavlikHTP/d630c0d7d5ff3828aa5b56295a39a0a8/raw/gistfile1.txt";
    public string PastebinNewsUrl { get; set; } = "https://pastebin.com/raw/WhsswfeZ";

    public string SyntaxGitHubUrl { get; set; } = "https://gist.githubusercontent.com/PavlikHTP/67caf1a443a2e7e49573829c07014815/raw/gistfile1.txt";
    public string SyntaxPastebinUrl { get; set; } = "https://pastebin.com/raw/ebDBExtv";

    public string CompletionGitHubUrl { get; set; } = "https://gist.githubusercontent.com/PavlikHTP/229677b25145dc74e5fe1fb24beba81c/raw/gistfile1.txt";
    
    public string AngelScriptCompilerPath { get; set; } = "";

    public double EditorFontSize { get; set; } = 13.0;
    public List<string> RecentAngelScriptFolders { get; set; } = new();
    public List<string> RecentAddonFolders { get; set; } = new();

    public List<string> ExpandedAngelScriptFolders { get; set; } = new();
    public List<string> ExpandedAddonFolders { get; set; } = new();
    public string LastOpenedAngelScriptFilePath { get; set; } = string.Empty;
    public string CustomAngelScriptCompilePath { get; set; } = string.Empty;
    public string UIEditorCompilePath { get; set; } = string.Empty;
    public HotkeySettings Hotkeys { get; set; } = new();
}