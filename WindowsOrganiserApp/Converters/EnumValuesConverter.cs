using System.Globalization;
using System.Windows.Data;

namespace WindowsOrganiserApp.Converters;

public class EnumValuesConverter : IValueConverter
{
    public static readonly EnumValuesConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not null ? Enum.GetValues(value.GetType()) : Array.Empty<object>();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
