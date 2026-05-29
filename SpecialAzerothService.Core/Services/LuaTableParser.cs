using System.Globalization;
using System.IO;
using System.Text;

namespace SpecialAzerothService.Core.Services;

/// <summary>
/// Parses WoW SavedVariables .lua files into nested dictionaries.
/// Handles: strings, numbers, booleans, nil, nested tables, comments.
/// </summary>
public static class LuaTableParser
{
    public static Dictionary<string, object?> ParseFile(string filePath)
    {
        var text = File.ReadAllText(filePath, Encoding.UTF8);
        return ParseAssignments(text);
    }

    private static Dictionary<string, object?> ParseAssignments(string text)
    {
        var result = new Dictionary<string, object?>();
        var pos = 0;
        SkipWhitespaceAndComments(text, ref pos);

        while (pos < text.Length)
        {
            SkipWhitespaceAndComments(text, ref pos);
            if (pos >= text.Length) break;

            var varName = ReadIdentifier(text, ref pos);
            if (string.IsNullOrEmpty(varName)) break;

            SkipWhitespaceAndComments(text, ref pos);
            if (pos >= text.Length || text[pos] != '=') break;
            pos++;
            SkipWhitespaceAndComments(text, ref pos);

            var value = ReadValue(text, ref pos);
            result[varName] = value;

            SkipWhitespaceAndComments(text, ref pos);
        }

        return result;
    }

    private static object? ReadValue(string text, ref int pos)
    {
        SkipWhitespaceAndComments(text, ref pos);
        if (pos >= text.Length) return null;

        var c = text[pos];

        if (c == '{') return ReadTable(text, ref pos);
        if (c == '"') return ReadString(text, ref pos);
        if (c == '-' || char.IsDigit(c)) return ReadNumber(text, ref pos);
        if (text.Length - pos >= 4 && text.Substring(pos, 4) == "true") { pos += 4; return true; }
        if (text.Length - pos >= 5 && text.Substring(pos, 5) == "false") { pos += 5; return false; }
        if (text.Length - pos >= 3 && text.Substring(pos, 3) == "nil") { pos += 3; return null; }

        return null;
    }

    private static Dictionary<string, object?> ReadTable(string text, ref int pos)
    {
        pos++; // skip {
        var table = new Dictionary<string, object?>();
        var arrayIndex = 1;

        while (pos < text.Length)
        {
            SkipWhitespaceAndComments(text, ref pos);
            if (pos >= text.Length) break;
            if (text[pos] == '}') { pos++; break; }

            string key;

            if (text[pos] == '[')
            {
                pos++; // skip [
                SkipWhitespaceAndComments(text, ref pos);
                if (pos < text.Length && text[pos] == '"')
                {
                    key = ReadString(text, ref pos);
                }
                else
                {
                    key = ReadNumberRaw(text, ref pos);
                }
                SkipWhitespaceAndComments(text, ref pos);
                if (pos < text.Length && text[pos] == ']') pos++;
                SkipWhitespaceAndComments(text, ref pos);
                if (pos < text.Length && text[pos] == '=') pos++;
            }
            else if (char.IsLetter(text[pos]) || text[pos] == '_')
            {
                var savedPos = pos;
                var ident = ReadIdentifier(text, ref pos);
                SkipWhitespaceAndComments(text, ref pos);
                if (pos < text.Length && text[pos] == '=')
                {
                    key = ident;
                    pos++; // skip =
                }
                else
                {
                    pos = savedPos;
                    key = arrayIndex.ToString();
                    arrayIndex++;
                }
            }
            else
            {
                key = arrayIndex.ToString();
                arrayIndex++;
            }

            SkipWhitespaceAndComments(text, ref pos);
            var value = ReadValue(text, ref pos);
            table[key] = value;

            SkipWhitespaceAndComments(text, ref pos);
            if (pos < text.Length && text[pos] == ',') pos++;
        }

        return table;
    }

    private static string ReadString(string text, ref int pos)
    {
        pos++; // skip opening "
        var sb = new StringBuilder();
        while (pos < text.Length && text[pos] != '"')
        {
            if (text[pos] == '\\' && pos + 1 < text.Length)
            {
                pos++;
                sb.Append(text[pos] switch
                {
                    'n' => '\n',
                    't' => '\t',
                    '\\' => '\\',
                    '"' => '"',
                    _ => text[pos]
                });
            }
            else
            {
                sb.Append(text[pos]);
            }
            pos++;
        }
        if (pos < text.Length) pos++; // skip closing "
        return sb.ToString();
    }

    private static double ReadNumber(string text, ref int pos)
    {
        var raw = ReadNumberRaw(text, ref pos);
        return double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    private static string ReadNumberRaw(string text, ref int pos)
    {
        var start = pos;
        if (pos < text.Length && text[pos] == '-') pos++;
        while (pos < text.Length && (char.IsDigit(text[pos]) || text[pos] == '.'))
            pos++;
        return text[start..pos];
    }

    private static string ReadIdentifier(string text, ref int pos)
    {
        var start = pos;
        while (pos < text.Length && (char.IsLetterOrDigit(text[pos]) || text[pos] == '_'))
            pos++;
        return text[start..pos];
    }

    private static void SkipWhitespaceAndComments(string text, ref int pos)
    {
        while (pos < text.Length)
        {
            if (char.IsWhiteSpace(text[pos]))
            {
                pos++;
            }
            else if (pos + 1 < text.Length && text[pos] == '-' && text[pos + 1] == '-')
            {
                while (pos < text.Length && text[pos] != '\n') pos++;
            }
            else break;
        }
    }

    // Helper to get a string from parsed data
    public static string GetString(Dictionary<string, object?> dict, string key, string fallback = "")
        => dict.TryGetValue(key, out var v) && v is string s ? s : fallback;

    public static int GetInt(Dictionary<string, object?> dict, string key, int fallback = 0)
    {
        if (!dict.TryGetValue(key, out var v) || v is null) return fallback;
        return v switch
        {
            double d => (int)d,
            long l => (int)l,
            int i => i,
            _ => fallback
        };
    }

    public static long GetLong(Dictionary<string, object?> dict, string key, long fallback = 0)
    {
        if (!dict.TryGetValue(key, out var v) || v is null) return fallback;
        return v switch
        {
            double d => (long)d,
            long l => l,
            int i => i,
            _ => fallback
        };
    }

    public static double GetDouble(Dictionary<string, object?> dict, string key, double fallback = 0)
    {
        if (!dict.TryGetValue(key, out var v) || v is null) return fallback;
        return v switch
        {
            double d => d,
            long l => l,
            int i => i,
            _ => fallback
        };
    }

    public static Dictionary<string, object?>? GetTable(Dictionary<string, object?> dict, string key)
        => dict.TryGetValue(key, out var v) && v is Dictionary<string, object?> t ? t : null;
}
