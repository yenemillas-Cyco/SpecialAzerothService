namespace SpecialAzerothService.Core.Services;

/// <summary>Progression du chargement au démarrage (splash).</summary>
public sealed record StartupLoadProgress(double Percent, string Message);
