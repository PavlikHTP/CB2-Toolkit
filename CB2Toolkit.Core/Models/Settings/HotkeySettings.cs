namespace CB2Toolkit.Core.Models.Settings;

public class HotkeySettings
{
    public string HideSearchPanelKey { get; set; } = "Escape";
    public string HideSearchPanelModifiers { get; set; } = "None";

    public string ToggleCommentKey { get; set; } = "OemQuestion";
    public string ToggleCommentModifiers { get; set; } = "Control";

    public string GlobalSearchKey { get; set; } = "F";
    public string GlobalSearchModifiers { get; set; } = "Control, Shift";

    public string SaveFileKey { get; set; } = "S";
    public string SaveFileModifiers { get; set; } = "Control";

    public string SearchPanelKey { get; set; } = "F";
    public string SearchPanelModifiers { get; set; } = "Control";

    public string DuplicateLineKey { get; set; } = "D";
    public string DuplicateLineModifiers { get; set; } = "Control";

    public string SaveAllKey { get; set; } = "S";
    public string SaveAllModifiers { get; set; } = "Control, Shift";

    public string RunCompilerKey { get; set; } = "F5";
    public string RunCompilerModifiers { get; set; } = "Control";

    public string RedoKey { get; set; } = "Z";
    public string RedoModifiers { get; set; } = "Control, Shift";

    public string UndoKey { get; set; } = "Z";
    public string UndoModifiers { get; set; } = "Control";
    
    public string RenameFile { get; set; } = "F2";
    public string RenameFileModifiers { get; set; } = "None";
    
    public string DeleteFile { get; set; } = "Delete";
    public string DeleteFileModifiers { get; set; } = "None";
    
    public string NavigateBackKey { get; set; } = "Left";
    public string NavigateBackModifiers { get; set; } = "Alt";

    public string NavigateForwardKey { get; set; } = "Right";
    public string NavigateForwardModifiers { get; set; } = "Alt";
}