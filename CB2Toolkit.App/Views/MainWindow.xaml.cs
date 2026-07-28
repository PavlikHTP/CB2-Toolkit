using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.ComponentModel;
using System.Windows.Data;
using System.Reflection;
using CB2Toolkit.AddonEditor.Views;
using CB2Toolkit.CodeEditor.Utils;
using CB2Toolkit.CodeEditor.Views;
using CB2Toolkit.Core.Console;
using CB2Toolkit.Core.Console.Commands;
using CB2Toolkit.Core.Models;
using CB2Toolkit.Core.Models.Enums;
using CB2Toolkit.Core.Models.Settings;
using CB2Toolkit.Core.Plugins;
using CB2Toolkit.Core.Services;
using CB2Toolkit.UIEditor.Views;

namespace CB2Toolkit.Views;

public partial class MainWindow : Window
{
    private string? _downloadUrl;
    private readonly ObservableCollection<LogEntry> _logEntries = new();
    private readonly ICollectionView _collectionView;

    private readonly CommandRegistry _commandRegistry;
    private readonly CommandProcessor _commandProcessor;

    private readonly List<string> _commandHistory = new();
    private int _historyIndex = -1;
    private string _currentInputDraft = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        MainContentHolder.Content = new MainMenuView();
    
        _collectionView = CollectionViewSource.GetDefaultView(_logEntries);
        _collectionView.Filter = FilterLogs;
        ConsoleItemsControl.ItemsSource = _collectionView;

        foreach (var oldEntry in LoggerService.Instance.GetHistory())
        {
            _logEntries.Add(oldEntry);
        }

        _commandRegistry = new CommandRegistry();
        
        _commandRegistry.RegisterFromAssembly(Assembly.GetExecutingAssembly());
        _commandRegistry.RegisterFromAssembly(typeof(CommandRegistry).Assembly);

        var helpCommand = new HelpCommand(_commandRegistry);
        _commandRegistry.Register(helpCommand);

        var consoleOutput = new LoggerConsoleOutput();
        _commandProcessor = new CommandProcessor(_commandRegistry, consoleOutput);

        UpdateService.Instance.OnUpdateAvailable += ShowUpdateBanner;
        LoggerService.Instance.OnLogAdded += AddLogEntry;
        LoggerService.Instance.OnLogCleared += ClearLogEntries;

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = SettingsService.Instance.Current;
        if (settings.PluginsEnabled)
        {
            await LoadPluginsAsync();
        }
    }

    private async Task LoadPluginsAsync()
    {
        try
        {
            var loader = PluginLoader.Instance;
            loader.EnsurePluginsFolder();

            var discovered = loader.DiscoverPlugins();

            foreach (var plugin in discovered)
            {
                try
                {
                    await loader.LoadPluginAsync(plugin);

                    if (plugin.Instance != null)
                    {
                        RegisterPluginAssembly(plugin.Instance.GetType().Assembly);
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.Instance.LogError($"Failed to load plugin '{plugin.Name}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"Plugin loading failed: {ex.Message}");
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        HotkeySettings hotkeys = SettingsService.Instance.Current.Hotkeys;

        if (HotkeyMatcher.IsMatch(e, hotkeys.ConsoleKey, hotkeys.ConsoleModifiers))
        {
            ToggleConsole();
            e.Handled = true;
            return;
        }
    }

    private bool FilterLogs(object obj)
    {
        if (obj is LogEntry entry)
        {
            if (entry.Type == LogType.Info && InfoCheckBox.IsChecked == false) return false;
            if (entry.Type == LogType.Warn && WarnCheckBox.IsChecked == false) return false;
            if (entry.Type == LogType.Error && ErrorCheckBox.IsChecked == false) return false;
            if (entry.Type == LogType.Debug && DebugCheckBox.IsChecked == false) return false;
            return true;
        }
        return true;
    }

    private void FilterCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _collectionView?.Refresh();
        ScrollToBottom();
    }

    private void AddLogEntry(LogEntry entry)
    {
        Dispatcher.Invoke(() =>
        {
            _logEntries.Add(entry);
            if (_logEntries.Count > 500)
            {
                _logEntries.RemoveAt(0);
            }
            ScrollToBottom();
        });
    }

    private void ClearLogEntries()
    {
        Dispatcher.Invoke(() => _logEntries.Clear());
    }

    private void ToggleConsole()
    {
        ConsoleOverlay.Visibility = ConsoleOverlay.Visibility == Visibility.Visible 
            ? Visibility.Collapsed 
            : Visibility.Visible;

        if (ConsoleOverlay.Visibility == Visibility.Visible)
        {
            ScrollToBottom();
            ConsoleInputTextBox.Focus();
        }
    }

    private void ToggleConsole_Click(object sender, RoutedEventArgs e)
    {
        ToggleConsole();
    }

    private void ClearConsole_Click(object sender, RoutedEventArgs e)
    {
        LoggerService.Instance.Clear();
    }

    private void ConsoleInputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SendConsoleCommand();
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            NavigateHistory(-1);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            NavigateHistory(1);
            e.Handled = true;
        }
    }

    private void NavigateHistory(int direction)
    {
        if (_commandHistory.Count == 0) return;

        // Если индекс выходит за границы, сбрасываем в конец (к черновику)
        if (_historyIndex < 0 || _historyIndex > _commandHistory.Count)
        {
            _historyIndex = _commandHistory.Count;
        }

        // Сохраняем введенный текст как черновик при попытке листать вверх
        if (_historyIndex == _commandHistory.Count && direction < 0)
        {
            _currentInputDraft = ConsoleInputTextBox.Text;
        }

        int newIndex = Math.Clamp(_historyIndex + direction, 0, _commandHistory.Count);

        if (newIndex != _historyIndex)
        {
            _historyIndex = newIndex;

            if (_historyIndex == _commandHistory.Count)
            {
                ConsoleInputTextBox.Text = _currentInputDraft;
            }
            else
            {
                ConsoleInputTextBox.Text = _commandHistory[_historyIndex];
            }

            ConsoleInputTextBox.CaretIndex = ConsoleInputTextBox.Text.Length;
        }
    }

    private void ConsoleSendButton_Click(object sender, RoutedEventArgs e)
    {
        SendConsoleCommand();
    }

    private async void SendConsoleCommand()
    {
        string command = ConsoleInputTextBox.Text.Trim();
        if (string.IsNullOrEmpty(command)) return;

        if (_commandHistory.Count == 0 || _commandHistory[^1] != command)
        {
            _commandHistory.Add(command);
            if (_commandHistory.Count > 100)
            {
                _commandHistory.RemoveAt(0);
            }
        }

        _historyIndex = _commandHistory.Count;
        _currentInputDraft = string.Empty;

        LoggerService.Instance.Log($"> {command}", LogType.Info, "#60A5FA");
        ConsoleInputTextBox.Text = string.Empty;
        
        await _commandProcessor.ExecuteAsync(command);

        ScrollToBottom();
    }

    public void RegisterPluginAssembly(Assembly assembly)
    {
        _commandRegistry.RegisterFromAssembly(assembly);
    }

    private void ScrollToBottom()
    {
        Dispatcher.BeginInvoke(new Action(() => ConsoleScrollViewer?.ScrollToEnd()));
    }

    public void NavigateToEditor()
    {
        MainContentHolder.Content = new AngelScriptEditorView();
    }

    public void NavigateToAddonEditor()
    {
        MainContentHolder.Content = new AddonEditorView();
    }
    
    public void NavigateToUIEditor()
    {
        MainContentHolder.Content = new UIEditorView();
        DiscordRpcService.UpdateToUIEditor();
    }

    public void NavigateToMenu()
    {
        MainContentHolder.Content = new MainMenuView();
        DiscordRpcService.UpdateToMainMenu();
    }

    public void NavigateToSettings()
    {
        MainContentHolder.Content = new SettingsView();
        DiscordRpcService.UpdateToMainMenu();
    }

    public void NavigateToFullNews(NewsItem news)
    {
        MainContentHolder.Content = new FullNewsView(news);
        DiscordRpcService.UpdateToMainMenu();
    }

    private void ShowUpdateBanner(string version, string url)
    {
        Dispatcher.Invoke(() =>
        {
            _downloadUrl = url;
            UpdateText.Text = $"New version {version} is available!";
            UpdateBanner.Visibility = Visibility.Visible;
        });
    }

    private void DownloadUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_downloadUrl))
        {
            try
            {
                Process.Start(new ProcessStartInfo(_downloadUrl) { UseShellExecute = true });
            }
            catch
            {
            }
        }
    }

    private void CloseBanner_Click(object sender, RoutedEventArgs e)
    {
        UpdateBanner.Visibility = Visibility.Collapsed;
    }

    private void VoiceCall_Click(object sender, RoutedEventArgs e)
    {
        var callWindow = new VoiceWindow
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        callWindow.Show();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}