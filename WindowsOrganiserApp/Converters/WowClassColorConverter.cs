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

public class AccountIdToNameConverter : IValueConverter
{
    public static List<WowAccount>? Accounts { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string id && Accounts is not null)
            return Accounts.FirstOrDefault(a => a.Id == id)?.Name ?? "—";
        return "—";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class ListSummaryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var param = parameter as string;
        return param switch
        {
            "prof" when value is List<ProfessionInfo> profs =>
                string.Join(", ", profs.Select(p => $"{p.Type}")),
            "cd" when value is List<CooldownEntry> cds =>
                string.Join(", ", cds.Select(c => c.IsReady ? $"{c.Type}:✅" : $"{c.Type}:⏳")),
            "qi" when value is List<QuestItemEntry> qis =>
                string.Join(", ", qis.Select(q => q.Type switch
                {
                    QuestItemType.Tete_de_Rend => "Rend",
                    QuestItemType.Tete_dOnyxia => "Ony",
                    QuestItemType.Tete_de_Nefarian => "Nef",
                    QuestItemType.Coeur_de_Hakkar => "Hakkar",
                    _ => q.Type.ToString()
                })),
            _ => ""
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
