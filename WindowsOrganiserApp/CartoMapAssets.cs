namespace WindowsOrganiserApp;

/// <summary>Image Carto : Azeroth + bande capitales (seule la partie Azeroth est affichée si le dock capitales est actif).</summary>
public static class CartoMapAssets
{
    public const string FileName = "WowMap.png";

    public static Uri PackUri { get; } =
        new($"pack://application:,,,/Assets/{FileName}");
}
