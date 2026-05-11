using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WindowsOrganiserApp.Models.Carto;

namespace WindowsOrganiserApp.Converters;

public class WowClassColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is WowClass wowClass)
        {
            var hex = WowClassColors.GetHexColor(wowClass);
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class CooldownRemainingConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is CooldownEntry cd)
        {
            if (cd.IsReady) return "PRÊT";
            var remaining = cd.TimeRemaining;
            if (remaining == null) return "—";
            return remaining.Value.TotalHours >= 1
                ? $"{(int)remaining.Value.TotalHours}h {remaining.Value.Minutes}m"
                : $"{remaining.Value.Minutes}m";
        }
        return "—";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class NullableEnumConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value;

    public object? ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
        => value;
}
