using System.Security.Cryptography;
using System.Text;
using SpecialAzerothService.Core.Models.Carto;

namespace SpecialAzerothService.Core.Services;

public static class CartoSyncRevision
{
    public static long ComputeFriendRevision(
        IReadOnlyList<WowAccount> accounts,
        IReadOnlyList<WowCharacter> localCharacters)
    {
        var sb = new StringBuilder(4096);
        foreach (var a in accounts.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            sb.Append(a.Id).Append('|').Append(a.Name).Append('|').Append(a.SourceFolder).Append(';');
        }

        foreach (var ch in localCharacters
                     .Where(c => !c.IsExternal && !c.ExcludeFromSync)
                     .OrderBy(c => c.SyncKey, StringComparer.OrdinalIgnoreCase))
        {
            AppendCharacter(sb, ch);
            sb.Append("|prof:").Append((int)ch.Status);
            sb.Append("|note:").Append(ch.Note);
        }

        return HashToRevision(sb.ToString());
    }

    public static long ComputeTpBoyRevision(IReadOnlyList<TpBoyPublicEntry> entries)
    {
        var sb = new StringBuilder(2048);
        foreach (var e in entries.OrderBy(x => x.SyncKey, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append(e.SyncKey).Append('|')
                .Append(e.MapX.ToString("F4")).Append('|')
                .Append(e.MapY.ToString("F4")).Append('|')
                .Append(e.ShardCount).Append('|')
                .Append(e.LastUpdate).Append('|')
                .Append(e.TerrainZoneSlug).Append('|')
                .Append(e.TerrainZoneX).Append('|')
                .Append(e.TerrainZoneY).Append(';');
        }

        return HashToRevision(sb.ToString());
    }

    private static void AppendCharacter(StringBuilder sb, WowCharacter ch)
    {
        sb.Append(ch.SyncKey).Append('|')
            .Append(ch.MapX.ToString("F4")).Append('|')
            .Append(ch.MapY.ToString("F4")).Append('|')
            .Append(ch.ShardCount).Append('|')
            .Append(ch.IsPlacedOnMap).Append(';');
    }

    private static long HashToRevision(string canonical)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return BitConverter.ToInt64(hash, 0);
    }
}
