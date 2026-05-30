namespace SpecialAzerothService.Core.Models.Carto;

/// <summary>Regroupements de CD métiers (ex. transmutations alchimie partagent un seul CD en jeu).</summary>
public static class CooldownGroups
{
  public static readonly CooldownType CanonicalAlchemyTransmute = CooldownType.Arcanite;

  public static bool IsAlchemyTransmute(CooldownType type) => type switch
  {
    CooldownType.Arcanite or CooldownType.Transmute_Elementaire or CooldownType.Transmutation
      or CooldownType.Air_to_Fire or CooldownType.Fire_to_Earth or CooldownType.Earth_to_Water
      or CooldownType.Water_to_Air or CooldownType.Undeath_to_Water or CooldownType.Water_to_Undeath
      or CooldownType.Life_to_Earth or CooldownType.Earth_to_Life => true,
    _ => false
  };

  public static bool IsAlchemySyncKey(string key)
  {
    var k = key.Trim().ToLowerInvariant();
    return k is "arcanite" or "elemental" or "element" or "alchemy" or "alchimie";
  }

  /// <summary>Clé/nom renvoyés par l'addon WowSync.</summary>
  public static CooldownType? MapSyncCooldownKey(string key, string? syncName = null)
  {
    if (MapAlchemySyncKey(key, syncName) is { } alchemy)
      return alchemy;

    var k = key.Trim().ToLowerInvariant();
    var label = syncName?.Trim().ToLowerInvariant() ?? "";

    if (k.Contains("lunaire") || k.Contains("mooncloth") || k == "moon"
        || label.Contains("lunaire") || label.Contains("mooncloth"))
      return CooldownType.Mooncloth;

    if (k.Contains("salt") || k.Contains("sel") || k.Contains("raffine")
        || label.Contains("sel") || label.Contains("salt"))
      return CooldownType.Sel_raffine;

    return null;
  }

  public static CooldownType? MapAlchemySyncKey(string key, string? syncName = null)
  {
    var k = key.Trim().ToLowerInvariant();
    return k switch
    {
      "arcanite" => CooldownType.Arcanite,
      "elemental" or "element" => CooldownType.Transmute_Elementaire,
      "alchemy" or "alchimie" => InferAlchemyTypeFromLabel(syncName),
      _ => IsAlchemySyncKey(key) ? InferAlchemyTypeFromLabel(syncName) : null
    };
  }

  public static CooldownType InferAlchemyTypeFromLabel(string? label)
  {
    if (string.IsNullOrWhiteSpace(label))
      return CanonicalAlchemyTransmute;

    var n = label.Trim().ToLowerInvariant();
    if (n.Contains("arcanite"))
      return CooldownType.Arcanite;

    if (n.Contains("element") || n.Contains("élément") || n.Contains("elementaire") || n.Contains("élémentaire"))
      return CooldownType.Transmute_Elementaire;

    if (n.Contains("feu") && n.Contains("air"))
      return CooldownType.Air_to_Fire;
    if (n.Contains("terre") && n.Contains("feu"))
      return CooldownType.Fire_to_Earth;
    if (n.Contains("eau") && n.Contains("terre"))
      return CooldownType.Earth_to_Water;
    if (n.Contains("air") && n.Contains("eau"))
      return CooldownType.Water_to_Air;

    return CooldownType.Transmute_Elementaire;
  }

  /// <summary>Ne garde qu'un CD alchimie (fin de CD la plus tardive), en conservant le type réel.</summary>
  public static void NormalizeAlchemyCooldowns(IList<CooldownEntry> cooldowns)
  {
    var alchemy = cooldowns.Where(c => IsAlchemyTransmute(c.Type)).ToList();
    if (alchemy.Count <= 1)
      return;

    var bestReady = alchemy
      .Select(c => c.EffectiveReadyAt)
      .Where(r => r.HasValue)
      .Max();

    var keeper = alchemy
        .Where(c => !c.IsReady)
        .OrderByDescending(c => c.EffectiveReadyAt ?? DateTime.MinValue)
        .FirstOrDefault()
      ?? alchemy
        .OrderByDescending(c => c.LastUsed ?? DateTime.MinValue)
        .First();

    foreach (var c in alchemy)
      cooldowns.Remove(c);

    if (bestReady is { } ready && ready > DateTime.Now)
    {
      keeper.ReadyAtOverride = ready;
      if (keeper.LastUsed == null)
        keeper.LastUsed = ready - keeper.EffectiveDuration;
    }
    else
      keeper.ReadyAtOverride = null;

    if (!cooldowns.Contains(keeper))
      cooldowns.Add(keeper);
  }
}
