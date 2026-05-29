using System.Globalization;
using System.Windows.Data;

namespace WindowsOrganiserApp.Converters;

/// <summary>Divise une largeur (ex. viewport) pour un WrapPanel à N colonnes.</summary>
public sealed class DivideWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var divisor = 2.0;
        var padding = 20.0;
        if (parameter is string param)
        {
            var parts = param.Split(';', ',');
            if (parts.Length > 0 && double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                divisor = d;
            if (parts.Length > 1 && double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
                padding = p;
        }

        if (value is double width && width > padding && divisor > 0)
            return Math.Max(180, (width - padding) / divisor);

        return 400.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
