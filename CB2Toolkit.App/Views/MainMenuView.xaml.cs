using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using CB2Toolkit.Core;
using CB2Toolkit.Core.Models;
using CB2Toolkit.Core.Services;
using CB2Toolkit.Models;

namespace CB2Toolkit.Views;

public partial class MainMenuView : UserControl
{
    private readonly NewsService _newsService = new();
    private static List<NewsDisplayItem>? _cachedDisplayNews;

    public MainMenuView()
    {
        InitializeComponent();
        LoadDynamicNews();
    }

    private async void LoadDynamicNews()
    {
        if (_cachedDisplayNews != null)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            NewsItemsControl.ItemsSource = _cachedDisplayNews;
            
            if (Resources["FadeInContentOnlyAnimation"] is Storyboard fadeInStoryboard)
            {
                fadeInStoryboard.Begin();
            }
            return;
        }

        var newsList = await _newsService.FetchNewsAsync(SettingsService.Instance.Current);

        if (newsList.Count > 0)
        {
            var displayList = new List<NewsDisplayItem>();

            foreach (var news in newsList)
            {
                var visibility = Visibility.Collapsed;

                if (Version.TryParse(news.Version, out var newsVersion) && newsVersion > AppMetadata.CurrentVersion)
                {
                    visibility = Visibility.Visible;
                }

                displayList.Add(new NewsDisplayItem
                {
                    Data = news,
                    Tag = news.Tag.ToUpper(),
                    Title = news.Title,
                    Date = news.Date,
                    Preview = news.Preview,
                    WarningVisibility = visibility
                });
            }

            _cachedDisplayNews = displayList;
        }
        else
        {
            var errorList = new List<NewsDisplayItem>
            {
                new NewsDisplayItem
                {
                    Data = new NewsItem("ERROR", "Error", "Offline Mode", "Failed to load latest updates. Check your internet connection.", "", "1.0.0"),
                    Tag = "ERROR",
                    Title = "Offline Mode",
                    Date = "Error",
                    Preview = "Failed to load latest updates. Check your internet connection.",
                    WarningVisibility = Visibility.Collapsed
                }
            };
            
            NewsItemsControl.ItemsSource = errorList;
        }
        
        NewsItemsControl.ItemsSource = _cachedDisplayNews;

        if (Resources["SwitchFromLoadingToContentAnimation"] is Storyboard switchStoryboard)
        {
            switchStoryboard.Begin();
        }
    }

    private void OpenEditor_Click(object sender, RoutedEventArgs e)
    {
        MainWindow mainWindow = Application.Current.MainWindow as MainWindow;
        mainWindow.NavigateToEditor();
    }
    
    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        MainWindow mainWindow = Application.Current.MainWindow as MainWindow;
        mainWindow?.NavigateToSettings();
    }
    
    private void OpenAddonEditor_Click(object sender, RoutedEventArgs e)
    {
        MainWindow mainWindow = Application.Current.MainWindow as MainWindow;
        mainWindow?.NavigateToAddonEditor();
    }
    
    private void OpenNews_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is NewsDisplayItem displayItem)
        {
            if (displayItem.Tag == "ERROR") return;

            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.NavigateToFullNews(displayItem.Data);
            }
        }
    }

    private void OpenUIEditor_Click(object sender, RoutedEventArgs e)
    {
        MainWindow mainWindow = Application.Current.MainWindow as MainWindow;
        mainWindow?.NavigateToUIEditor();
    }
}