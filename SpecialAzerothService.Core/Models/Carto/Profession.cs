namespace SpecialAzerothService.Core.Models.Carto;

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
        CooldownType.Air_to_Fire => "Transmutation: Air → Feu (24h)",
        CooldownType.Fire_to_Earth => "Transmutation: Feu → Terre (24h)",
        CooldownType.Earth_to_Water => "Transmutation: Terre → Eau (24h)",
        CooldownType.Water_to_Air => "Transmutation: Eau → Air (24h)",
        CooldownType.Undeath_to_Water => "Transmutation: Mort → Eau (24h)",
        CooldownType.Water_to_Undeath => "Transmutation: Eau → Mort (24h)",
        CooldownType.Life_to_Earth => "Transmutation: Vie → Terre (24h)",
        CooldownType.Earth_to_Life => "Transmutation: Terre → Vie (24h)",
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

    /// <summary>Fin de CD imposée (ex. transmu alchimie partagée, durée variable par type).</summary>
    public DateTime? ReadyAtOverride { get; set; }

    public TimeSpan Duration => Type switch
    {
        CooldownType.Arcanite => TimeSpan.FromHours(48),
        CooldownType.Transmute_Elementaire => TimeSpan.FromHours(24),
        CooldownType.Mooncloth or CooldownType.Etoffe_lunaire => TimeSpan.FromDays(4),
        CooldownType.Etoffe_de_lombre => TimeSpan.FromDays(4),
        CooldownType.Sel_raffine => TimeSpan.FromDays(3),
        _ => TimeSpan.FromHours(24)
    };

    /// <summary>Durée réelle du CD en cours (sync ou type), pour la barre de progression.</summary>
    public TimeSpan EffectiveDuration
    {
        get
        {
            if (ReadyAtOverride is { } ready && LastUsed is { } last)
            {
                var span = ready - last;
                if (span > TimeSpan.Zero)
                    return span;
            }

            return Duration;
        }
    }

    public DateTime? EffectiveReadyAt => ReadyAtOverride ?? LastUsed?.Add(Duration);

    public DateTime? ReadyAt => EffectiveReadyAt;

    /// <summary>WowSync fournit <see cref="ReadyAtOverride"/> en UTC.</summary>
    private bool UsesUtcClock => ReadyAtOverride.HasValue;

    private DateTime ClockNow => UsesUtcClock ? DateTime.UtcNow : DateTime.Now;

    public bool IsReady
    {
        get
        {
            if (ReadyAt == null)
                return true;
            return ClockNow >= ReadyAt.Value;
        }
    }

    public TimeSpan? TimeRemaining
    {
        get
        {
            if (IsReady || ReadyAt == null)
                return null;

            var remaining = ReadyAt.Value - ClockNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    /// <summary>Avancement du CD (0 = début, 1 = prêt).</summary>
    public double ElapsedFraction
    {
        get
        {
            if (IsReady)
                return 1;

            var total = EffectiveDuration.TotalSeconds;
            if (total <= 0)
                return 1;

            var remaining = TimeRemaining?.TotalSeconds ?? 0;
            return Math.Clamp(1 - remaining / total, 0, 1);
        }
    }
}
