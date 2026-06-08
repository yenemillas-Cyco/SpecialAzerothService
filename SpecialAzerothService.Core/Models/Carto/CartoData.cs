namespace SpecialAzerothService.Core.Models.Carto;

public sealed class CartoData
{
    /// <summary>Utilisateurs Carto (regroupement de comptes WTF).</summary>
    public List<CartoUser> Users { get; set; } = [];

    /// <summary>Visibilité roster par utilisateur et catégorie de perso.</summary>
    public List<CartoCategoryPolicy> CategoryPolicies { get; set; } = [];

    /// <summary>Clé = dossier WTF — nom affiché et rattachement utilisateur.</summary>
    public Dictionary<string, CartoAccountConfig> AccountSettings { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Obsolète — migré vers AccountSettings au chargement.</summary>
    public Dictionary<string, string> AccountDisplayNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<WowAccount> Accounts { get; set; } = [];

    /// <summary>Catégorie + note par perso WowSync (format v2, clé Nom-Royaume).</summary>
    public List<CartoCharacterProfile> CharacterProfiles { get; set; } = [];

    /// <summary>Carte, métiers, CDs, quêtes, etc. (pas catégorie / note — voir CharacterProfiles).</summary>
    public List<CartoCharacterExtras> CharacterExtras { get; set; } = [];

    /// <summary>Obsolète — migré vers CharacterExtras au chargement.</summary>
    public List<WowCharacter> Characters { get; set; } = [];

    public List<MapTimer> Timers { get; set; } = [];

    /// <summary>False au premier lancement : initialise la visibilité CD (Moi seul par défaut).</summary>
    public bool CooldownRosterVisibilityConfigured { get; set; }
}
