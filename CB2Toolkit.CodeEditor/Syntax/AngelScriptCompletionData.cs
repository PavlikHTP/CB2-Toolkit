using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CB2Toolkit.CodeEditor.Models.Enums;
using ICSharpCode.AvalonEdit.CodeCompletion;

namespace CB2Toolkit.CodeEditor.Syntax;

public class AngelScriptCompletionData(string text, CompletionType type = CompletionType.Field) : ICompletionData
{
    public string Text { get; private set; } = text;
    public CompletionType Type { get; private set; } = type;

    public object Content 
    {
        get
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            
            var badgeBorder = new Border
            {
                Width = 16,
                Height = 16,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            
            var badgeText = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            switch (Type)
            {
                case CompletionType.Keyword:
                    badgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CE7E3B"));
                    badgeText.Text = "K";
                    badgeText.Foreground = Brushes.White;
                    break;
                case CompletionType.Class:
                    badgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4EC9B0"));
                    badgeText.Text = "C";
                    badgeText.Foreground = Brushes.Black;
                    break;
                case CompletionType.Function:
                    badgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCDCAA"));
                    badgeText.Text = "F";
                    badgeText.Foreground = Brushes.Black;
                    break;
                case CompletionType.Field:
                    badgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CDCFE"));
                    badgeText.Text = "V";
                    badgeText.Foreground = Brushes.Black;
                    break;
            }
            badgeBorder.Child = badgeText;

            var label = new TextBlock
            {
                Text = Text,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4D4D4")),
                FontFamily = new FontFamily("Consolas")
            };

            panel.Children.Add(badgeBorder);
            panel.Children.Add(label);
            return panel;
        }
    }

    public object? Description { get; set; }
    
    object? ICompletionData.Description => null;
    
    public double Priority => Type switch
    {
        CompletionType.Keyword => 1.0,
        CompletionType.Class => 2.0,
        CompletionType.Function => 3.0,
        _ => 4.0
    };

    public ImageSource? Image => null;

    public void Complete(ICSharpCode.AvalonEdit.Editing.TextArea textArea, ICSharpCode.AvalonEdit.Document.ISegment completionSegment, EventArgs insertionEventArgs)
    {
        string insertionText = Type == CompletionType.Function ? Text + ";" : Text;
        textArea.Document.Replace(completionSegment, insertionText);
    }
}