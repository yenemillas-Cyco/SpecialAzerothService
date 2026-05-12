namespace WindowsOrganiserApp.Models.Carto;

public enum ProfessionType
{
    Alchimie,
    Forge,
    Enchantement,
    Ingenierie,
    Herboristerie,
    Couture,
    Travail_du_cuir,
    Exploitation_miniere,
    Depecage,
    Peche,
    Cuisine,
    Secourisme
}

public enum CooldownType
{
    Arcanite,
    Transmute_Elementaire,
    Mooncloth,
    Sel_raffine,

    // Legacy (kept for backward compat)
    Transmutation,
    Etoffe_lunaire,
    Etoffe_de_lombre,
    Air_to_Fire,
    Fire_to_Earth,
    Earth_to_Water,
    Water_to_Air,
    Undeath_to_Water,
    Water_to_Undeath,
    Life_to_Earth,
    Earth_to_Life
}

public sealed class ProfessionInfo
{
    public ProfessionType Type { get; set; }
    public int Skill { get; set; }
}

public static class CooldownTypeExtensions
{
    public static string DisplayName(this CooldownType type) => type switch
    {
        CooldownType.Arcanite => "Transmutation: Arcanite (48h)",
        CooldownType.Transmute_Elementaire => "Transmutation élémentaire (24h)",
        CooldownType.Mooncloth => "Étoffe lunaire (4j)",
        CooldownType.Sel_raffine => "Sel raffiné (3j)",
        _ => type.ToString()
    };
}

public sealed class CooldownEntry
{
    public CooldownType Type { get; set; }
    public DateTime? LastUsed { get; set; }
    public string? Note { get; set; }

    public TimeSpan Duration => Type switch
    {
        CooldownType.Arcanite => TimeSpan.FromHours(48),
        CooldownType.Transmute_Elementaire => TimeSpan.FromHours(24),
        CooldownType.Mooncloth or CooldownType.Etoffe_lunaire => TimeSpan.FromDays(4),
        CooldownType.Etoffe_de_lombre => TimeSpan.FromDays(4),
        CooldownType.Sel_raffine => TimeSpan.FromDays(3),
        _ => TimeSpan.FromHours(24)
    };

    public DateTime? ReadyAt => LastUsed?.Add(Duration);
    public bool IsReady => LastUsed == null || DateTime.Now >= ReadyAt;
    public TimeSpan? TimeRemaining => IsReady ? null : ReadyAt - DateTime.Now;
}
