using CB2Toolkit.Core.Models.Settings.jsonc.Entries;

namespace CB2Toolkit.Core.Models.Settings.jsonc;

public class JsoncConfig
{
    public string serverfolder { get; set; } = string.Empty;
    public List<JsoncFileEntry> files { get; set; } = new();
    public string roomtemplates { get; set; } = string.Empty;
    public string materials { get; set; } = string.Empty;
    public List<PlayerModelEntry> playermodels { get; set; } = new();
    public List<PlayerTextureEntry> playertextures { get; set; } = new();
    public List<PlayerWeaponTextureEntry> playerweapontextures { get; set; } = new();
    public List<AttachModelEntry> attaches { get; set; } = new();
    public List<WeaponEntry> weapons { get; set; } = new();
    public List<ItemEntry> items { get; set; } = new();
    public List<WorldObjectEntry> objects { get; set; } = new();
    public List<WorldTextureEntry> textures { get; set; } = new();
    public List<FontEntry> fonts { get; set; } = new();
    public List<SpriteFontEntry> spritefonts { get; set; } = new();
}