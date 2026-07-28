using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace CB2Toolkit.Views;

public partial class PluginWarningDialog : Window
{
    private readonly DispatcherTimer _countdownTimer;
    private int _remainingSeconds = 10;
    public bool UserAccepted { get; private set; }

    public PluginWarningDialog()
    {
        InitializeComponent();
        
        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += CountdownTimer_Tick;
        _countdownTimer.Start();
    }

    private void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        _remainingSeconds--;

        if (_remainingSeconds <= 0)
        {
            _countdownTimer.Stop();
            BtnAllow.IsEnabled = true;
            CountdownText.Text = "You may now enable plugins.";
            CountdownText.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9CA3AF"));
        }
        else
        {
            CountdownText.Text = $"You can enable plugins in {_remainingSeconds} seconds...";
        }
    }

    private void BtnAllow_Click(object sender, RoutedEventArgs e)
    {
        UserAccepted = true;
        DialogResult = true;
    }

    private void BtnDeny_Click(object sender, RoutedEventArgs e)
    {
        UserAccepted = false;
        DialogResult = false;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        UserAccepted = false;
        DialogResult = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        _countdownTimer.Stop();
        base.OnClosed(e);
    }
}
