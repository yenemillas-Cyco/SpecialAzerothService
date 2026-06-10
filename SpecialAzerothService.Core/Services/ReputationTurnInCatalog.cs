using SpecialAzerothService.Core.Models.Reputation;

namespace SpecialAzerothService.Core.Services;

/// <summary>Réputations augmentables via objets (Classic Era).</summary>
public static partial class ReputationTurnInCatalog
{
  public const int ZandalarHonorTokenItemId = 19858;

  // Classic Era 1.13 — bijoux hakkari (IDs 19707–19713, pas 19937–19943 qui sont des butins ZG)
  private static readonly ReputationTurnInItem[] ZandalarBijoux =
  [
      new() { ItemId = 19713, NameFr = "Bijou hakkari bronze" },
      new() { ItemId = 19707, NameFr = "Bijou hakkari rouge" },
      new() { ItemId = 19709, NameFr = "Bijou hakkari jaune" },
      new() { ItemId = 19710, NameFr = "Bijou hakkari orange" },
      new() { ItemId = 19711, NameFr = "Bijou hakkari vert" },
      new() { ItemId = 19712, NameFr = "Bijou hakkari violet" },
      new() { ItemId = 19708, NameFr = "Bijou hakkari bleu" },
  ];

  // Classic Era 1.13 — pièces en forme de serpent (IDs 19698–19706)
  private static readonly ReputationTurnInItem[] ZandalarCoins =
  [
      new() { ItemId = 19698, NameFr = "Pièce zulienne" },
      new() { ItemId = 19699, NameFr = "Pièce razzashi" },
      new() { ItemId = 19700, NameFr = "Pièce hakkari" },
      new() { ItemId = 19701, NameFr = "Pièce gurubashi" },
      new() { ItemId = 19702, NameFr = "Pièce vilebranche" },
      new() { ItemId = 19703, NameFr = "Pièce fanécorce" },
      new() { ItemId = 19704, NameFr = "Pièce sable-furie" },
      new() { ItemId = 19705, NameFr = "Pièce casse-crâne" },
      new() { ItemId = 19706, NameFr = "Pièce scalp-rouge" },
  ];

  public static readonly ReputationFarmDefinition ZandalarTribe = new()
  {
      Id = "ZandalarTribe",
      FactionNameFr = "Tribu zandalar",
      LocationFr = "Île de Yojamba (Vallée de Strangleronce)",
      NpcNameFr = "Vinchaxa",
      NotesFr =
          "Bijou détruit : +75 + jeton d'honneur (+50) = 125 rép. "
          + "3 pièces : +25 + jeton (+50) = 75 rép. "
          + "Un bijou équivaut à cinq pièces en réputation.",
      Routes =
      [
          new ReputationTurnInRoute
          {
              RouteId = "Bijoux",
              Method = ReputationTurnInMethod.Bijoux,
              LabelFr = "Bijoux hakkari",
              DescriptionFr = "Détruire un bijou (n'importe quelle couleur) auprès de Vinchaxa.",
              BaseReputation = 75,
              HonorTokenReputation = 50,
              ItemsPerTurnIn = 1,
              HonorTokenItemId = ZandalarHonorTokenItemId,
              HonorTokenNameFr = "Jeton d'honneur zandalar",
              ItemUnitLabelFr = "bijoux",
              AcceptedItems = ZandalarBijoux,
          },
          new ReputationTurnInRoute
          {
              RouteId = "Coins",
              Method = ReputationTurnInMethod.Coins,
              LabelFr = "Pièces de tribu",
              DescriptionFr = "Échanger 3 pièces (toutes tribus confondues) contre de la réputation.",
              BaseReputation = 25,
              HonorTokenReputation = 50,
              ItemsPerTurnIn = 3,
              HonorTokenItemId = ZandalarHonorTokenItemId,
              HonorTokenNameFr = "Jeton d'honneur zandalar",
              ItemUnitLabelFr = "pièces",
              AcceptedItems = ZandalarCoins,
          },
      ],
  };

  // Lazy : l'ordre d'init des champs static entre fichiers partial n'est pas garanti.
  private static readonly Lazy<IReadOnlyList<ReputationFarmDefinition>> AllLazy = new(() =>
      (IReadOnlyList<ReputationFarmDefinition>)
      [
          ArgentDawn,
          CenarionCircle,
          ThoriumBrotherhood,
          DarkmoonFaire,
          BroodOfNozdormu,
          ZandalarTribe,
      ]);

  public static IReadOnlyList<ReputationFarmDefinition> All => AllLazy.Value;

  public static ReputationFarmDefinition? TryGetById(string id) =>
      All.FirstOrDefault(f => f.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

  public static ReputationTurnInRoute? TryGetRoute(ReputationFarmDefinition farm, ReputationTurnInMethod method) =>
      farm.Routes.FirstOrDefault(r => r.Method == method);

  public static ReputationTurnInRoute? TryGetRouteById(ReputationFarmDefinition farm, string routeId) =>
      farm.Routes.FirstOrDefault(r => r.RouteId.Equals(routeId, StringComparison.OrdinalIgnoreCase));
}
