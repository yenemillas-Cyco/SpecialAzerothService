using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowsOrganiserApp.Converters;

/// <summary>
/// Largeur utile d'un nœud TreeView = largeur du TreeView − indentation par profondeur − marges.
/// </summary>
public sealed class TreeViewDescendantWidthConverter : IMultiValueConverter
{
    public const double HorizontalChrome = 14;
    public const double IndentPerLevel = 18;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 1 || values[0] is not double treeWidth || treeWidth < 1)
            return double.NaN;

        var depth = values.Length > 1 && values[1] is int d ? d : 0;
        var extra = parameter switch
        {
            double dbl => dbl,
            int i => i,
            string s when double.TryParse(s, NumberStyles.Any, culture, out var parsed) => parsed,
            _ => 0d
        };

        var width = treeWidth - HorizontalChrome - depth * IndentPerLevel - extra;
        return Math.Max(120, width);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Marge gauche alignée sur l'indentation TreeView (comptes sous utilisateur, etc.).</summary>
public sealed class TreeViewDepthMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var depth = value is int d ? d : 0;
        var left = depth * TreeViewDescendantWidthConverter.IndentPerLevel;
        if (parameter is string s && double.TryParse(s, NumberStyles.Any, culture, out var extraLeft))
            left += extraLeft;

        return new Thickness(left, 0, 0, 4);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
