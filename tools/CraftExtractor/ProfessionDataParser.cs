using System.Text.RegularExpressions;

namespace CraftExtractor;

internal sealed record GatherItemSkill(int SkillLow, int SkillMid, int SkillHigh);

internal sealed record ProfessionSpellData(
    int CreatedItemId,
    int SkillMin,
    int SkillLow,
    int SkillHigh,
    List<int> ReagentIds,
    List<int> ReagentCounts);

internal static class ProfessionDataParser
{
    private static readonly Regex LineRe = new(
        @"^\s*\[(\d+)\]\s*=\s*\{(.*)\},?\s*$",
        RegexOptions.Compiled);

    public static Dictionary<int, ProfessionSpellData> ParseClassic(string professionLua)
    {
        var start = professionLua.IndexOf("PROFESSION_DATA.CLASSIC = {", StringComparison.Ordinal);
        if (start < 0) return new Dictionary<int, ProfessionSpellData>();

        start = professionLua.IndexOf('{', start);
        var end = FindMatchingBrace(professionLua, start);
        var body = professionLua[(start + 1)..end];

        var map = new Dictionary<int, ProfessionSpellData>();
        foreach (var line in body.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith('[')) continue;

            var m = LineRe.Match(trimmed);
            if (!m.Success) continue;

            var spellId = int.Parse(m.Groups[1].Value);
            if (!TryParseRow(m.Groups[2].Value, out var data)) continue;
            map[spellId] = data;
        }

        return map;
    }

    public static Dictionary<int, GatherItemSkill> ParseClassicItemSkills(string professionLua)
    {
        var start = professionLua.IndexOf("PROFESSION_ITEM_SKILL_DATA.CLASSIC = {", StringComparison.Ordinal);
        if (start < 0) return new Dictionary<int, GatherItemSkill>();

        start = professionLua.IndexOf('{', start);
        var end = FindMatchingBrace(professionLua, start);
        var body = professionLua[(start + 1)..end];

        var map = new Dictionary<int, GatherItemSkill>();
        var lineRe = new Regex(@"^\s*\[(\d+)\]\s*=\s*\{\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\},?\s*(?:--.*)?$", RegexOptions.Compiled);
        foreach (var line in body.Split('\n'))
        {
            var m = lineRe.Match(line.Trim());
            if (!m.Success) continue;
            map[int.Parse(m.Groups[1].Value)] = new GatherItemSkill(
                int.Parse(m.Groups[2].Value),
                int.Parse(m.Groups[3].Value),
                int.Parse(m.Groups[4].Value));
        }

        return map;
    }

    private static bool TryParseRow(string inner, out ProfessionSpellData data)
    {
        data = null!;
        var parts = SplitTopLevel(inner);
        if (parts.Count < 7) return false;

        var created = parts[0] == "nil" ? 0 : int.Parse(parts[0]);
        var skillMin = int.Parse(parts[2]);
        var skillLow = int.Parse(parts[3]);
        var skillHigh = int.Parse(parts[4]);
        var reagentIds = ParseIntList(parts[5]);
        var reagentCounts = ParseIntList(parts[6]);

        data = new ProfessionSpellData(created, skillMin, skillLow, skillHigh, reagentIds, reagentCounts);
        return true;
    }

    private static List<int> ParseIntList(string braceList)
    {
        var inner = braceList.Trim();
        if (!inner.StartsWith('{') || !inner.EndsWith('}')) return [];
        inner = inner[1..^1].Trim();
        if (inner.Length == 0) return [];
        return inner.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToList();
    }

    private static List<string> SplitTopLevel(string s)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (ch == '{') depth++;
            else if (ch == '}') depth--;
            else if (ch == ',' && depth == 0)
            {
                parts.Add(s[start..i].Trim());
                start = i + 1;
            }
        }

        parts.Add(s[start..].Trim());
        return parts;
    }

    private static int FindMatchingBrace(string text, int openIndex)
    {
        var depth = 0;
        for (var i = openIndex; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
        }

        throw new InvalidOperationException("Accolades non équilibrées dans Profession.lua");
    }
}
