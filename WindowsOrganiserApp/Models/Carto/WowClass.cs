namespace WindowsOrganiserApp.Models.Carto;

public enum WowClass
{
    Guerrier,
    Paladin,
    Chasseur,
    Voleur,
    Pretre,
    Chaman,
    Mage,
    Demoniste,
    Druide
}

public static class WowClassColors
{
    public static string GetHexColor(WowClass wowClass) => wowClass switch
    {
        WowClass.Guerrier => "#C79C6E",
        WowClass.Paladin => "#F58CBA",
        WowClass.Chasseur => "#ABD473",
        WowClass.Voleur => "#FFF569",
        WowClass.Pretre => "#FFFFFF",
        WowClass.Chaman => "#0070DE",
        WowClass.Mage => "#69CCF0",
        WowClass.Demoniste => "#9482C9",
        WowClass.Druide => "#FF7D0A",
        _ => "#CCCCCC"
    };
}
