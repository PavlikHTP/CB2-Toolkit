using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CB2Toolkit.UIEditor.Utils;

public class RgbToBrushConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 4 && values[0] is int r && values[1] is int g && values[2] is int b &&
            values[3] is double opacity)
        {
            return new SolidColorBrush(Color.FromArgb((byte)Math.Clamp(opacity * 255, 0, 255), (byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255),
                (byte)Math.Clamp(b, 0, 255)));
        }

        return Brushes.Transparent;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => null;
}