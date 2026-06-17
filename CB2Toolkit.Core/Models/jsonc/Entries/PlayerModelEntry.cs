namespace CB2Toolkit.Core.Models.Settings.jsonc.Entries;

public class PlayerModelEntry
{
    public int id { get; set; }
    public string filename { get; set; } = string.Empty;
    public float offsety { get; set; } = 0.0f;
    public float offsetyaw { get; set; } = 0.0f;
    public string headbone { get; set; } = string.Empty;
    public string spinebone { get; set; } = string.Empty;
    public string handbone { get; set; } = string.Empty;
    public string forearmbone { get; set; } = string.Empty;
    public float maximumspinepitch { get; set; } = 0.0f;
    public float maximumspinepitchdist { get; set; } = 0.0f;
    public float maximumheadpitch { get; set; } = 0.0f;
    public float fixedspinerotation { get; set; } = 0.0f;
    public bool usedefaultrolls { get; set; } = false;
    public float collisionradius { get; set; }
    public float scale { get; set; }
    public string stepsound { get; set; } = string.Empty;
    public float stamina { get; set; } = 0.0f;
    public float speed { get; set; } = 0.0f;
    public float viewoffsety { get; set; } = 0.0f;
    public float holdingitemoffsetx { get; set; } = 0.0f;
    public float holdingitemoffsety { get; set; } = 0.0f;
    public float holdingitemoffsetz { get; set; } = 0.0f;
    public float holdingitemoffsetpitch { get; set; } = 0.0f;
    public float holdingitemoffsetyaw { get; set; } = 0.0f;
    public float holdingitemoffsetroll { get; set; } = 0.0f;
    public float hitboxwidth { get; set; }
    public float hitboxheight { get; set; }
    public float hitboxdepth { get; set; }
    public string movesound { get; set; } = string.Empty;
    public bool disableroll { get; set; } = false;
    public bool disablebloodloss { get; set; } = false;
    public bool disableinjuries { get; set; } = false;
    public bool flippitch { get; set; } = false;
    public bool disableinteractitems { get; set; } = false;
    public int rotationmode { get; set; } = 0;
    public bool comparedspinerotation { get; set; } = false;
    public bool disablejump { get; set; } = false;
    public int material { get; set; } = 0;
    public int collisiontype { get; set; } = 0;
    public string emitterbones { get; set; } = string.Empty;
    public int boneemitter { get; set; } = 0;
    public int copyattaches { get; set; } = 0;

    public List<PlayerIdleSound> idlesounds { get; set; } = new();
    public List<PlayerAnimation> animations { get; set; } = new();
    public List<PlayerDeadAnimation> deadanimations { get; set; } = new();
}

public class PlayerIdleSound
{
    public int id { get; set; }
    public string filename { get; set; } = string.Empty;
    public bool selfplay { get; set; } = false;
}

public class PlayerAnimation
{
    public int id { get; set; }
    public int start { get; set; }
    public int end { get; set; }
    public float speed { get; set; }
    public float stepaccum { get; set; }
}

public class PlayerDeadAnimation
{
    public int id { get; set; }
    public int start { get; set; }
    public int end { get; set; }
    public float speed { get; set; }
}