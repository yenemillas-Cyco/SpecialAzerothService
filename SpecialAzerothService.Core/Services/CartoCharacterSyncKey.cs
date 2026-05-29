using System.Globalization;
using System.Text;

namespace SpecialAzerothService.Core.Services;

/// <summary>Clé personnage Nom-Royaume : correspondance insensible à la casse et aux accents (ex. Ptitagité).</summary>
public static class CartoCharacterSyncKey
{
    public static string Normalize(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "";

        var trimmed = key.Trim();
        var sb = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    public static bool Equals(string? a, string? b) =>
        Normalize(a) == Normalize(b);

    public static string FromNameRealm(string name, string realm) =>
        $"{name?.Trim()}-{realm?.Trim()}";
}
