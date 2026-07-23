using System.Globalization;
using System.Windows.Data;

namespace CB2Toolkit.UIEditor.Utils;

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
