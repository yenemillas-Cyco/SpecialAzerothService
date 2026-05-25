namespace WindowsOrganiserApp;

/// <summary>Carte unique Carto (monde + capitales sur une image).</summary>
public static class CartoMapAssets
{
    public const string FileName = "WowMap.png";

    public static Uri PackUri { get; } =
        new($"pack://application:,,,/Assets/{FileName}");
}
