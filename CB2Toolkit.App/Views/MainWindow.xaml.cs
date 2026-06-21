using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using CB2Toolkit.AddonEditor.Views;
using CB2Toolkit.CodeEditor.Views;
using CB2Toolkit.Core.Models;
using CB2Toolkit.Core.Services;

namespace CB2Toolkit.Views;

public partial class MainWindow : Window
{
    private string? _downloadUrl;

    public MainWindow()
    {
        InitializeComponent();
        MainContentHolder.Content = new MainMenuView();

        UpdateService.Instance.OnUpdateAvailable += ShowUpdateBanner;
    }

    public void NavigateToEditor()
    {
        MainContentHolder.Content = new AngelScriptEditorView();
    }

    public void NavigateToAddonEditor()
    {
        MainContentHolder.Content = new AddonEditorView();
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

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}