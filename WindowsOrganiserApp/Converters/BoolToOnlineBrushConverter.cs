using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WindowsOrganiserApp.Converters;

public class BoolToOnlineBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Online = new(Color.FromRgb(80, 200, 80));
    private static readonly SolidColorBrush Offline = new(Color.FromRgb(120, 120, 120));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Online : Offline;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
