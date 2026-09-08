using System.Windows;
using System.Windows.Controls;
using CB2Toolkit.Core.Utilities.Extensions;

namespace CB2Toolkit.CodeEditor.Utils;

public static class PathTextBoxHelper
{
    public static void Attach(TextBox textBox)
    {
        if (textBox == null) return;

        DataObject.AddPastingHandler(textBox, OnPasting);
        textBox.TextChanged += OnTextChanged;
    }

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            string? raw = e.DataObject.GetData(DataFormats.UnicodeText) as string;
            if (!string.IsNullOrEmpty(raw))
            {
                e.DataObject.SetData(DataFormats.UnicodeText, raw.SanitizePath());
            }
        }
    }

    private static void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var box = (TextBox)sender;
        if (box == null) return;

        string raw = box.Text;
        if (string.IsNullOrEmpty(raw)) return;

        string clean = raw.SanitizePath();
        if (clean == raw) return;

        int caret = box.CaretIndex;
        int cleanCaret = MapCaret(raw, clean, caret);

        box.Text = clean;
        box.CaretIndex = Math.Clamp(cleanCaret, 0, clean.Length);
    }

    private static int MapCaret(string raw, string clean, int caret)
    {
        int leadingTrim = raw.Length - raw.TrimStart().Length;

        int cleanCaret = 0;
        int i = 0;
        while (i < raw.Length && i < caret)
        {
            char c = raw[i];
            if (i < leadingTrim || c is '"' or '\'' or '`')
            {
                i++;
                continue;
            }

            cleanCaret++;
            i++;
        }

        return Math.Min(cleanCaret, clean.Length);
    }
}
