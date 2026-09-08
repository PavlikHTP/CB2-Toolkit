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

    public string DuplicateKey { get; set; } = "D";
    public string DuplicateModifiers { get; set; } = "Control";

    public string SaveAllKey { get; set; } = "S";
    public string SaveAllModifiers { get; set; } = "Control, Shift";

    public string RunCompilerKey { get; set; } = "F5";
    public string RunCompilerModifiers { get; set; } = "Control";

    public string RedoKey { get; set; } = "Z";
    public string RedoModifiers { get; set; } = "Control, Shift";

    public string UndoKey { get; set; } = "Z";
    public string UndoModifiers { get; set; } = "Control";
    
    public string RenameKey { get; set; } = "F2";
    public string RenameModifiers { get; set; } = "None";
    
    public string DeleteKey { get; set; } = "Delete";
    public string DeleteModifiers { get; set; } = "None";
    
    public string NavigateBackKey { get; set; } = "Left";
    public string NavigateBackModifiers { get; set; } = "Alt";

    public string NavigateForwardKey { get; set; } = "Right";
    public string NavigateForwardModifiers { get; set; } = "Alt";
    
    public string SelectAllKey { get; set; } = "A";
    public string SelectAllModifiers { get; set; } = "Control";
    
    public string FormatKey { get; set; } = "K";
    public string FormatModifiers { get; set; } = "Control";

    public string ConsoleKey { get; set; } = "OemTilde";
    public string ConsoleModifiers { get; set; } = "Control";

    public string GoToDefinitionKey { get; set; } = "F12";
    public string GoToDefinitionModifiers { get; set; } = "None";

    public string AutoCompleteKey { get; set; } = "Space";
    public string AutoCompleteModifiers { get; set; } = "Control";
}