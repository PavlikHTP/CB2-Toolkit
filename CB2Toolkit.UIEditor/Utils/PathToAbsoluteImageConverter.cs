using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace CB2Toolkit.UIEditor.Utils;

public class PathToAbsoluteImageConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is string relPath && values[1] is string basePath)
        {
            if (string.IsNullOrWhiteSpace(relPath)) return null;
            try
            {
                string fullPath = Path.IsPathRooted(relPath) ? relPath : Path.Combine(basePath ?? "", relPath);
                if (File.Exists(fullPath))
                {
                    System.Windows.Media.Imaging.BitmapImage bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(fullPath);
                    bitmap.EndInit();
                    return bitmap;
                }
            }
            catch
            {
            }
        }
        return null;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => null;
}