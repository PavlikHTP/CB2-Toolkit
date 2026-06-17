using System.Windows.Input;

namespace CB2Toolkit.CodeEditor.Utils;

public static class HotkeyMatcher
{
    public static bool IsMatch(KeyEventArgs e, string targetKeyStr, string targetModifiersStr)
    {
        if (!Enum.TryParse<Key>(targetKeyStr, out var targetKey) || 
            !Enum.TryParse<ModifierKeys>(targetModifiersStr, out var targetModifiers))
        {
            return false;
        }

        Key pressedKey = e.Key == Key.System ? e.SystemKey : e.Key;
        return pressedKey == targetKey && Keyboard.Modifiers == targetModifiers;
    }
}