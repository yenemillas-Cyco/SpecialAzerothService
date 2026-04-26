using System.Globalization;
using System.Windows.Data;

namespace WindowsOrganiserApp.Converters;

public class BoolToMainLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? "★ Leader" : "Set Leader";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
