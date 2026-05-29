using System.Text.Json;
using System.Text.Json.Serialization;

var readOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var writeOpts = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

var repoRoot = FindRepoRoot();
var craftPath = Path.Combine(repoRoot, "WindowsOrganiserApp", "Assets", "Craft.json");
var guidesPath = Path.Combine(repoRoot, "WindowsOrganiserApp", "Assets", "CraftLevelingGuides.json");
var outputPath = Path.Combine(repoRoot, "WindowsOrganiserApp", "Assets", "CraftLevelingLists.seed.json");

var craftDb = JsonSerializer.Deserialize<CraftDatabase>(await File.ReadAllTextAsync(craftPath), readOpts)
                ?? throw new InvalidOperationException("Craft.json invalide.");
var guidesFile = JsonSerializer.Deserialize<GuidesFile>(await File.ReadAllTextAsync(guidesPath), readOpts)
                 ?? throw new InvalidOperationException("CraftLevelingGuides.json invalide.");

var lists = new List<CraftListDefinition>();
var warnings = new List<string>();

foreach (var guide in guidesFile.Guides)
{
    var profession = craftDb.Professions.FirstOrDefault(p =>
        p.Id.Equals(guide.ProfessionId, StringComparison.OrdinalIgnoreCase));
    if (profession == null)
    {
        warnings.Add($"Métier introuvable : {guide.ProfessionId}");
        continue;
    }

    var list = new CraftListDefinition
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = guide.ListName,
        Items = []
    };

    foreach (var step in guide.Steps)
    {
        if (!TryResolveEntry(profession, step.Label, out var entry))
        {
            warnings.Add($"[{guide.ListName}] recette introuvable : {step.Label}");
            continue;
        }

        if (entry.IsItemEntry || (entry.CreatedItemId <= 0 && entry.SpellId <= 0))
        {
            warnings.Add($"[{guide.ListName}] entrée non craftable ignorée : {step.Label}");
            continue;
        }

        list.Items.Add(new CraftListItem
        {
            ItemId = entry.CreatedItemId,
            SpellId = entry.SpellId,
            Quantity = Math.Max(1, step.Qty),
            ProfessionId = profession.Id
        });
    }

    lists.Add(list);
    Console.WriteLine($"{list.Name}: {list.Items.Count} recettes");
}

var seed = new CraftListsData { Version = 1, Lists = lists };
await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(seed, writeOpts));
Console.WriteLine($"Écrit : {outputPath}");

if (warnings.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("Avertissements :");
    foreach (var w in warnings)
        Console.WriteLine("  - " + w);
}

static bool TryResolveEntry(CraftProfession profession, string label, out CraftEntry entry)
{
    entry = null!;
    var key = Normalize(label);
    CraftEntry? exact = null;
    CraftEntry? fuzzy = null;

    foreach (var cat in profession.Categories)
    {
        foreach (var e in cat.Entries)
        {
            if (e.IsItemEntry) continue;
            var entryKey = Normalize(e.Label);
            if (entryKey == key)
            {
                exact = e;
                break;
            }

            if (entryKey.Contains(key, StringComparison.Ordinal) || key.Contains(entryKey, StringComparison.Ordinal))
                fuzzy ??= e;
        }
    }

    if (exact != null)
    {
        entry = exact;
        return true;
    }

    if (fuzzy != null)
    {
        entry = fuzzy;
        return true;
    }

    return false;
}

static string Normalize(string s) =>
    new string(s.Where(c => !char.IsWhiteSpace(c) && c != '\'' && c != '’').ToArray())
        .ToLowerInvariant();

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        var assets = Path.Combine(dir.FullName, "WindowsOrganiserApp", "Assets");
        if (File.Exists(Path.Combine(assets, "Craft.json"))
            && File.Exists(Path.Combine(assets, "CraftLevelingGuides.json")))
            return dir.FullName;
        dir = dir.Parent;
    }

    throw new InvalidOperationException("Racine du dépôt introuvable.");
}

sealed class GuidesFile
{
    public List<LevelingGuide> Guides { get; set; } = [];
}

sealed class LevelingGuide
{
    public string ProfessionId { get; set; } = "";
    public string ListName { get; set; } = "";
    public List<LevelingStep> Steps { get; set; } = [];
}

sealed class LevelingStep
{
    public string Label { get; set; } = "";
    public int Qty { get; set; }
}

sealed class CraftDatabase
{
    public List<CraftProfession> Professions { get; set; } = [];
}

sealed class CraftProfession
{
    public string Id { get; set; } = "";
    public List<CraftCategory> Categories { get; set; } = [];
}

sealed class CraftCategory
{
    public List<CraftEntry> Entries { get; set; } = [];
}

sealed class CraftEntry
{
    public bool IsItemEntry { get; set; }
    public int SpellId { get; set; }
    public int CreatedItemId { get; set; }
    public string Label { get; set; } = "";
}

sealed class CraftListsData
{
    public int Version { get; set; }
    public List<CraftListDefinition> Lists { get; set; } = [];
}

sealed class CraftListDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<CraftListItem> Items { get; set; } = [];
}

sealed class CraftListItem
{
    public int ItemId { get; set; }
    public int Quantity { get; set; }
    public string? ProfessionId { get; set; }
    public int SpellId { get; set; }
}
