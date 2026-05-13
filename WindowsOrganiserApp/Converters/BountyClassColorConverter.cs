using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WindowsOrganiserApp.Converters;

public class BountyClassColorConverter : IValueConverter
{
    private static readonly Dictionary<string, string> ClassColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Guerrier"] = "#C79C6E",
        ["Paladin"] = "#F58CBA",
        ["Chasseur"] = "#ABD473",
        ["Voleur"] = "#FFF569",
        ["Prêtre"] = "#FFFFFF",
        ["Pretre"] = "#FFFFFF",
        ["Chaman"] = "#0070DE",
        ["Mage"] = "#69CCF0",
        ["Démoniste"] = "#9482C9",
        ["Demoniste"] = "#9482C9",
        ["Druide"] = "#FF7D0A"
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string className && ClassColors.TryGetValue(className, out var hex))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        return new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
