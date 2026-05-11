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
    Transmutation,
    Etoffe_lunaire,
    Etoffe_de_lombre,
    Sel_raffine
}

public sealed class ProfessionInfo
{
    public ProfessionType Type { get; set; }
    public int Skill { get; set; }
}

public sealed class CooldownEntry
{
    public CooldownType Type { get; set; }
    public DateTime? LastUsed { get; set; }
    public string? Note { get; set; }

    public TimeSpan Duration => Type switch
    {
        CooldownType.Transmutation => TimeSpan.FromHours(24),
        CooldownType.Etoffe_lunaire => TimeSpan.FromDays(4),
        CooldownType.Etoffe_de_lombre => TimeSpan.FromDays(4),
        CooldownType.Sel_raffine => TimeSpan.FromDays(3),
        _ => TimeSpan.FromHours(24)
    };

    public DateTime? ReadyAt => LastUsed?.Add(Duration);
    public bool IsReady => LastUsed == null || DateTime.Now >= ReadyAt;
    public TimeSpan? TimeRemaining => IsReady ? null : ReadyAt - DateTime.Now;
}
