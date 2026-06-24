using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CB2Toolkit.CodeEditor.Models.Enums;
using CB2Toolkit.CodeEditor.Views;
using CB2Toolkit.Core;
using CB2Toolkit.Core.Models.Enums;
using CB2Toolkit.Core.Models.Settings;
using CB2Toolkit.Core.Services;
using Microsoft.Win32;

namespace CB2Toolkit.Views;

public partial class SettingsView : UserControl
{
    private bool _isBusy;

    private static readonly Brush ActiveBackgroundBrush = CreateFrozenBrush("#2B2B30");
    private static readonly Brush ActiveBorderBrush = CreateFrozenBrush("#4D7CFE");
    private static readonly Brush NormalBackgroundBrush = CreateFrozenBrush("#1E1E22");
    private static readonly Brush NormalBorderBrush = CreateFrozenBrush("#2B2B30");

    private static Brush CreateFrozenBrush(string hexColor)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
        brush.Freeze();
        return brush;
    }

    public SettingsView()
    {
        InitializeComponent();
        LoadSettingsIntoUi();
    }

    private void LoadSettingsIntoUi()
    {
        var settings = SettingsService.Instance.Current;

        GitHubUrlTextBox.Text = settings.GitHubNewsUrl;
        PastebinUrlTextBox.Text = settings.PastebinNewsUrl;
        HighlightGitHubUrlTextBox.Text = settings.SyntaxGitHubUrl;
        HighlightPastebinUrlTextBox.Text = settings.SyntaxPastebinUrl;
        CompletionGitHubUrlTextBox.Text = settings.CompletionGitHubUrl;
        AsCompilerPathTextBox.Text = settings.AngelScriptCompilerPath;
        FontSizeSlider.Value = settings.EditorFontSize;
        PriorityComboBox.SelectedValue = settings.FetchPriority.ToString();

        LoadHotkey(HideSearchPanelHotkeyButton, settings.Hotkeys.HideSearchPanelKey, settings.Hotkeys.HideSearchPanelModifiers);
        LoadHotkey(ToggleCommentHotkeyButton, settings.Hotkeys.ToggleCommentKey, settings.Hotkeys.ToggleCommentModifiers);
        LoadHotkey(GlobalSearchHotkeyButton, settings.Hotkeys.GlobalSearchKey, settings.Hotkeys.GlobalSearchModifiers);
        LoadHotkey(SaveFileHotkeyButton, settings.Hotkeys.SaveFileKey, settings.Hotkeys.SaveFileModifiers);
        LoadHotkey(SearchPanelHotkeyButton, settings.Hotkeys.SearchPanelKey, settings.Hotkeys.SearchPanelModifiers);
        LoadHotkey(DuplicateLineHotkeyButton, settings.Hotkeys.DuplicateLineKey, settings.Hotkeys.DuplicateLineModifiers);
        LoadHotkey(SaveAllHotkeyButton, settings.Hotkeys.SaveAllKey, settings.Hotkeys.SaveAllModifiers);
        LoadHotkey(RunCompilerHotkeyButton, settings.Hotkeys.RunCompilerKey, settings.Hotkeys.RunCompilerModifiers);
        LoadHotkey(UndoHotkeyButton, settings.Hotkeys.UndoKey, settings.Hotkeys.UndoModifiers);
        LoadHotkey(RedoHotkeyButton, settings.Hotkeys.RedoKey, settings.Hotkeys.RedoModifiers);
        LoadHotkey(RenameFileHotkeyButton, settings.Hotkeys.RenameFile, settings.Hotkeys.RenameFileModifiers);
        LoadHotkey(DeleteFileHotkeyButton, settings.Hotkeys.DeleteFile, settings.Hotkeys.DeleteFileModifiers);
        LoadHotkey(NavigateBackHotkeyButton, settings.Hotkeys.NavigateBackKey, settings.Hotkeys.NavigateBackModifiers);
        LoadHotkey(NavigateForwardHotkeyButton, settings.Hotkeys.NavigateForwardKey, settings.Hotkeys.NavigateForwardModifiers);
    }

    private void SaveUiToSettings(AppSettings settings)
    {
        settings.GitHubNewsUrl = GitHubUrlTextBox.Text.Trim();
        settings.PastebinNewsUrl = PastebinUrlTextBox.Text.Trim();
        settings.SyntaxGitHubUrl = HighlightGitHubUrlTextBox.Text.Trim();
        settings.SyntaxPastebinUrl = HighlightPastebinUrlTextBox.Text.Trim();
        settings.CompletionGitHubUrl = CompletionGitHubUrlTextBox.Text.Trim();
        settings.AngelScriptCompilerPath = AsCompilerPathTextBox.Text.Trim();
        settings.EditorFontSize = FontSizeSlider.Value;

        if (PriorityComboBox.SelectedValue is string tag && Enum.TryParse<FetchPrioritySource>(tag, out var priority))
        {
            settings.FetchPriority = priority;
        }

        SaveHotkey(HideSearchPanelHotkeyButton, (k, m) => { settings.Hotkeys.HideSearchPanelKey = k; settings.Hotkeys.HideSearchPanelModifiers = m; });
        SaveHotkey(ToggleCommentHotkeyButton, (k, m) => { settings.Hotkeys.ToggleCommentKey = k; settings.Hotkeys.ToggleCommentModifiers = m; });
        SaveHotkey(GlobalSearchHotkeyButton, (k, m) => { settings.Hotkeys.GlobalSearchKey = k; settings.Hotkeys.GlobalSearchModifiers = m; });
        SaveHotkey(SaveFileHotkeyButton, (k, m) => { settings.Hotkeys.SaveFileKey = k; settings.Hotkeys.SaveFileModifiers = m; });
        SaveHotkey(SearchPanelHotkeyButton, (k, m) => { settings.Hotkeys.SearchPanelKey = k; settings.Hotkeys.SearchPanelModifiers = m; });
        SaveHotkey(DuplicateLineHotkeyButton, (k, m) => { settings.Hotkeys.DuplicateLineKey = k; settings.Hotkeys.DuplicateLineModifiers = m; });
        SaveHotkey(SaveAllHotkeyButton, (k, m) => { settings.Hotkeys.SaveAllKey = k; settings.Hotkeys.SaveAllModifiers = m; });
        SaveHotkey(RunCompilerHotkeyButton, (k, m) => { settings.Hotkeys.RunCompilerKey = k; settings.Hotkeys.RunCompilerModifiers = m; });
        SaveHotkey(UndoHotkeyButton, (k, m) => { settings.Hotkeys.UndoKey = k; settings.Hotkeys.UndoModifiers = m; });
        SaveHotkey(RedoHotkeyButton, (k, m) => { settings.Hotkeys.RedoKey = k; settings.Hotkeys.RedoModifiers = m; });
        SaveHotkey(RenameFileHotkeyButton, (k, m) => { settings.Hotkeys.RenameFile = k; settings.Hotkeys.RenameFileModifiers = m; });
        SaveHotkey(DeleteFileHotkeyButton, (k, m) => { settings.Hotkeys.DeleteFile = k; settings.Hotkeys.DeleteFileModifiers = m; });
        SaveHotkey(NavigateBackHotkeyButton, (k, m) => { settings.Hotkeys.NavigateBackKey = k; settings.Hotkeys.NavigateBackModifiers = m; });
        SaveHotkey(NavigateForwardHotkeyButton, (k, m) => { settings.Hotkeys.NavigateForwardKey = k; settings.Hotkeys.NavigateForwardModifiers = m; });
    }

    private void HotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        Button button = (Button)sender;
        button.Content = ">>> Press Key <<<";
        button.Background = ActiveBackgroundBrush;
        button.BorderBrush = ActiveBorderBrush;
    }

    private void HotkeyButton_KeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
            e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
            e.Key == Key.LeftShift || e.Key == Key.RightShift ||
            e.Key == Key.LWin || e.Key == Key.RWin)
        {
            return;
        }

        Key pressedKey = e.Key == Key.System ? e.SystemKey : e.Key;
        ModifierKeys modifiers = Keyboard.Modifiers;

        var button = (Button)sender;
        string keyStr = pressedKey.ToString();
        string modStr = modifiers.ToString();

        button.Content = modifiers == ModifierKeys.None ? keyStr : $"{modStr} + {keyStr}".Replace(", ", " + ");
        button.Tag = new Tuple<string, string>(keyStr, modStr);

        button.Background = NormalBackgroundBrush;
        button.BorderBrush = NormalBorderBrush;
    }

    private void HotkeyButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var button = (Button)sender;

        if (button.Content is string content && content == ">>> Press Key <<<")
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                return;
            }

            e.Handled = true;

            string keyStr = e.ChangedButton.ToString();
            string modStr = Keyboard.Modifiers.ToString();

            button.Content = Keyboard.Modifiers == ModifierKeys.None ? keyStr : $"{modStr} + {keyStr}".Replace(", ", " + ");
            button.Tag = new Tuple<string, string>(keyStr, modStr);

            button.Background = NormalBackgroundBrush;
            button.BorderBrush = NormalBorderBrush;
        }
    }
    
    private void LoadHotkey(Button button, string key, string modifiers)
    {
        button.Content = string.IsNullOrEmpty(modifiers) || modifiers == "None" ? key : $"{modifiers} + {key}".Replace(", ", " + ");
        button.Tag = new Tuple<string, string>(key, modifiers);
    }

    private void SaveHotkey(Button button, Action<string, string> setter)
    {
        if (button.Tag is Tuple<string, string> t)
        {
            setter(t.Item1, t.Item2);
        }
    }

    private void SaveSettings_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        SaveSettings_Click(sender, e);
    }
    
    private void BackToMenu_Click(object sender, RoutedEventArgs e)
    {
        BackToMenu();
    }
    
    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        _isBusy = true;

        try
        {
            SaveUiToSettings(SettingsService.Instance.Current);
            await SettingsService.Instance.SaveAsync();
            ModernMessageBox.Show(Window.GetWindow(this), "Settings successfully saved to AppData!", AppMetadata.Title);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void BrowseCompiler_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Executable Files (*.exe)|*.exe",
            Title = "Select AngelScript Compiler"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            AsCompilerPathTextBox.Text = openFileDialog.FileName;
        }
    }

    private void OpenConfigFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(AppMetadata.AppDataFolder))
        {
            Directory.CreateDirectory(AppMetadata.AppDataFolder);
        }

        ShellService.Instance.OpenFolder(AppMetadata.AppDataFolder);
    }

    private async void ImportConfig_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        _isBusy = true;

        try
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "JSON Configuration (*.json)|*.json",
                Title = "Select configuration file to import"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                Window parentWindow = Window.GetWindow(this);
                bool success = await SettingsService.Instance.ImportAsync(openFileDialog.FileName);
                
                if (success)
                {
                    LoadSettingsIntoUi();
                    ModernMessageBox.Show(parentWindow, "Configuration successfully imported!", AppMetadata.Title);
                }
                else
                {
                    ModernMessageBox.Show(parentWindow, "Failed to import file.", "Error", ModernBoxType.Error);
                }
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async void ExportConfig_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        _isBusy = true;

        try
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "JSON Configuration (*.json)|*.json",
                FileName = "cb2_settings_backup.json",
                Title = "Save current configuration"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var settings = SettingsService.Instance.Current;
                SaveUiToSettings(settings);

                Window parentWindow = Window.GetWindow(this);
                bool success = await SettingsService.Instance.ExportAsync(saveFileDialog.FileName);
                
                if (success)
                {
                    ModernMessageBox.Show(parentWindow, "Configuration successfully exported!", "CB2Toolkit");
                }
                else
                {
                    ModernMessageBox.Show(parentWindow, "Failed to save configuration file.", "Error", ModernBoxType.Error);
                }
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    public void BackToMenu()
    {
        Window currentWindow = Window.GetWindow(this);
        if (currentWindow != null)
        {
            dynamic mainWindow = currentWindow;
            mainWindow.NavigateToMenu();
        }
    }
}