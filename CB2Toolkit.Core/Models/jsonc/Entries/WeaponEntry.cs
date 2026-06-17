namespace CB2Toolkit.Core.Models.Settings.jsonc.Entries;

public class WeaponEntry
{
    public int id { get; set; }
    public string filename { get; set; } = string.Empty;
    public int type { get; set; } = 0;
    public int attachid { get; set; }
    public float offsetx { get; set; }
    public float offsety { get; set; }
    public float offsetz { get; set; }
    public float offsetpitch { get; set; }
    public float offsetyaw { get; set; }
    public float offsetroll { get; set; }
    public float scale { get; set; }
    public float muzzleflashscale { get; set; }
    public float muzzleflashyaw { get; set; }
    public float recoil { get; set; }
    public float spread { get; set; }
    public float shootrate { get; set; }
    public int recoilseed { get; set; }
    public float damage { get; set; }
    public int maxammo { get; set; }
    public int maxclipammo { get; set; }
    public float reloadtime { get; set; }
    public string shootsound { get; set; } = string.Empty;
    public string reloadsound { get; set; } = string.Empty;
    public string deploysound { get; set; } = string.Empty;
    public string shootstartsound { get; set; } = string.Empty;
    public float sightoffsetx { get; set; }
    public float sightoffsety { get; set; }
    public bool isviewmodel { get; set; } = false;
    public bool notexture { get; set; } = false;
    public int handsurface { get; set; }
    public bool pump { get; set; } = false;
    public int pellets { get; set; }
    public string shellbone { get; set; } = string.Empty;
    public string shellmodel { get; set; } = string.Empty;
    public string shellactionsound { get; set; } = string.Empty;
    public string shellcollisionsound { get; set; } = string.Empty;
    public string shellsound { get; set; } = string.Empty;
    public int shelltimeout { get; set; }
    public float shellspeed { get; set; }
    public float shellradius { get; set; }
    public float shellscale { get; set; } = 1.0f;
    public float shellimpacttime { get; set; }
    public float shellgravity { get; set; }
    public float shellrestitution { get; set; }
    public int shellemitter { get; set; } = -1;
    public int shellactionemitter { get; set; } = -1;
    public int itemid { get; set; } = 0;
    public string itemname { get; set; } = string.Empty;
    public string itemworldmodel { get; set; } = string.Empty;
    public string itemicon { get; set; } = string.Empty;
    public float itemscale { get; set; } = 0.1f;
    public int itempicksound { get; set; } = 1;

    public List<WeaponAnimation> animations { get; set; } = new();
}

public class WeaponAnimation
{
    public int id { get; set; }
    public int start { get; set; }
    public int end { get; set; }
    public float speed { get; set; }
}