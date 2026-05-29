namespace SpecialAzerothService.Core.Models.Conso;

public sealed record Ingredient(string Name, int Quantity);

public sealed record ConsumableRecipe(string Code, string FullName, List<Ingredient> Ingredients);

public sealed record BossConsumable(string ConsumableCode, int Quantity);

public sealed record BossInfo(string Name, List<BossConsumable> Consumables);

public sealed record MaterialLine(string Name, int Total);

public sealed record ConsoCategory(string Name, List<SelectableConsoItem> Items);

public sealed record ConsoItem(string Code, string FullName, int DefaultQty);

public sealed class SelectableConsoItem
{
    public ConsoItem Item { get; }
    public int Quantity { get; set; }
    public bool IsSelected { get; set; }
    public string Label => $"{Item.FullName} (x{Quantity})";

    public SelectableConsoItem(ConsoItem item)
    {
        Item = item;
        Quantity = item.DefaultQty;
    }
}

public static class NaxxData
{
    public static readonly List<ConsumableRecipe> Recipes =
    [
        new("RO", "Resist Ombre (Protection contre l'ombre)",
        [
            new("Palerette", 4),
            new("Tombeline", 4),
            new("Fiole plombée", 1),
            new("Feuillèrve", 1),
            new("Fiole de cristal", 1)
        ]),
        new("RG", "Resist Givre (Protection contre le givre)",
        [
            new("Eau élémentaire", 1),
            new("Feuillèrve", 1),
            new("Fiole de cristal", 1)
        ]),
        new("RN", "Resist Nature (Protection contre la nature)",
        [
            new("Terre élémentaire", 1),
            new("Feuillèrve", 1),
            new("Fiole de cristal", 1)
        ]),
        new("RA", "Resist Arcane (Protection contre les arcanes)",
        [
            new("Poussière de rêve", 1),
            new("Feuillèrve", 1),
            new("Fiole de cristal", 1)
        ]),
        new("RF", "Resist Feu (Protection contre le feu)",
        [
            new("Feu élémentaire", 1),
            new("Feuillèrve", 1),
            new("Fiole de cristal", 1)
        ]),
        new("Magesang", "Magesang",
        [
            new("Feuillèrve", 1),
            new("Fleur de peste", 2),
            new("Fiole de cristal", 1)
        ]),
        new("Sagesse", "Sagesse distillée",
        [
            new("Feuillèrve", 30),
            new("Calot de glace", 10),
            new("Lotus Noir", 1),
            new("Fiole de cristal", 1)
        ]),
        new("HuileMana", "Huile de mana",
        [
            new("Grand éclat brillant", 2),
            new("Lotus pourpre", 3),
            new("Fiole imprégnée", 1)
        ]),
        new("Robustesse", "Elixir de robustesse",
        [
            new("Acérite sauvage", 1),
            new("Dorépine", 1),
            new("Fiole plombée", 1)
        ]),
        new("Mangouste", "Elixir de la Mangouste",
        [
            new("Sauge-argent des montagnes", 2),
            new("Fleur de peste", 2),
            new("Fiole de cristal", 1)
        ]),
        new("FlaconTitans", "Flacon des Titans",
        [
            new("Gromsblood", 30),
            new("Anguille de Rocastone", 10),
            new("Lotus Noir", 1),
            new("Fiole de cristal", 1)
        ]),
        new("Geants", "Elixir de puissance des Géants",
        [
            new("Tournesol", 1),
            new("Gromsblood", 1),
            new("Fiole de cristal", 1)
        ]),
        new("FlaconSupreme", "Flacon de pouvoir suprême",
        [
            new("Feuillèrve", 7),
            new("Sauge-argent des montagnes", 3),
            new("Lotus Noir", 1),
            new("Fiole de cristal", 1)
        ]),
        new("ShadowPower", "Elixir de puissance de l'ombre",
        [
            new("Champignon fantôme", 3),
            new("Fiole de cristal", 1)
        ]),
        new("ArcaneElixir", "Elixir des arcanes supérieur",
        [
            new("Feuillèrve", 3),
            new("Sauge-argent des montagnes", 1),
            new("Fiole de cristal", 1)
        ]),
        new("Sapeur", "Charge de sapeur gobelin",
        [
            new("Barre de mithril", 1),
            new("Etoffe de tisse-mage", 2),
            new("Pierre solide", 4),
            new("Poudre noire solide", 4)
        ])
    ];

    public static readonly List<BossInfo> Bosses =
    [
        new("Horreb",
        [
            new("RO", 2)
        ]),
        new("Fearlina",
        [
            new("RN", 1),
            new("RF", 1)
        ]),
        new("Gluth",
        [
            new("RN", 1)
        ]),
        new("Thaddius",
        [
            new("RN", 1)
        ]),
        new("Gotthic",
        [
            new("RA", 1)
        ]),
        new("4 Cavaliers",
        [
            new("RF", 1),
            new("RO", 3)
        ]),
        new("Saphirron",
        [
            new("RG", 3),
            new("RO", 1)
        ]),
        new("KT",
        [
            new("RG", 4)
        ])
    ];

    public static readonly List<ConsoCategory> ExtraCategories =
    [
        new("Consommables",
        [
            new(new ConsoItem("Mangouste", "Elixir de la Mangouste", 1)),
            new(new ConsoItem("Geants", "Elixir puissance des Géants", 1)),
            new(new ConsoItem("FlaconTitans", "Flacon des Titans", 1)),
            new(new ConsoItem("ArcaneElixir", "Elixir des arcanes supérieur", 1)),
            new(new ConsoItem("ShadowPower", "Elixir puissance de l'ombre", 1)),
            new(new ConsoItem("FlaconSupreme", "Flacon de pouvoir suprême", 1)),
            new(new ConsoItem("Magesang", "Magesang", 1)),
            new(new ConsoItem("Sagesse", "Sagesse distillée", 1)),
            new(new ConsoItem("HuileMana", "Huile de mana", 1)),
            new(new ConsoItem("Robustesse", "Elixir de robustesse", 1)),
            new(new ConsoItem("Sapeur", "Charge de sapeur gobelin", 1)),
            new(new ConsoItem("RO", "Resist Ombre", 1)),
            new(new ConsoItem("RG", "Resist Givre", 1)),
            new(new ConsoItem("RN", "Resist Nature", 1)),
            new(new ConsoItem("RA", "Resist Arcane", 1)),
            new(new ConsoItem("RF", "Resist Feu", 1))
        ])
    ];
}
