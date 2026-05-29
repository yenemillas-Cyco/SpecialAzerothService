using System.Text.Json;
using System.Text.RegularExpressions;
using CraftExtractor;

const string DefaultLua = @"D:\Programmes\World of Warcraft\_classic_era_\Interface\AddOns\AtlasLootClassic_Crafting\data.lua";
const string DefaultProfessionLua = @"D:\Programmes\World of Warcraft\_classic_era_\Interface\AddOns\AtlasLootClassic\Data\Profession.lua";

var luaPath = args.Length > 0 ? args[0] : DefaultLua;
var outPath = args.Length > 1
    ? args[1]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "WindowsOrganiserApp", "Assets", "Craft.json"));
var professionLuaPath = args.Length > 2 ? args[2] : DefaultProfessionLua;

if (!File.Exists(luaPath))
{
    Console.Error.WriteLine($"data.lua introuvable: {luaPath}");
    return 1;
}

var lua = await File.ReadAllTextAsync(luaPath);
var professions = AtlasLootCraftParser.Parse(lua);

if (File.Exists(professionLuaPath))
{
    var profLua = await File.ReadAllTextAsync(professionLuaPath);
    var spellData = ProfessionDataParser.ParseClassic(profLua);
    var itemSkills = ProfessionDataParser.ParseClassicItemSkills(profLua);
    AtlasLootCraftParser.EnrichWithProfessionData(professions, spellData);
    AtlasLootCraftParser.EnrichWithItemSkills(professions, itemSkills);
    Console.WriteLine($"  Enrichi : {spellData.Count} sorts, {itemSkills.Count} herbes/objets récolte");
}
else
{
    Console.WriteLine($"  AVERTISSEMENT: Profession.lua introuvable ({professionLuaPath})");
}
var payload = new CraftDatabaseDto
{
    Version = 3,
    Game = "classic-era",
    Source = "AtlasLootClassic_Crafting/data.lua",
    ExtractedFrom = luaPath,
    ContentTypes = ["Professions", "Gathering", "Secondary", "Class"],
    Professions = professions
};

Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
await File.WriteAllTextAsync(outPath, json);

var entries = professions.Sum(p => p.Categories.Sum(c => c.Entries.Count));
Console.WriteLine($"Écrit {outPath}");
Console.WriteLine($"  {professions.Count} métiers, {professions.Sum(p => p.Categories.Count)} catégories, {entries} entrées");
return 0;

file sealed class CraftDatabaseDto
{
    public int Version { get; set; }
    public string Game { get; set; } = "";
    public string Source { get; set; } = "";
    public string ExtractedFrom { get; set; } = "";
    public List<string> ContentTypes { get; set; } = [];
    public List<CraftProfessionDto> Professions { get; set; } = [];
}

file sealed class CraftProfessionDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string NameFr { get; set; } = "";
    public string ContentType { get; set; } = "";
    public List<CraftCategoryDto> Categories { get; set; } = [];
}

file sealed class CraftCategoryDto
{
    public string Name { get; set; } = "";
    public List<CraftEntryDto> Entries { get; set; } = [];
}

file sealed class CraftEntryDto
{
    public int Slot { get; set; }
    public bool IsItemEntry { get; set; }
    public int SpellId { get; set; }
    public int CreatedItemId { get; set; }
    public List<int> ItemIds { get; set; } = [];
    public string Label { get; set; } = "";
    public int SkillMin { get; set; }
    public int SkillLow { get; set; }
    public int SkillHigh { get; set; }
    public List<CraftReagentDto> Reagents { get; set; } = [];
}

file sealed class CraftReagentDto
{
    public int ItemId { get; set; }
    public int Count { get; set; }
}

file static class AtlasLootCraftParser
{
    private static readonly Dictionary<string, string> ContentTypeMap = new()
    {
        ["PROF_CONTENT"] = "Professions",
        ["PROF_GATH_CONTENT"] = "Gathering",
        ["PROF_SEC_CONTENT"] = "Secondary",
        ["PROF_CLASS_CONTENT"] = "Class",
    };

    private static readonly Dictionary<string, string> ProfFr = new()
    {
        ["Alchemy"] = "Alchimie",
        ["Blacksmithing"] = "Forge",
        ["Enchanting"] = "Enchantement",
        ["Engineering"] = "Ingénierie",
        ["Tailoring"] = "Couture",
        ["Leatherworking"] = "Travail du cuir",
        ["Mining"] = "Minage",
        ["Herbalism"] = "Herboristerie",
        ["Cooking"] = "Cuisine",
        ["FirstAid"] = "Secourisme",
        ["Fishing"] = "Pêche",
        ["RoguePoisons"] = "Poisons (voleur)",
    };

    private static readonly Regex DataBlockRe = new(@"data\[""([^""]+)""\]\s*=\s*\{", RegexOptions.Compiled);
    private static readonly Regex ContentTypeRe = new(@"ContentType\s*=\s*(\w+)", RegexOptions.Compiled);
    private static readonly Regex ProfNameRe = new(@"name\s*=\s*ALIL\[""([^""]+)""\]", RegexOptions.Compiled);
    private static readonly Regex CategoryNameLineRe = new(@"name\s*=", RegexOptions.Compiled);
    private static readonly Regex DiffBlockRe = new(@"\[(NORMAL|MAIL|PLATE|LEATHER)_DIFF\]\s*=\s*\{", RegexOptions.Compiled);
    private static readonly Regex QuotedStringRe = new(@"""(?:[^""\\]|\\.)*""", RegexOptions.Compiled);
    private static readonly Regex EntryRe = new(@"\{\s*(\d+)\s*,\s*([\d\s,]+)\s*\}\s*,?\s*(?:--\s*(.+))?", RegexOptions.Compiled);

    public static List<CraftProfessionDto> Parse(string lua)
    {
        var list = new List<CraftProfessionDto>();
        foreach (Match m in DataBlockRe.Matches(lua))
        {
            var key = m.Groups[1].Value;
            var start = m.Index + m.Length - 1;
            var end = FindMatchingBrace(lua, start);
            var body = lua[(start + 1)..end];
            list.Add(ParseProfession(key, body));
        }

        return list;
    }

    private static CraftProfessionDto ParseProfession(string key, string body)
    {
        var contentKey = ContentTypeRe.Match(body).Groups[1].Value;
        var contentType = ContentTypeMap.GetValueOrDefault(contentKey, contentKey);
        var name = ProfNameRe.Match(body).Groups[1].Value;
        if (string.IsNullOrEmpty(name)) name = key;

        var usesItemEntries = body.Contains("TableType = NORMAL_ITTYPE", StringComparison.Ordinal);

        return new CraftProfessionDto
        {
            Id = key,
            Name = name,
            NameFr = ProfFr.GetValueOrDefault(key, name),
            ContentType = contentType,
            Categories = ParseCategories(body, usesItemEntries)
        };
    }

    private static List<CraftCategoryDto> ParseCategories(string body, bool entriesAreItems)
    {
        var categories = new List<CraftCategoryDto>();
        var itemsIdx = body.IndexOf("items = {", StringComparison.Ordinal);
        if (itemsIdx < 0) return categories;

        var itemsStart = body.IndexOf('{', itemsIdx);
        var itemsEnd = FindMatchingBrace(body, itemsStart);
        var itemsBody = body[(itemsStart + 1)..itemsEnd];

        var nameMatches = CategoryNameLineRe.Matches(itemsBody).Cast<Match>().ToList();
        for (var i = 0; i < nameMatches.Count; i++)
        {
            var nameIdx = nameMatches[i].Index;
            var sliceEnd = i + 1 < nameMatches.Count ? nameMatches[i + 1].Index : itemsBody.Length;
            var catSlice = itemsBody[nameIdx..sliceEnd];
            var catName = ExtractCategoryName(catSlice);
            if (string.IsNullOrWhiteSpace(catName)) continue;

            var entries = new List<CraftEntryDto>();
            foreach (Match diffMatch in DiffBlockRe.Matches(catSlice))
            {
                var diffStart = catSlice.IndexOf('{', diffMatch.Index);
                var diffEnd = FindMatchingBrace(catSlice, diffStart);
                entries.AddRange(ParseEntries(catSlice[(diffStart + 1)..diffEnd], entriesAreItems));
            }

            if (entries.Count > 0)
                categories.Add(new CraftCategoryDto { Name = catName, Entries = entries });
        }

        return categories;
    }

    private static string ExtractCategoryName(string categorySlice)
    {
        var lineEnd = categorySlice.IndexOf('\n');
        if (lineEnd < 0) lineEnd = categorySlice.Length;
        var nameLine = categorySlice[..lineEnd];

        var parts = new List<string>();
        foreach (Match m in QuotedStringRe.Matches(nameLine))
        {
            var raw = m.Value.Trim('"').Trim();
            if (raw.StartsWith("INV_", StringComparison.OrdinalIgnoreCase)) continue;
            if (raw is "-" or " - " or "") continue;
            parts.Add(raw);
        }

        return parts.Count == 0 ? "" : string.Join(" - ", parts);
    }

    private static List<CraftEntryDto> ParseEntries(string diffBody, bool entriesAreItems)
    {
        var entries = new List<CraftEntryDto>();
        foreach (Match m in EntryRe.Matches(diffBody))
        {
            var slot = int.Parse(m.Groups[1].Value);
            var ids = m.Groups[2].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(int.Parse)
                .ToList();

            var label = m.Groups[3].Success ? m.Groups[3].Value.Trim() : "";
            if (label.Contains('/'))
                label = label.Split('/')[0].Trim();

            var spellId = 0;
            var itemIds = new List<int>();
            if (entriesAreItems)
                itemIds = ids;
            else if (ids.Count == 1)
                spellId = ids[0];
            else
                itemIds = ids;

            entries.Add(new CraftEntryDto
            {
                Slot = slot,
                IsItemEntry = entriesAreItems,
                SpellId = spellId,
                ItemIds = itemIds,
                Label = label
            });
        }

        return entries;
    }

    private static int FindMatchingBrace(string text, int openIndex)
    {
        var depth = 0;
        for (var i = openIndex; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '{')
            {
                depth++;
            }
            else if (ch == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
            else if (ch is '"' or '\'')
            {
                var quote = ch;
                i++;
                while (i < text.Length && text[i] != quote)
                {
                    if (text[i] == '\\') i++;
                    i++;
                }
            }
            else if (ch == '-' && i + 1 < text.Length && text[i + 1] == '-')
            {
                while (i < text.Length && text[i] is not '\r' and not '\n')
                    i++;
            }
        }

        throw new InvalidOperationException("Accolades non équilibrées dans data.lua");
    }

    public static void EnrichWithProfessionData(
        List<CraftProfessionDto> professions,
        Dictionary<int, ProfessionSpellData> spellData)
    {
        foreach (var entry in professions.SelectMany(p => p.Categories).SelectMany(c => c.Entries))
        {
            if (entry.IsItemEntry || entry.SpellId <= 0 || !spellData.TryGetValue(entry.SpellId, out var data))
                continue;

            entry.CreatedItemId = data.CreatedItemId;
            entry.SkillMin = data.SkillMin;
            entry.SkillLow = data.SkillLow;
            entry.SkillHigh = data.SkillHigh;
            entry.Reagents = new List<CraftReagentDto>();
            for (var i = 0; i < data.ReagentIds.Count; i++)
            {
                var count = i < data.ReagentCounts.Count ? data.ReagentCounts[i] : 1;
                entry.Reagents.Add(new CraftReagentDto { ItemId = data.ReagentIds[i], Count = count });
            }
        }
    }

    public static void EnrichWithItemSkills(
        List<CraftProfessionDto> professions,
        Dictionary<int, GatherItemSkill> itemSkills)
    {
        foreach (var entry in professions.SelectMany(p => p.Categories).SelectMany(c => c.Entries))
        {
            if (!entry.IsItemEntry || entry.ItemIds.Count == 0) continue;
            var primaryId = entry.ItemIds[0];
            if (!itemSkills.TryGetValue(primaryId, out var skill)) continue;

            entry.SkillLow = skill.SkillLow;
            entry.SkillHigh = skill.SkillHigh;
            entry.SkillMin = skill.SkillMid;
        }
    }
}
