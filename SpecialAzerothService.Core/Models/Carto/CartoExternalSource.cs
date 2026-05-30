namespace SpecialAzerothService.Core.Models.Carto;

/// <summary>Identifiants de provenance pour les personnages externes (carte / sync).</summary>
public static class CartoExternalSource
{
    public const string TpBoyPrefix = "tp:";

    public static string TpBoyPublic(string ownerGuid) => TpBoyPrefix + ownerGuid;

    public static bool IsTpBoyPublic(string? source) =>
        source != null && source.StartsWith(TpBoyPrefix, StringComparison.Ordinal);

    public static string? TpBoyOwnerGuid(string? source) =>
        IsTpBoyPublic(source) ? source![TpBoyPrefix.Length..] : null;

    public static bool IsNetworkFriend(string? source) =>
        !string.IsNullOrEmpty(source) && !IsTpBoyPublic(source);
}
