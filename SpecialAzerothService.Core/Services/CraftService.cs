using System.IO;
using System.Reflection;
using System.Text.Json;
using SpecialAzerothService.Core.Models.Craft;

namespace SpecialAzerothService.Core.Services;

public sealed class CraftService : ICraftService
{
    public CraftDatabase Database { get; }

    public CraftService()
    {
        Database = LoadDatabase();
    }

    public IReadOnlyList<CraftProfession> GetProfessions(string? contentTypeFilter = null)
    {
        var list = Database.Professions.AsEnumerable()
            .Where(p => !string.Equals(p.ContentType, "Gathering", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(contentTypeFilter))
            list = list.Where(p => p.ContentType == contentTypeFilter);
        return list.OrderBy(p => p.NameFr).ToList();
    }

    private static CraftDatabase LoadDatabase()
    {
        var asm = Assembly.GetExecutingAssembly();
        const string resourceName = "SpecialAzerothService.Core.Assets.Craft.json";
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new InvalidOperationException($"Ressource embarquée introuvable : {resourceName}");

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return JsonSerializer.Deserialize<CraftDatabase>(json, JsonOptions)
               ?? throw new InvalidOperationException("Craft.json invalide ou vide.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
