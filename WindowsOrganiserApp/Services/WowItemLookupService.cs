using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsOrganiserApp.Models.WowSync;

namespace WindowsOrganiserApp.Services;

public interface IWowItemLookupService
{
    Task<ImageSource?> GetIconAsync(WowItem item, CancellationToken cancellationToken = default);
    Task<WowheadItemDetails?> GetDetailsAsync(WowItem item, CancellationToken cancellationToken = default);
}

public sealed class WowItemLookupService : IWowItemLookupService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private readonly ConcurrentDictionary<int, Task<WowheadItemDetails?>> _detailsCache = new();
    private readonly ConcurrentDictionary<string, Task<ImageSource?>> _imageCache = new();

    static WowItemLookupService()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) SpecialAzerothService/1.0");
    }

    public async Task<ImageSource?> GetIconAsync(WowItem item, CancellationToken cancellationToken = default)
    {
        if (item.ItemId <= 0) return null;

        var key = $"item:{item.ItemId}";
        return await _imageCache.GetOrAdd(key, _ => LoadIconAsync(item.ItemId, cancellationToken)).ConfigureAwait(false);
    }

    public Task<WowheadItemDetails?> GetDetailsAsync(WowItem item, CancellationToken cancellationToken = default)
    {
        if (item.ItemId <= 0) return Task.FromResult<WowheadItemDetails?>(null);
        return _detailsCache.GetOrAdd(item.ItemId, _ => FetchDetailsAsync(item.ItemId, cancellationToken));
    }

    private async Task<ImageSource?> LoadIconAsync(int itemId, CancellationToken cancellationToken)
    {
        var details = await _detailsCache.GetOrAdd(itemId, id => FetchDetailsAsync(id, cancellationToken))
            .ConfigureAwait(false);
        if (details == null || string.IsNullOrEmpty(details.IconSlug)) return null;

        try
        {
            var url = $"https://wow.zamimg.com/images/wow/icons/large/{details.IconSlug}.jpg";
            var bytes = await Http.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
            if (bytes.Length == 0) return null;
            return CreateBitmap(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? CreateBitmap(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private async Task<WowheadItemDetails?> FetchDetailsAsync(int itemId, CancellationToken cancellationToken)
    {
        foreach (var apiUrl in new[]
        {
            $"https://nether.wowhead.com/classic/fr/tooltip/item/{itemId}?data=3",
            $"https://nether.wowhead.com/classic-era/fr/tooltip/item/{itemId}?data=3",
            $"https://nether.wowhead.com/classic/tooltip/item/{itemId}?data=3&locale=7"
        })
        {
            try
            {
                using var response = await Http.GetAsync(apiUrl, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) continue;

                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var details = ParseWowheadJson(json);
                if (details != null) return details;
            }
            catch
            {
                // try next endpoint
            }
        }

        return null;
    }

    internal static WowheadItemDetails? ParseWowheadJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("name", out var nameProp)) return null;

            var name = nameProp.GetString() ?? "";
            var quality = root.TryGetProperty("quality", out var qProp) ? qProp.GetInt32() : 0;
            var icon = root.TryGetProperty("icon", out var iconProp) ? iconProp.GetString() ?? "" : "";
            var tooltipHtml = root.TryGetProperty("tooltip", out var tipProp) ? tipProp.GetString() ?? "" : "";

            var itemLevel = ParseItemLevel(tooltipHtml);
            var maxStack = ParseMaxStack(tooltipHtml);
            var (gold, silver, copper) = ParseSellPrice(tooltipHtml);
            var extraLines = ParseExtraLines(tooltipHtml);

            return new WowheadItemDetails
            {
                Name = name,
                Quality = quality,
                IconSlug = icon,
                ItemLevel = itemLevel,
                MaxStack = maxStack,
                SellGold = gold,
                SellSilver = silver,
                SellCopper = copper,
                ExtraLines = extraLines
            };
        }
        catch
        {
            return null;
        }
    }

    private static int? ParseItemLevel(string html)
    {
        var m = Regex.Match(html, @"<!--ilvl-->(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : null;
    }

    private static int? ParseMaxStack(string html)
    {
        var m = Regex.Match(html, @"Empilement maxi:\s*(\d+)", RegexOptions.IgnoreCase);
        if (m.Success) return int.Parse(m.Groups[1].Value);

        m = Regex.Match(html, @"whtt-maxstack[^>]*>([^<]+)", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var digits = Regex.Match(m.Groups[1].Value, @"(\d+)");
            if (digits.Success) return int.Parse(digits.Groups[1].Value);
        }

        return null;
    }

    private static (int Gold, int Silver, int Copper) ParseSellPrice(string html)
    {
        int gold = 0, silver = 0, copper = 0;
        var m = Regex.Match(html, @"class=""moneygold"">(\d+)");
        if (m.Success) gold = int.Parse(m.Groups[1].Value);
        m = Regex.Match(html, @"class=""moneysilver"">(\d+)");
        if (m.Success) silver = int.Parse(m.Groups[1].Value);
        m = Regex.Match(html, @"class=""moneycopper"">(\d+)");
        if (m.Success) copper = int.Parse(m.Groups[1].Value);
        return (gold, silver, copper);
    }

    private static List<string> ParseExtraLines(string html)
    {
        var lines = new List<string>();
        foreach (Match m in Regex.Matches(html, @"<div class=""whtt-extra[^""]*"">([^<]+)</div>", RegexOptions.IgnoreCase))
            AddLine(lines, m.Groups[1].Value);

        if (html.Contains("Lié quand ramassé", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("Binds when picked up", StringComparison.OrdinalIgnoreCase))
            AddLine(lines, "Lié quand ramassé");

        if (html.Contains(">Unique<", StringComparison.OrdinalIgnoreCase))
            AddLine(lines, "Unique");

        return lines;
    }

    private static void AddLine(List<string> lines, string raw)
    {
        var text = System.Net.WebUtility.HtmlDecode(raw).Trim();
        if (text.Length == 0) return;
        if (text.StartsWith("Niveau d'objet", StringComparison.OrdinalIgnoreCase)) return;
        if (text.StartsWith("Empilement maxi", StringComparison.OrdinalIgnoreCase)) return;
        if (text.StartsWith("Prix de Vente", StringComparison.OrdinalIgnoreCase)) return;
        if (!lines.Contains(text)) lines.Add(text);
    }
}
