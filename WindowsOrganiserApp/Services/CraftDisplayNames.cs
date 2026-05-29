namespace WindowsOrganiserApp.Services;

public static class CraftDisplayNames
{
    /// <summary>Libellé de section dans l’onglet Crafting (ex. Alchimiste).</summary>
    public static string ProfessionGroupFr(string professionId, string nameFr) => professionId switch
    {
        "Alchemy" => "Alchimiste",
        "Engineering" => "Ingénieur",
        "Blacksmithing" => "Forgeron",
        "Leatherworking" => "Travailleur du cuir",
        "Tailoring" => "Tailleur",
        "Enchanting" => "Enchanteur",
        "Mining" => "Mineur",
        "Herbalism" => "Herboriste",
        "Cooking" => "Cuisinier",
        "FirstAid" => "Secouriste",
        "Fishing" => "Pêcheur",
        "RoguePoisons" => "Poisons",
        _ => nameFr
    };

    public static string ContentTypeFr(string contentType) => contentType switch
    {
        "Professions" => "Métiers principaux",
        "Gathering" => "Récolte",
        "Secondary" => "Secondaires",
        "Class" => "Classe",
        _ => contentType
    };

    public static string CategoryFr(string name)
    {
        if (CategoryFrMap.TryGetValue(name, out var exact))
            return exact;

        return name
            .Replace("Weapons", "Armes", StringComparison.OrdinalIgnoreCase)
            .Replace("Armor", "Armures", StringComparison.OrdinalIgnoreCase)
            .Replace("Enhancements", "Améliorations", StringComparison.OrdinalIgnoreCase)
            .Replace("Shields", "Boucliers", StringComparison.OrdinalIgnoreCase)
            .Replace("Daggers", "Dagues", StringComparison.OrdinalIgnoreCase)
            .Replace("Axes", "Haches", StringComparison.OrdinalIgnoreCase)
            .Replace("Maces", "Masse", StringComparison.OrdinalIgnoreCase)
            .Replace("Swords", "Epées", StringComparison.OrdinalIgnoreCase)
            .Replace("Polearms", "Armes d'hast", StringComparison.OrdinalIgnoreCase)
            .Replace("Head", "Tête", StringComparison.OrdinalIgnoreCase)
            .Replace("Shoulder", "Epaules", StringComparison.OrdinalIgnoreCase)
            .Replace("Chest", "Torse", StringComparison.OrdinalIgnoreCase)
            .Replace("Waist", "Taille", StringComparison.OrdinalIgnoreCase)
            .Replace("Legs", "Jambes", StringComparison.OrdinalIgnoreCase)
            .Replace("Feet", "Pieds", StringComparison.OrdinalIgnoreCase)
            .Replace("Hands", "Mains", StringComparison.OrdinalIgnoreCase)
            .Replace("Wrist", "Poignets", StringComparison.OrdinalIgnoreCase)
            .Replace("Weapon", "Arme", StringComparison.OrdinalIgnoreCase)
            .Replace("2H Weapon", "Arme 2M", StringComparison.OrdinalIgnoreCase)
            .Replace("Cloak", "Cape", StringComparison.OrdinalIgnoreCase)
            .Replace("Hand", "Mains", StringComparison.OrdinalIgnoreCase)
            .Replace("Shield", "Bouclier", StringComparison.OrdinalIgnoreCase)
            .Replace("Misc", "Divers", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly Dictionary<string, string> CategoryFrMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Flasks"] = "Flasques",
        ["Transmutes"] = "Transmutations",
        ["Healing/Mana Potions"] = "Potions soin / mana",
        ["Protection Potions"] = "Potions de protection",
        ["Util Potions"] = "Potions utilitaires",
        ["Combat Elixirs"] = "Élixirs de combat",
        ["Guardian Elixirs"] = "Élixirs du gardien",
        ["Flasks and Elixirs"] = "Flasques et élixirs",
        ["Misc Elixirs"] = "Élixirs divers",
        ["Misc"] = "Divers",
        ["Enhancements"] = "Améliorations",
        ["Smelting"] = "Fondre",
        ["Artisan"] = "Artisan",
        ["Expert"] = "Expert",
        ["Journeyman"] = "Compagnon",
        ["Apprentice"] = "Apprenti",
    };
}
