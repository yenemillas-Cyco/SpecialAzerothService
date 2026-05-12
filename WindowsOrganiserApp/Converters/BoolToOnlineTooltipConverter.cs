using System.Globalization;
using System.Windows.Data;

namespace WindowsOrganiserApp.Converters;

public class BoolToOnlineTooltipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? "En ligne" : "Hors ligne";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
