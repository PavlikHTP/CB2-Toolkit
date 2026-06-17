namespace CB2Toolkit.Core.Models.Settings.jsonc.Entries;

public class AttachModelEntry
{
    public int id { get; set; }
    public string filename { get; set; } = string.Empty;
    public string texture { get; set; } = string.Empty;
    public bool rigged { get; set; } = false;
    public int variety { get; set; } = 0;

    public List<AttachPreset> presets { get; set; } = new();
    public List<AttachSound> sounds { get; set; } = new();
}

public class AttachPreset
{
    public int id { get; set; }
    public string bone { get; set; } = string.Empty;
    public float offsetx { get; set; }
    public float offsety { get; set; }
    public float offsetz { get; set; }
    public float offsetpitch { get; set; }
    public float offsetyaw { get; set; }
    public float offsetroll { get; set; }
    public float scale { get; set; }
    public int variety { get; set; }
}

public class AttachSound
{
    public int id { get; set; }
    public string filename { get; set; } = string.Empty;
}