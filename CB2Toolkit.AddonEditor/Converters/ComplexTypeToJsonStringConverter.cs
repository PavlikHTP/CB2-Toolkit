namespace CB2Toolkit.AddonEditor.Converters;

public class ComplexTypeToJsonStringConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value == null) return string.Empty;

        Type type = value.GetType();

        if (type == typeof(string) || type.IsPrimitive || value is decimal)
        {
            return value;
        }

        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = false
            };
            return System.Text.Json.JsonSerializer.Serialize(value, options);
        }
        catch
        {
            return value.ToString();
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is string jsonStr && !string.IsNullOrWhiteSpace(jsonStr))
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize(jsonStr, targetType);
            }
            catch
            {
                return System.Windows.Data.Binding.DoNothing;
            }
        }
        return value;
    }
}

