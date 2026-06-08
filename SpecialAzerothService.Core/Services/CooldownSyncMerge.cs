using SpecialAzerothService.Core.Models.Carto;
using SpecialAzerothService.Core.Models.WowSync;

namespace SpecialAzerothService.Core.Services;

/// <summary>Fusion des CD WowSync → modèle Carto (ne pas écraser un timer en cours absent du scan).</summary>
public static class CooldownSyncMerge
{
    public static void ApplySyncCooldowns(WowCharacterData sync, WowCharacter carto)
    {
        foreach (var cd in sync.Cooldowns)
        {
            var type = CooldownGroups.MapSyncCooldownKey(cd.Key, cd.Name);
            if (type == null)
                continue;

            var entry = FindOrCreateEntry(carto, type.Value);
            ApplySyncEntry(entry, cd, type.Value);
        }

        CooldownGroups.NormalizeAlchemyCooldowns(carto.Cooldowns);
    }

    public static void ApplySyncEntry(CooldownEntry entry, WowProfessionCooldown sync, CooldownType type)
    {
        if (sync.IsExplicitlyReady)
        {
            entry.ReadyAtOverride = null;
            if (sync.ReadyAtUtc is { } readyAt)
                entry.LastUsed = readyAt - entry.Duration;
            return;
        }

        if (!sync.IsExplicitlyRunning)
            return;

        if (sync.ReadyAtUtc is not { } runningReadyAt)
            return;

        entry.ReadyAtOverride = runningReadyAt;
        var remaining = runningReadyAt - DateTime.UtcNow;
        if (remaining > TimeSpan.Zero)
        {
            var total = entry.Duration;
            if (type == CooldownType.Arcanite && remaining > TimeSpan.FromHours(25))
                total = TimeSpan.FromHours(48);
            else if (remaining > total)
                total = remaining;

            entry.LastUsed = runningReadyAt - total;
        }
        else if (entry.LastUsed == null)
        {
            entry.LastUsed = runningReadyAt - entry.Duration;
        }
    }

    public static CooldownEntry FindOrCreateEntry(WowCharacter carto, CooldownType type)
    {
        var entry = carto.Cooldowns.FirstOrDefault(c => c.Type == type)
                    ?? (CooldownGroups.IsAlchemyTransmute(type)
                        ? carto.Cooldowns.FirstOrDefault(c => CooldownGroups.IsAlchemyTransmute(c.Type))
                        : null);

        if (entry == null)
        {
            entry = new CooldownEntry { Type = type };
            carto.Cooldowns.Add(entry);
        }
        else if (CooldownGroups.IsAlchemyTransmute(type))
        {
            entry.Type = type;
        }

        return entry;
    }
}
