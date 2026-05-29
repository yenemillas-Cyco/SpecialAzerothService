namespace SpecialAzerothService.Core.Models.Craft;

public sealed class MaterialRequirement
{
    public int NetNeeded { get; init; }
    public int GrossNeeded { get; init; }
    public int ConsumedFromStock => Math.Max(0, GrossNeeded - NetNeeded);
}

public sealed class CraftDecompositionResult
{
    public required IReadOnlyDictionary<int, MaterialRequirement> Materials { get; init; }
}
