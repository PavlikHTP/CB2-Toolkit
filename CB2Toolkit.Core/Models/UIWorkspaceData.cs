using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace CB2Toolkit.Core.Models;

public class UIWorkspaceData
{
    [JsonInclude]
    public int RefWidth { get; set; } = 1920;

    [JsonInclude]
    public int RefHeight { get; set; } = 1080;

    [JsonInclude]
    public bool SquareViewport { get; set; } = false;

    [JsonInclude]
    public ObservableCollection<UIElementModel> Elements { get; set; } = new();
}
