using System.IO;
using SpecialAzerothService.Core.Models.Carto;
using WindowsOrganiserApp.Services;

var repoRoot = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

var capitalsDir = Path.Combine(repoRoot, "WindowsOrganiserApp", "Assets", "Capitals");
var outputPath = Path.Combine(capitalsDir, CapitalMapsCompositeLayout.CompositeAssetFileName);

if (!Directory.Exists(capitalsDir))
{
    Console.Error.WriteLine($"Dossier introuvable : {capitalsDir}");
    return 1;
}

CapitalMapsCompositeBuilder.SaveCompositeJpeg(outputPath, capitalsDir);
Console.WriteLine($"Image générée : {outputPath}");
Console.WriteLine(
    $"Taille {CapitalMapsCompositeLayout.PixelWidth}×{CapitalMapsCompositeLayout.PixelHeight} px — ne pas changer sans recalibrer les zones.");
return 0;
