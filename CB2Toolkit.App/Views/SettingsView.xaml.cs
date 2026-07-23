using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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

    private DispatcherTimer? _hotkeyAnimationTimer;
    private Button? _activeHotkeyButton;
    private int _hotkeyAnimationFrame;

    private static readonly string[] HotkeyAnimationFrames = new[]
    {
        "    > Press Key <    ",
        "   >> Press Key <<   ",
        "  >>> Press Key <<<  ",
        " >>>> Press Key <<<< "
    };

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
        LoadHotkey(DuplicateLineHotkeyButton, settings.Hotkeys.DuplicateKey, settings.Hotkeys.DuplicateModifiers);
        LoadHotkey(SaveAllHotkeyButton, settings.Hotkeys.SaveAllKey, settings.Hotkeys.SaveAllModifiers);
        LoadHotkey(RunCompilerHotkeyButton, settings.Hotkeys.RunCompilerKey, settings.Hotkeys.RunCompilerModifiers);
        LoadHotkey(UndoHotkeyButton, settings.Hotkeys.UndoKey, settings.Hotkeys.UndoModifiers);
        LoadHotkey(RedoHotkeyButton, settings.Hotkeys.RedoKey, settings.Hotkeys.RedoModifiers);
        LoadHotkey(RenameFileHotkeyButton, settings.Hotkeys.RenameKey, settings.Hotkeys.RenameModifiers);
        LoadHotkey(DeleteFileHotkeyButton, settings.Hotkeys.DeleteKey, settings.Hotkeys.DeleteModifiers);
        LoadHotkey(NavigateBackHotkeyButton, settings.Hotkeys.NavigateBackKey, settings.Hotkeys.NavigateBackModifiers);
        LoadHotkey(NavigateForwardHotkeyButton, settings.Hotkeys.NavigateForwardKey, settings.Hotkeys.NavigateForwardModifiers);
        LoadHotkey(SelectAllHotkeyButton, settings.Hotkeys.SelectAllKey, settings.Hotkeys.SelectAllModifiers);
        LoadHotkey(ConsoleHotkeyButton, settings.Hotkeys.ConsoleKey, settings.Hotkeys.ConsoleModifiers);
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
        SaveHotkey(DuplicateLineHotkeyButton, (k, m) => { settings.Hotkeys.DuplicateKey = k; settings.Hotkeys.DuplicateModifiers = m; });
        SaveHotkey(SaveAllHotkeyButton, (k, m) => { settings.Hotkeys.SaveAllKey = k; settings.Hotkeys.SaveAllModifiers = m; });
        SaveHotkey(RunCompilerHotkeyButton, (k, m) => { settings.Hotkeys.RunCompilerKey = k; settings.Hotkeys.RunCompilerModifiers = m; });
        SaveHotkey(UndoHotkeyButton, (k, m) => { settings.Hotkeys.UndoKey = k; settings.Hotkeys.UndoModifiers = m; });
        SaveHotkey(RedoHotkeyButton, (k, m) => { settings.Hotkeys.RedoKey = k; settings.Hotkeys.RedoModifiers = m; });
        SaveHotkey(RenameFileHotkeyButton, (k, m) => { settings.Hotkeys.RenameKey = k; settings.Hotkeys.RenameModifiers = m; });
        SaveHotkey(DeleteFileHotkeyButton, (k, m) => { settings.Hotkeys.DeleteKey = k; settings.Hotkeys.DeleteModifiers = m; });
        SaveHotkey(NavigateBackHotkeyButton, (k, m) => { settings.Hotkeys.NavigateBackKey = k; settings.Hotkeys.NavigateBackModifiers = m; });
        SaveHotkey(NavigateForwardHotkeyButton, (k, m) => { settings.Hotkeys.NavigateForwardKey = k; settings.Hotkeys.NavigateForwardModifiers = m; });
        SaveHotkey(SelectAllHotkeyButton, (k, m) => { settings.Hotkeys.SelectAllKey = k; settings.Hotkeys.SelectAllModifiers = m; });
        SaveHotkey(ConsoleHotkeyButton, (k, m) => { settings.Hotkeys.ConsoleKey = k; settings.Hotkeys.ConsoleModifiers = m; });
    }

    private void StartHotkeyAnimation(Button button)
    {
        ResetActiveHotkeyButton();

        _activeHotkeyButton = button;
        _hotkeyAnimationFrame = 0;

        _activeHotkeyButton.Background = ActiveBackgroundBrush;
        _activeHotkeyButton.BorderBrush = ActiveBorderBrush;
        _activeHotkeyButton.Content = HotkeyAnimationFrames[0];

        _hotkeyAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(160)
        };
        _hotkeyAnimationTimer.Tick += HotkeyAnimationTimer_Tick;
        _hotkeyAnimationTimer.Start();
    }

    private void HotkeyAnimationTimer_Tick(object? sender, EventArgs e)
    {
        if (_activeHotkeyButton == null) return;

        _hotkeyAnimationFrame = (_hotkeyAnimationFrame + 1) % HotkeyAnimationFrames.Length;
        _activeHotkeyButton.Content = HotkeyAnimationFrames[_hotkeyAnimationFrame];
    }

    private void StopHotkeyAnimation()
    {
        if (_hotkeyAnimationTimer != null)
        {
            _hotkeyAnimationTimer.Stop();
            _hotkeyAnimationTimer.Tick -= HotkeyAnimationTimer_Tick;
            _hotkeyAnimationTimer = null;
        }
        _activeHotkeyButton = null;
    }

    private void ResetActiveHotkeyButton()
    {
        if (_activeHotkeyButton != null)
        {
            _activeHotkeyButton.Background = NormalBackgroundBrush;
            _activeHotkeyButton.BorderBrush = NormalBorderBrush;

            if (_activeHotkeyButton.Tag is Tuple<string, string> t)
            {
                LoadHotkey(_activeHotkeyButton, t.Item1, t.Item2);
            }
        }

        StopHotkeyAnimation();
    }

    private void HotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            StartHotkeyAnimation(button);
        }
    }

    private void HotkeyButton_KeyDown(object sender, KeyEventArgs e)
    {
        var button = (Button)sender;

        if (button != _activeHotkeyButton) return;

        e.Handled = true;

        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
            e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
            e.Key == Key.LeftShift || e.Key == Key.RightShift ||
            e.Key == Key.LWin || e.Key == Key.RWin)
        {
         
            return;
        }
        
        StopHotkeyAnimation();

        Key pressedKey = e.Key == Key.System ? e.SystemKey : e.Key;
        ModifierKeys modifiers = Keyboard.Modifiers;

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

        if (button == _activeHotkeyButton)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                return;
            }

            e.Handled = true;

            StopHotkeyAnimation();

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
        var mainWindow = Window.GetWindow(this) as MainWindow;
        mainWindow?.NavigateToMenu();
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
}