using System;
using System.Net;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CB2Toolkit.Core.Services;
using NAudio.Wave;

namespace CB2Toolkit.Views;

public partial class VoiceWindow : Window
{
    public VoiceWindow()
    {
        InitializeComponent();
        
        MuteMicToggle.IsChecked = VoiceService.Instance.IsMuted;
        DeafenToggle.IsChecked = VoiceService.Instance.IsDeafened;
        MicVolumeSlider.Value = VoiceService.Instance.MicVolumeMultiplier;
        MicVolumeValueText.Text = $"x{MicVolumeSlider.Value:F1}";

        LoadAudioDevices();

        ConnectBtn.IsEnabled = true;
        DisconnectBtn.IsEnabled = false;

        VoiceService.Instance.OnRemoteVolumeChanged += VoiceService_OnRemoteVolumeChanged;
        VoiceService.Instance.OnLocalVolumeChanged += VoiceService_OnLocalVolumeChanged;
    }

    private void LoadAudioDevices()
    {
        InputDeviceComboBox.Items.Clear();
        OutputDeviceComboBox.Items.Clear();

        try
        {
            int waveInDevices = WaveIn.DeviceCount;
            for (int i = 0; i < waveInDevices; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                InputDeviceComboBox.Items.Add(caps.ProductName);
            }

            int waveOutDevices = WaveOut.DeviceCount;
            for (int i = 0; i < waveOutDevices; i++)
            {
                var caps = WaveOut.GetCapabilities(i);
                OutputDeviceComboBox.Items.Add(caps.ProductName);
            }
        }
        catch
        {
            InputDeviceComboBox.Items.Add("Default Microphone");
            OutputDeviceComboBox.Items.Add("Default Playback Device");
        }

        if (InputDeviceComboBox.Items.Count > 0) InputDeviceComboBox.SelectedIndex = 0;
        if (OutputDeviceComboBox.Items.Count > 0) OutputDeviceComboBox.SelectedIndex = 0;
    }

    private void VoiceService_OnRemoteVolumeChanged(float volume)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (VolumeScale != null) VolumeScale.ScaleX = volume;
        }));
    }

    private void VoiceService_OnLocalVolumeChanged(float volume)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (LocalVolumeScale != null) LocalVolumeScale.ScaleX = volume;
        }));
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        VoiceService.Instance.StopCall();
        Close();
    }

    private void Connect_Click(object sender, RoutedEventArgs e)
    {
        string targetIp = FriendIpTextBox.Text.Trim();
        if (!IPAddress.TryParse(targetIp, out _) && targetIp != "localhost")
        {
            MessageBox.Show("Please enter a valid IP address.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(PortTextBox.Text.Trim(), out int targetPort) || targetPort < 1 || targetPort > 65535)
        {
            MessageBox.Show("Please enter a valid port number (1 - 65535).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            int localPort = targetPort;

            int inputDeviceIndex = InputDeviceComboBox.SelectedIndex >= 0 ? InputDeviceComboBox.SelectedIndex : 0;
            int outputDeviceIndex = OutputDeviceComboBox.SelectedIndex >= 0 ? OutputDeviceComboBox.SelectedIndex : 0;

            VoiceService.Instance.StartCall(targetIp, localPort, targetPort, inputDeviceIndex, outputDeviceIndex);

            ConnectBtn.IsEnabled = false;
            DisconnectBtn.IsEnabled = true;
            FriendIpTextBox.IsEnabled = false;
            PortTextBox.IsEnabled = false;
            InputDeviceComboBox.IsEnabled = false;
            OutputDeviceComboBox.IsEnabled = false;

            StatusDot.Fill = (Brush)FindResource("StatusConnectedBrush");
            StatusText.Text = $"Connected (Local: {localPort})";
            StatusText.Foreground = (Brush)FindResource("TextConnectedBrush");

            NetworkStatsPanel.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        VoiceService.Instance.StopCall();

        ConnectBtn.IsEnabled = true;
        DisconnectBtn.IsEnabled = false;
        FriendIpTextBox.IsEnabled = true;
        PortTextBox.IsEnabled = true;
        InputDeviceComboBox.IsEnabled = true;
        OutputDeviceComboBox.Items.Clear();
        LoadAudioDevices();
        InputDeviceComboBox.IsEnabled = true;
        OutputDeviceComboBox.IsEnabled = true;

        StatusDot.Fill = (Brush)FindResource("StatusDisconnectedBrush");
        StatusText.Text = "Disconnected";
        StatusText.Foreground = (Brush)FindResource("TextDisconnectedBrush");

        NetworkStatsPanel.Visibility = Visibility.Collapsed;
    }

    private void MuteMic_Click(object sender, RoutedEventArgs e)
    {
        VoiceService.Instance.IsMuted = MuteMicToggle.IsChecked ?? false;
    }

    private void Deafen_Click(object sender, RoutedEventArgs e)
    {
        VoiceService.Instance.IsDeafened = DeafenToggle.IsChecked ?? false;
    }

    private void MicVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VoiceService.Instance != null)
        {
            VoiceService.Instance.MicVolumeMultiplier = (float)e.NewValue;
        }
        if (MicVolumeValueText != null)
        {
            MicVolumeValueText.Text = $"x{e.NewValue:F1}";
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        VoiceService.Instance.OnRemoteVolumeChanged -= VoiceService_OnRemoteVolumeChanged;
        VoiceService.Instance.OnLocalVolumeChanged -= VoiceService_OnLocalVolumeChanged;
        base.OnClosed(e);
    }
}