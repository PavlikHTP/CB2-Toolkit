using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CB2Toolkit.CodeEditor.Utils;
using CB2Toolkit.Core.Models;
using CB2Toolkit.Core.Models.Enums;
using CB2Toolkit.Core.Models.Settings;
using CB2Toolkit.Core.Services;
using Microsoft.Win32;

namespace CB2Toolkit.UIEditor.Views;

public partial class UIEditorView : UserControl
{
    private TerminalExecutionService _terminalService = new();
    private bool _isDragging;
    private Point _dragStartPoint;
    private UIElementModel _draggedElement;
    private double _originalElementX;
    private double _originalElementY;

    public ObservableCollection<UIElementModel> Elements { get; set; } = new();

    public UIEditorView()
    {
        InitializeComponent();
        ElementsList.ItemsSource = Elements;
        VisualPreviewContainer.ItemsSource = Elements;

        Loaded += (s, e) =>
        {
            _terminalService.OutputReceived +=
                text => Dispatcher.Invoke(() => LoggerService.Instance.Log(text, "#D4D4D4"));
            _terminalService.ErrorReceived +=
                text => Dispatcher.Invoke(() => LoggerService.Instance.Log(text, "#CD5C5C"));
            LoggerService.Instance.OnLogAdded += entry => Dispatcher.Invoke(() => ConsoleOutput.Items.Add(entry));
            LoggerService.Instance.OnLogCleared += () => Dispatcher.Invoke(() => ConsoleOutput.Items.Clear());

            var settings = SettingsService.Instance.Current;
            if (!string.IsNullOrEmpty(settings.UIEditorCompilePath))
            {
                CompilePathInput.Text = settings.UIEditorCompilePath;
            }
        };
    }

    private void BackToMenu_Click(object sender, RoutedEventArgs e)
    {
        Window currentWindow = Window.GetWindow(this);
        if (currentWindow != null)
        {
            dynamic mainWindow = currentWindow;
            mainWindow.NavigateToMenu();
        }
    }

    private void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Project Folder"
        };

        if (dialog.ShowDialog() == true)
        {
            CompilePathInput.Text = dialog.FolderName;
            SettingsService.Instance.Current.UIEditorCompilePath = dialog.FolderName;
            _ = SettingsService.Instance.SaveAsync();
            LoggerService.Instance.LogInfo($"Selected project workspace: {dialog.FolderName}");
        }
    }

    private void ImportUI_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import UI Script",
            Filter = "AngelScript (*.as)|*.as|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                ParseUIFile(dialog.FileName);
                LoggerService.Instance.LogInfo($"Successfully imported UI layout from {Path.GetFileName(dialog.FileName)}");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"Failed to import UI workspace: {ex.Message}");
            }
        }
    }

    private void ParseUIFile(string path)
    {
        string[] lines = File.ReadAllLines(path);
        var tempDict = new Dictionary<string, UIElementModel>();
        Elements.Clear();

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var createMatch = Regex.Match(line, @"^(?<name>\w+)\[idx\]\s*=\s*gfx\.Create(?<type>\w+)\((?<args>.*)\);");
            if (createMatch.Success)
            {
                string name = createMatch.Groups["name"].Value;
                string type = createMatch.Groups["type"].Value;
                string[] args = SplitArgs(createMatch.Groups["args"].Value);

                var el = new UIElementModel { Name = name, R = 255, G = 255, B = 255, Opacity = 1.0 };

                if (type == "Rect")
                {
                    el.Type = ElementType.Rect; el.Icon = "🟦";
                    el.X = ParseDouble(args[1]); el.Y = ParseDouble(args[2]); el.Width = ParseDouble(args[3]); el.Height = ParseDouble(args[4]);
                }
                else if (type == "Oval")
                {
                    el.Type = ElementType.Oval; el.Icon = "⭕";
                    el.X = ParseDouble(args[1]); el.Y = ParseDouble(args[2]); el.Width = ParseDouble(args[3]); el.Height = ParseDouble(args[4]);
                }
                else if (type == "Text")
                {
                    el.Type = ElementType.Text; el.Icon = "📝";
                    el.Text = ExtractString(args[2]);
                    el.X = ParseDouble(args[3]); el.Y = ParseDouble(args[4]); el.Width = 1.0; el.Height = 1.0; 
                }
                else if (type == "Image")
                {
                    el.Type = ElementType.Image; el.Icon = "🖼";
                    el.Text = ExtractString(args[1]);
                    el.X = ParseDouble(args[2]); el.Y = ParseDouble(args[3]); el.Width = ParseDouble(args[4]); el.Height = ParseDouble(args[5]);
                }
                else if (type == "ProgressBar")
                {
                    el.Type = ElementType.ProgressBar; el.Icon = "📊";
                    el.MiscValue = args[1].Trim();
                    el.X = ParseDouble(args[2]); el.Y = ParseDouble(args[3]); el.Width = ParseDouble(args[4]); el.Height = ParseDouble(args[5]);
                }

                tempDict[name] = el;
                Elements.Add(el);
                continue;
            }

            var propMatch = Regex.Match(line, @"^(?<name>\w+)\[idx\]\.(?<method>SetColor|SetOpacity|SetScale|SetCallback)\((?<args>.*)\);");
            if (propMatch.Success)
            {
                string name = propMatch.Groups["name"].Value;
                string method = propMatch.Groups["method"].Value;
                string[] args = SplitArgs(propMatch.Groups["args"].Value);

                if (tempDict.TryGetValue(name, out var el))
                {
                    if (method == "SetColor")
                    {
                        el.R = int.Parse(args[0]); el.G = int.Parse(args[1]); el.B = int.Parse(args[2]);
                    }
                    else if (method == "SetOpacity")
                    {
                        el.Opacity = ParseDouble(args[0].Replace("f", ""));
                    }
                    else if (method == "SetScale")
                    {
                        el.Width = ParseDouble(args[0]); el.Height = ParseDouble(args[1]);
                    }
                    else if (method == "SetCallback")
                    {
                        el.MiscValue = ExtractString(args[0]);
                    }
                }
            }
        }

        if (Elements.Count > 0) ElementsList.SelectedIndex = 0;
    }

    private double ParseDouble(string val)
    {
        if (double.TryParse(val.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result)) return result;
        return 0.0;
    }

    private string ExtractString(string val)
    {
        return val.Trim().Trim('"');
    }

    private string[] SplitArgs(string argsLine)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        foreach (char c in argsLine)
        {
            if (c == '\"') inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }
            current.Append(c);
        }
        result.Add(current.ToString());
        return result.ToArray();
    }

    private void ElementsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        PropertiesPanel.Visibility = ElementsList.SelectedItem != null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddElement(ElementType type, string defaultName, string icon)
    {
        var el = new UIElementModel
        {
            Name = $"{defaultName}{Elements.Count + 1}",
            Type = type,
            Icon = icon,
            X = 0.35,
            Y = 0.35,
            Width = type == ElementType.Text ? 1.0 : 0.4,
            Height = type == ElementType.Text ? 1.0 : 0.2,
            R = 255,
            G = 255,
            B = 255,
            Opacity = 1.0,
            Text = type == ElementType.Text ? "SAMPLE TEXT NODE" : (type == ElementType.Image ? "ui_bg.webm" : ""),
            MiscValue = type == ElementType.ProgressBar ? "5.0" : ""
        };
        Elements.Add(el);
        ElementsList.SelectedItem = el;
    }

    private void AddRect_Click(object sender, RoutedEventArgs e) => AddElement(ElementType.Rect, "background", "🟦");
    private void AddOval_Click(object sender, RoutedEventArgs e) => AddElement(ElementType.Oval, "circleNode", "⭕");
    private void AddText_Click(object sender, RoutedEventArgs e) => AddElement(ElementType.Text, "titleText", "📝");
    private void AddImage_Click(object sender, RoutedEventArgs e) => AddElement(ElementType.Image, "textureNode", "🖼");

    private void AddProgressBar_Click(object sender, RoutedEventArgs e) =>
        AddElement(ElementType.ProgressBar, "loadingBar", "📊");

    private void DeleteElement_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelectedElement();
    }

    private void DeleteSelectedElement()
    {
        if (ElementsList.SelectedItem is UIElementModel model)
        {
            Elements.Remove(model);
            LoggerService.Instance.LogInfo($"Removed element: {model.Name}");
        }
    }

    private void PreviewWorkspace_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var canvas = sender as Canvas;
        if (canvas == null) return;

        var hitResult = VisualTreeHelper.HitTest(canvas, e.GetPosition(canvas));
        if (hitResult == null) return;

        DependencyObject depObj = hitResult.VisualHit;
        
        while (depObj != null && depObj != canvas)
        {
            if (depObj is FrameworkElement fe && fe.DataContext is UIElementModel model)
            {
                ElementsList.SelectedItem = model;
                _draggedElement = model;
                _isDragging = true;
                _dragStartPoint = e.GetPosition(canvas);
                _originalElementX = model.X;
                _originalElementY = model.Y;
                canvas.CaptureMouse();
                e.Handled = true;
                return;
            }

            depObj = VisualTreeHelper.GetParent(depObj);
        }
    }

    private void PreviewWorkspace_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            var canvas = sender as Canvas;
            canvas?.ReleaseMouseCapture();
            _isDragging = false;
            _draggedElement = null;
        }
    }

    private void PreviewWorkspace_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && _draggedElement != null)
        {
            var canvas = sender as Canvas;
            if (canvas == null || canvas.ActualWidth == 0 || canvas.ActualHeight == 0) return;

            Point currentPoint = e.GetPosition(canvas);
            double deltaX = currentPoint.X - _dragStartPoint.X;
            double deltaY = currentPoint.Y - _dragStartPoint.Y;

            double normDeltaX = deltaX / canvas.ActualWidth;
            double normDeltaY = deltaY / canvas.ActualHeight;

            double maxValidX = Math.Max(0.0, 1.0 - _draggedElement.Width);
            double maxValidY = Math.Max(0.0, 1.0 - _draggedElement.Height);

            double targetX = Math.Clamp(_originalElementX + normDeltaX, 0.0, maxValidX);
            double targetY = Math.Clamp(_originalElementY + normDeltaY, 0.0, maxValidY);

            _draggedElement.X = Math.Round(targetX, 4);
            _draggedElement.Y = Math.Round(targetY, 4);
        }
    }

    private void PropertyInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (sender is TextBox textBox)
            {
                BindingExpression be = textBox.GetBindingExpression(TextBox.TextProperty);
                be?.UpdateSource();
                Keyboard.ClearFocus();
            }
        }
    }

    private void ResizeThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (sender is Thumb thumb && thumb.DataContext is UIElementModel model)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(thumb);
            while (parent != null && !(parent is Canvas))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            if (parent is Canvas canvas && canvas.ActualWidth > 0 && canvas.ActualHeight > 0)
            {
                double normDeltaX = e.HorizontalChange / canvas.ActualWidth;
                double normDeltaY = e.VerticalChange / canvas.ActualHeight;

                double maxWidth = Math.Max(0.01, 1.0 - model.X);
                double maxHeight = Math.Max(0.01, 1.0 - model.Y);

                double targetWidth = Math.Clamp(model.Width + normDeltaX, 0.01, maxWidth);
                double targetHeight = Math.Clamp(model.Height + normDeltaY, 0.01, maxHeight);

                model.Width = Math.Round(targetWidth, 4);
                model.Height = Math.Round(targetHeight, 4);
            }
        }
    }
    
    private void View_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        HotkeySettings hotkeys = SettingsService.Instance.Current.Hotkeys;

        if (HotkeyMatcher.IsMatch(e, hotkeys.DeleteFile, hotkeys.DeleteFileModifiers))
        {
            DeleteSelectedElement();
            e.Handled = true;
            return;
        }

        if (HotkeyMatcher.IsMatch(e, hotkeys.RunCompilerKey, hotkeys.RunCompilerModifiers))
        {
            CompileUI();
            e.Handled = true;
            return;
        }
    }

    private void GenerateUI_Click(object sender, RoutedEventArgs e)
    {
        CompileUI();
    }

    private async void CompileUI()
    {
        if (string.IsNullOrWhiteSpace(CompilePathInput.Text) || string.IsNullOrWhiteSpace(OutputNameInput.Text))
        {
            LoggerService.Instance.LogError(
                "Configuration failure: Specify output directory and naming layout workspace parameters.");
            return;
        }

        LoggerService.Instance.LogInfo("Triggering visual workspace script generator pipeline...");

        try
        {
            string fileName = OutputNameInput.Text.Trim();
            if (!fileName.EndsWith(".as")) fileName += ".as";

            string outputPath = Path.Combine(CompilePathInput.Text, fileName);
            StringBuilder sb = new StringBuilder();
            
            sb.AppendLine();
            sb.AppendLine("namespace UI");
            sb.AppendLine("{");

            foreach (var el in Elements)
            {
                sb.AppendLine($"    GUIElement[] {el.Name};");
            }

            sb.AppendLine("    bool[] states;");
            sb.AppendLine();

            sb.AppendLine("    void Load(uint count)");
            sb.AppendLine("    {");
            foreach (var el in Elements)
            {
                sb.AppendLine($"        {el.Name}.resize(count);");
            }

            sb.AppendLine("        states.resize(count);");
            sb.AppendLine("        for(uint i = 0; i < count; i++) states[i] = false;");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    void Unload()");
            sb.AppendLine("    {");
            sb.AppendLine("        for(uint i = 0; i < states.size(); i++) { if(states[i]) Hide(i); }");
            foreach (var el in Elements)
            {
                sb.AppendLine($"        {el.Name}.resize(0);");
            }

            sb.AppendLine("        states.resize(0);");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    void Toggle(Player player, int idx)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (states[idx]) Hide(idx);");
            sb.AppendLine("        else Show(player, idx);");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    void Show(Player player, int idx)");
            sb.AppendLine("    {");
            sb.AppendLine("        Graphics gfx;");
            sb.AppendLine("        Server srv;");

            foreach (var el in Elements)
            {
                sb.AppendLine();
                string invariantX = el.X.ToString("F4", CultureInfo.InvariantCulture);
                string invariantY = el.Y.ToString("F4", CultureInfo.InvariantCulture);
                string invariantW = el.Width.ToString("F4", CultureInfo.InvariantCulture);
                string invariantH = el.Height.ToString("F4", CultureInfo.InvariantCulture);
                string invariantOpacity = el.Opacity.ToString("F4", CultureInfo.InvariantCulture);

                string call = "";
                switch (el.Type)
                {
                    case ElementType.Rect:
                        call = $"gfx.CreateRect(player, {invariantX}, {invariantY}, {invariantW}, {invariantH})";
                        break;
                    case ElementType.Oval:
                        call = $"gfx.CreateOval(player, {invariantX}, {invariantY}, {invariantW}, {invariantH})";
                        break;
                    case ElementType.Text:
                        call = $"gfx.CreateText(player, 0, \"{el.Text}\", {invariantX}, {invariantY}, false)";
                        break;
                    case ElementType.Image:
                        call = $"gfx.CreateImage(player, \"{el.Text}\", {invariantX}, {invariantY}, {invariantW}, {invariantH})";
                        break;
                    case ElementType.ProgressBar:
                        string pTime = string.IsNullOrWhiteSpace(el.MiscValue) ? "5.0" : el.MiscValue;
                        call = $"gfx.CreateProgressBar(player, {pTime}, {invariantX}, {invariantY}, {invariantW}, {invariantH})";
                        break;
                }

                sb.AppendLine($"        {el.Name}[idx] = {call};");
                sb.AppendLine($"        {el.Name}[idx].SetColor({el.R}, {el.G}, {el.B});");
                sb.AppendLine($"        {el.Name}[idx].SetOpacity({invariantOpacity}, 0.0f);");

                if (el.Type == ElementType.Text)
                {
                    sb.AppendLine($"        {el.Name}[idx].SetScale({invariantW}, {invariantH});");
                }

                if (!string.IsNullOrWhiteSpace(el.MiscValue) && el.Type != ElementType.ProgressBar)
                {
                    sb.AppendLine($"        {el.Name}[idx].SetCallback(\"{el.MiscValue}\");");
                }
            }

            sb.AppendLine();
            sb.AppendLine("        states[idx] = true;");
            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine("    void Hide(int idx)");
            sb.AppendLine("    {");
            foreach (var el in Elements)
            {
                sb.AppendLine($"        {el.Name}[idx].Remove();");
            }

            sb.AppendLine("        states[idx] = false;");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8);
            LoggerService.Instance.LogInfo(
                $"Successfully compiled operational layout asset package assembly to: {outputPath}");
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"Compiler framework abort processing error exceptions: {ex.Message}");
        }
    }

    private void ClearConsole_Click(object sender, RoutedEventArgs e)
    {
        LoggerService.Instance.Clear();
    }

    private void CopyConsole_Click(object sender, RoutedEventArgs e)
    {
        if (ConsoleOutput.Items.Count == 0) return;

        var sb = new StringBuilder();
        foreach (var item in ConsoleOutput.Items)
        {
            if (item != null)
            {
                dynamic entry = item;
                try
                {
                    sb.AppendLine(entry.Text);
                }
                catch
                {
                    sb.AppendLine(item.ToString());
                }
            }
        }

        try
        {
            Clipboard.SetText(sb.ToString());
        }
        catch
        {
        }
    }

    private void AutoscrollToggle_Checked(object sender, RoutedEventArgs e)
    {
        ScrollConsoleToBottom();
    }

    private void ScrollConsoleToBottom()
    {
        if (ConsoleScrollViewer == null) return;
        ConsoleScrollViewer.Dispatcher.InvokeAsync(
            () => { ConsoleScrollViewer.ScrollToEnd(); }, DispatcherPriority.Background);
    }

    private async void TerminalInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            string command = TerminalInput.Text.Trim();
            if (string.IsNullOrEmpty(command)) return;

            TerminalInput.Text = string.Empty;
            LoggerService.Instance.Log($"> {command}", "#808080");
            if (AutoscrollToggle.IsChecked == true) ScrollConsoleToBottom();

            string workDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(CompilePathInput.Text))
            {
                workDir = CompilePathInput.Text;
            }

            await _terminalService.ExecuteAsync(command, workDir);

            if (AutoscrollToggle.IsChecked == true) ScrollConsoleToBottom();
        }
    }
}

public class NormalizedToAbsoluteConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is double norm && values[1] is double dimension)
        {
            return norm * dimension;
        }

        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => null;
}

public class RgbToBrushConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 4 && values[0] is int r && values[1] is int g && values[2] is int b &&
            values[3] is double opacity)
        {
            byte alpha = (byte)Math.Clamp(opacity * 255, 0, 255);
            return new SolidColorBrush(Color.FromArgb(alpha, (byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255),
                (byte)Math.Clamp(b, 0, 255)));
        }

        return Brushes.Transparent;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => null;
}