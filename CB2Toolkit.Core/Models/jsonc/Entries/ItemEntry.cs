namespace CB2Toolkit.Core.Models.Settings.jsonc.Entries;

public class ItemEntry
{
    public int id { get; set; }
    public string name { get; set; } = string.Empty;
    public string worldmodel { get; set; } = string.Empty;
    public string icon { get; set; } = string.Empty;
    public float scale { get; set; } = 0.1f;
    public int picksound { get; set; } = 1;
    public int weapon { get; set; } = 0;
    public string color { get; set; } = "255 255 255";
}