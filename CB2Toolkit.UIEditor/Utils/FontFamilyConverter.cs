using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using CB2Toolkit.Core.Services;
using FontEnum = CB2Toolkit.Core.Models.Enums.Fonts;

namespace CB2Toolkit.UIEditor.Utils;

public class FontFamilyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FontEnum font)
        {
            var info = FontService.Instance.GetFontInfo(font);
            return info?.Family ?? new FontFamily("Segoe UI");
        }
        return new FontFamily("Segoe UI");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class FontSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FontEnum font)
        {
            var info = FontService.Instance.GetFontInfo(font);
            return info?.Size ?? 14.0;
        }
        return 14.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}