using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CB2Toolkit.CodeEditor.Views;

public sealed record ErrorEntry(string Display, int Offset);

public partial class ErrorsListWindow : Window
{
    private static readonly Geometry ErrorGeometry = Geometry.Parse("M12,2C6.47,2,2,6.47,2,12s4.47,10,10,10,10-4.47,10-10S17.53,2,12,2z M17,15.59,15.59,17,12,13.41,8.41,17,7,15.59,10.59,12,7,8.41,8.41,7,12,10.59,15.59,7,17,8.41,13.41,12,17,15.59z");
    private static readonly Geometry WarningGeometry = Geometry.Parse("M1,21h22L12,2L1,21z M13,18h-2v-2h2V18z M13,14h-2V9h2V14z");
    private static readonly Brush ErrorBrush = CreateFrozenBrush("#EF4444");
    private static readonly Brush ErrorBorderBrush = CreateFrozenBrush("#451A1A");
    private static readonly Brush WarningBrush = CreateFrozenBrush("#F59E0B");
    private static readonly Brush WarningBorderBrush = CreateFrozenBrush("#452A1A");

    private readonly Action<int>? _onJump;

    public ErrorsListWindow(string title, IReadOnlyList<ErrorEntry> entries, Action<int>? onJump = null, bool isWarning = false)
    {
        InitializeComponent();
        TitleTextBlock.Text = title;
        _onJump = onJump;

        if (isWarning)
        {
            StatusIcon.Data = WarningGeometry;
            StatusIcon.Fill = WarningBrush;
            WindowBorder.BorderBrush = WarningBorderBrush;
        }
        else
        {
            StatusIcon.Data = ErrorGeometry;
            StatusIcon.Fill = ErrorBrush;
            WindowBorder.BorderBrush = ErrorBorderBrush;
        }

        foreach (ErrorEntry entry in entries)
        {
            ErrorsList.Items.Add(entry);
        }
    }

    private static Brush CreateFrozenBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void ErrorsList_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is TextBlock textBlock && textBlock.DataContext is ErrorEntry entry)
        {
            _onJump?.Invoke(entry.Offset);
            Close();
        }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
