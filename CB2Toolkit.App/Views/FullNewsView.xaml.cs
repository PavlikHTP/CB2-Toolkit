using System.Windows;
using System.Windows.Controls;
using CB2Toolkit.Core.Models;

namespace CB2Toolkit.Views;

public partial class FullNewsView : UserControl
{
    public FullNewsView(NewsItem news)
    {
        InitializeComponent();
        
        NewsTitle.Text = news.Title;
        NewsDate.Text = news.Date;
        NewsContent.Text = news.Content;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        mainWindow?.NavigateToMenu();
    }
}