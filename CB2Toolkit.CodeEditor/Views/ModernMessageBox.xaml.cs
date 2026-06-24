using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CB2Toolkit.CodeEditor.Models;
using CB2Toolkit.CodeEditor.Models.Enums;

namespace CB2Toolkit.CodeEditor.Views;

public partial class ModernMessageBox : Window
{
    private static readonly Geometry InfoGeometry = Geometry.Parse("M12,2C6.48,2,2,6.48,2,12s4.48,10,10,10,10-4.48,10-10S17.52,2,12,2z M10,17l-5,-5,1.41-1.41L10,14.17l7.59-7.59L19,8,10,17z");
    private static readonly Geometry ErrorGeometry = Geometry.Parse("M12,2C6.47,2,2,6.47,2,12s4.47,10,10,10,10-4.47,10-10S17.53,2,12,2z M17,15.59,15.59,17,12,13.41,8.41,17,7,15.59,10.59,12,7,8.41,8.41,7,12,10.59,15.59,7,17,8.41,13.41,12,17,15.59z");
    private static readonly Geometry QuestionGeometry = Geometry.Parse("M12,2C6.48,2,2,6.48,2,12s4.48,10,10,10,10-4.48,10-10S17.52,2,12,2z M12,18c-.55,0-1-.45-1-1s.45-1,1-1,1,.45,1,1-.45,1-1,1zm1-4.33c0,.39-.24.71-.61.85-.25.1-.39.38-.39.65v.83c0,.55-.45,1-1,1s-1-.45-1-1v-1.34c0-.85.52-1.62,1.31-1.92.31-.12.51-.4.51-.73,0-.45-.36-.81-.81-.81s-.81.36-.81.81c0,.55-.45,1-1,1s-1-.45-1-1c0-1.55,1.26-2.81,2.81-2.81s2.81,1.26,2.81,2.81c0,.92-.49,1.72-1.22,2.18z");
    private static readonly Geometry InputGeometry = Geometry.Parse("M3,17.25V21h3.75L17.81,9.94l-3.75-3.75L3,17.25z M20.71,7.04c.39-.39.39-1.02,0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41,0l-1.83,1.83 3.75,3.75 1.83-1.83z");

    private static readonly Brush InfoBrush = CreateFrozenBrush("#4D7CFE");
    private static readonly Brush ErrorBrush = CreateFrozenBrush("#EF4444");
    private static readonly Brush ErrorBorderBrush = CreateFrozenBrush("#451A1A");
    private static readonly Brush QuestionBrush = CreateFrozenBrush("#10B981");
    private static readonly Brush InputBrush = CreateFrozenBrush("#A855F7");

    private readonly ModernBoxResult _result = new();
    private readonly ModernBoxType _type;

    public ModernMessageBox(string message, string title, ModernBoxType type, string defaultInput = "")
    {
        InitializeComponent();
        
        _type = type;
        MessageTextBlock.Text = message;
        TitleTextBlock.Text = title;
        InputTextBox.Text = defaultInput;
        
        ConfigureDialog(type);
    }
    
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (_type == ModernBoxType.Question) BtnYes_Click(this, e);
            else BtnOk_Click(this, e);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (_type == ModernBoxType.Information || _type == ModernBoxType.Error) BtnOk_Click(this, e);
            else if (_type == ModernBoxType.Question) BtnNo_Click(this, e);
            else BtnCancel_Click(this, e);
        }
    }

    private void ConfigureDialog(ModernBoxType type)
    {
        switch (type)
        {
            case ModernBoxType.Information:
                StatusIcon.Data = InfoGeometry;
                StatusIcon.Fill = InfoBrush;
                BtnOk.Visibility = Visibility.Visible;
                break;

            case ModernBoxType.Error:
                StatusIcon.Data = ErrorGeometry;
                StatusIcon.Fill = ErrorBrush;
                WindowBorder.BorderBrush = ErrorBorderBrush;
                BtnOk.Visibility = Visibility.Visible;
                break;

            case ModernBoxType.Question:
                StatusIcon.Data = QuestionGeometry;
                StatusIcon.Fill = QuestionBrush;
                BtnYes.Visibility = Visibility.Visible;
                BtnNo.Visibility = Visibility.Visible;
                break;

            case ModernBoxType.Input:
                StatusIcon.Data = InputGeometry;
                StatusIcon.Fill = InputBrush;
                InputTextBox.Visibility = Visibility.Visible;
                BtnOk.Visibility = Visibility.Visible;
                BtnCancel.Visibility = Visibility.Visible;
                
                Dispatcher.InvokeAsync(() =>
                {
                    InputTextBox.Focus();
                    InputTextBox.SelectAll();
                }, DispatcherPriority.Input);
                break;
        }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        _result.Result = ModernBoxResultType.OK;
        _result.InputText = InputTextBox.Text;
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        _result.Result = ModernBoxResultType.Cancel;
        DialogResult = false;
    }

    private void BtnYes_Click(object sender, RoutedEventArgs e)
    {
        _result.Result = ModernBoxResultType.Yes;
        DialogResult = true;
    }

    private void BtnNo_Click(object sender, RoutedEventArgs e)
    {
        _result.Result = ModernBoxResultType.No;
        DialogResult = false;
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed) 
        {
            DragMove();
        }
    }
    
    private static Brush CreateFrozenBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    public static ModernBoxResult Show(Window owner, string message, string title, ModernBoxType type = ModernBoxType.Information, string defaultInput = "")
    {
        var msgBox = new ModernMessageBox(message, title, type, defaultInput)
        {
            Owner = owner
        };
        msgBox.ShowDialog();
        return msgBox._result;
    }
}