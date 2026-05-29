using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using SpecialAzerothService.Core.Models.Craft;
using SpecialAzerothService.Core.Models.WowSync;

namespace SpecialAzerothService.Core.Services;

public interface IWowheadDataService
{
    Task<byte[]?> GetIconAsync(WowItem item, CancellationToken cancellationToken = default);
    Task<WowheadItemDetails?> GetDetailsAsync(WowItem item, CancellationToken cancellationToken = default);
    Task<byte[]?> GetSpellIconAsync(int spellId, CancellationToken cancellationToken = default);
    Task<WowheadItemDetails?> GetSpellDetailsAsync(int spellId, CancellationToken cancellationToken = default);
    /// <summary>Vrai si tous les PNJ vendeurs ont un stock illimité (Wowhead stock = -1).</summary>
    Task<bool> IsInfinitelyVendorPurchasableAsync(int itemId, CancellationToken cancellationToken = default);

    /// <summary>Stock illimité chez les marchands et prix unitaire (cuivre) le moins cher.</summary>
    Task<CraftVendorPurchaseInfo> GetVendorPurchaseInfoAsync(int itemId, CancellationToken cancellationToken = default);
}

public sealed class WowheadDataService : IWowheadDataService
{
    // Wowhead renvoie les prix dans une unité "pratique" différente (souvent 100x la valeur en cuivre WoW).
    // On normalise ici pour que le reste de l'app travaille en "cuivre" WoW (1 po = 10 000 c).
    private const int WowheadPriceScaleToCopper = 25;

    private static int NormalizeWowheadPriceToCopper(int wowheadPrice)
        => wowheadPrice <= 0 ? 0 : wowheadPrice / WowheadPriceScaleToCopper;

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private readonly ConcurrentDictionary<int, Task<WowheadItemDetails?>> _detailsCache = new();
    private readonly ConcurrentDictionary<int, Task<WowheadItemDetails?>> _spellDetailsCache = new();
    /// <summary>Cache uniquement les résultats positifs (évite de figer un faux négatif après correction du parseur).</summary>
    private readonly ConcurrentDictionary<int, bool> _infiniteVendorPositiveCache = new();
    private readonly ConcurrentDictionary<int, CraftVendorPurchaseInfo> _vendorInfoCache = new();
    private readonly ConcurrentDictionary<string, Task<byte[]?>> _imageCache = new();

    static WowheadDataService()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) SpecialAzerothService/1.0");
    }

    public async Task<byte[]?> GetIconAsync(WowItem item, CancellationToken cancellationToken = default)
    {
        if (item.ItemId > 0)
        {
            var key = $"item:{item.ItemId}";
            return await _imageCache.GetOrAdd(key, _ => LoadItemIconAsync(item.ItemId, cancellationToken))
                .ConfigureAwait(false);
        }

        if (item.SpellId > 0)
            return await GetSpellIconAsync(item.SpellId, cancellationToken).ConfigureAwait(false);

        return null;
    }

    public Task<WowheadItemDetails?> GetDetailsAsync(WowItem item, CancellationToken cancellationToken = default)
    {
        if (item.ItemId > 0)
            return _detailsCache.GetOrAdd(item.ItemId, id => SafeFetchItemDetailsAsync(id, cancellationToken));

        if (item.SpellId > 0)
            return GetSpellDetailsAsync(item.SpellId, cancellationToken);

        return Task.FromResult<WowheadItemDetails?>(null);
    }

    public Task<byte[]?> GetSpellIconAsync(int spellId, CancellationToken cancellationToken = default)
    {
        if (spellId <= 0) return Task.FromResult<byte[]?>(null);
        var key = $"spell:{spellId}";
        return _imageCache.GetOrAdd(key, _ => LoadSpellIconAsync(spellId, cancellationToken));
    }

    public Task<WowheadItemDetails?> GetSpellDetailsAsync(int spellId, CancellationToken cancellationToken = default)
    {
        if (spellId <= 0) return Task.FromResult<WowheadItemDetails?>(null);
        return _spellDetailsCache.GetOrAdd(spellId, id => SafeFetchSpellDetailsAsync(id, cancellationToken));
    }

    public async Task<bool> IsInfinitelyVendorPurchasableAsync(int itemId, CancellationToken cancellationToken = default)
    {
        var info = await GetVendorPurchaseInfoAsync(itemId, cancellationToken).ConfigureAwait(false);
        return info.IsInfiniteStock;
    }

    public async Task<CraftVendorPurchaseInfo> GetVendorPurchaseInfoAsync(int itemId, CancellationToken cancellationToken = default)
    {
        if (itemId <= 0) return CraftVendorPurchaseInfo.None;

        if (_vendorInfoCache.TryGetValue(itemId, out var cached))
            return cached;

        CraftVendorPurchaseInfo info;
        if (KnownInfiniteVendorItemIds.Contains(itemId))
        {
            var buyPrice = await TryFetchItemBuyPriceCopperAsync(itemId, cancellationToken).ConfigureAwait(false);
            info = new CraftVendorPurchaseInfo(true, buyPrice);
        }
        else
        {
            info = await FetchVendorPurchaseInfoAsync(itemId, cancellationToken).ConfigureAwait(false);
        }

        if (info.IsInfiniteStock)
        {
            _infiniteVendorPositiveCache[itemId] = true;
            _vendorInfoCache[itemId] = info;
        }

        return info;
    }

    private async Task<WowheadItemDetails?> SafeFetchItemDetailsAsync(int itemId, CancellationToken cancellationToken)
    {
        try
        {
            return await FetchItemDetailsAsync(itemId, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private async Task<WowheadItemDetails?> SafeFetchSpellDetailsAsync(int spellId, CancellationToken cancellationToken)
    {
        try
        {
            return await FetchSpellDetailsAsync(spellId, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private async Task<CraftVendorPurchaseInfo> FetchVendorPurchaseInfoAsync(int itemId, CancellationToken cancellationToken)
    {
        foreach (var url in BuildVendorPageUrls(itemId))
        {
            try
            {
                using var response = await Http.GetAsync(url, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) continue;

                var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var offers = ParseVendorOffers(html);
                if (offers.Count == 0) continue;

                if (!offers.All(o => o.Stock < 0))
                    return CraftVendorPurchaseInfo.None;

                var prices = offers.Where(o => o.PriceCopper > 0).Select(o => o.PriceCopper).ToList();
                var unitPrice = prices.Count > 0
                    ? prices.Min()
                    : await TryFetchItemBuyPriceCopperAsync(itemId, cancellationToken).ConfigureAwait(false);

                return new CraftVendorPurchaseInfo(true, unitPrice);
            }
            catch
            {
                // try next URL
            }
        }

        return CraftVendorPurchaseInfo.None;
    }

    private static IEnumerable<string> BuildVendorPageUrls(int itemId)
    {
        yield return $"https://www.wowhead.com/classic/fr/item={itemId}/vendeurs";
        yield return $"https://www.wowhead.com/classic/fr/item={itemId}/sold-by";
        yield return $"https://www.wowhead.com/classic/item={itemId}/sold-by";
    }

    private async Task<int> TryFetchItemBuyPriceCopperAsync(int itemId, CancellationToken cancellationToken)
    {
        foreach (var url in new[]
        {
            $"https://www.wowhead.com/classic/fr/item={itemId}",
            $"https://www.wowhead.com/classic/item={itemId}"
        })
        {
            try
            {
                using var response = await Http.GetAsync(url, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) continue;

                var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var buyPrice = ParseItemBuyPriceCopper(html, itemId);
                if (buyPrice > 0) return buyPrice;
            }
            catch
            {
                // try next URL
            }
        }

        return 0;
    }

    internal static int ParseItemBuyPriceCopper(string html, int itemId)
    {
        var itemKey = $"\"{itemId}\":";
        var idx = html.IndexOf(itemKey, StringComparison.Ordinal);
        if (idx < 0) return 0;

        var slice = html.Substring(idx, Math.Min(1200, html.Length - idx));
        var m = Regex.Match(slice, @"""buyprice""\s*:\s*(\d+)", RegexOptions.IgnoreCase);
        if (!m.Success || !int.TryParse(m.Groups[1].Value, out var rawPrice)) return 0;
        return NormalizeWowheadPriceToCopper(rawPrice);
    }

    private readonly record struct VendorOffer(int Stock, int PriceCopper);

    /// <summary>Extrait stock et prix (cuivre) de chaque marchand listé sur Wowhead.</summary>
    private static List<VendorOffer> ParseVendorOffers(string html)
    {
        var vendorDataJson = ExtractSoldByVendorDataJson(html);
        if (vendorDataJson == null)
            return [];

        var offers = new List<VendorOffer>();
        foreach (Match m in Regex.Matches(
                     vendorDataJson,
                     @"""stock""\s*:\s*(-?\d+).*?""cost""\s*:\s*\[\[(\d+)\]\]",
                     RegexOptions.Singleline | RegexOptions.IgnoreCase))
        {
            if (int.TryParse(m.Groups[1].Value, out var stock) &&
                int.TryParse(m.Groups[2].Value, out var rawPrice))
                offers.Add(new VendorOffer(stock, NormalizeWowheadPriceToCopper(rawPrice)));
        }

        return offers;
    }

    /// <summary>Repère le tableau JSON du listview « sold-by » (gestion correcte des crochets imbriqués).</summary>
    internal static string? ExtractSoldByVendorDataJson(string html)
    {
        var searchFrom = 0;
        while (searchFrom < html.Length)
        {
            var extraIdx = html.IndexOf("extraCols:", searchFrom, StringComparison.OrdinalIgnoreCase);
            if (extraIdx < 0) return null;

            var stockIdx = html.IndexOf("stock", extraIdx, StringComparison.OrdinalIgnoreCase);
            if (stockIdx < 0 || stockIdx - extraIdx > 80)
            {
                searchFrom = extraIdx + 10;
                continue;
            }

            var dataIdx = html.IndexOf("data:", stockIdx, StringComparison.OrdinalIgnoreCase);
            if (dataIdx < 0 || dataIdx - stockIdx > 400)
            {
                searchFrom = extraIdx + 10;
                continue;
            }

            var arrayStart = html.IndexOf('[', dataIdx);
            if (arrayStart < 0)
            {
                searchFrom = extraIdx + 10;
                continue;
            }

            var depth = 0;
            for (var i = arrayStart; i < html.Length; i++)
            {
                switch (html[i])
                {
                    case '[':
                        depth++;
                        break;
                    case ']':
                        depth--;
                        if (depth == 0)
                            return html.Substring(arrayStart, i - arrayStart + 1);
                        break;
                }
            }

            return null;
        }

        return null;
    }

    /// <summary>Composants d'artisanat classiques vendus en stock illimité chez les PNJ (secours si Wowhead échoue).</summary>
    private static readonly HashSet<int> KnownInfiniteVendorItemIds =
    [
        2320, // Bobine grossière
        2321, // Bobine fine
        2324, // Désaltérant
        2604, // Teinture rouge
        2605, // Teinture verte
        2606, // Teinture jaune
        2607, // Teinture bleue
        2608, // Teinture rouge vif
        3371, // Fiole vide
        3372, // Fiole de plomb
        4289, // Sel
        4291, // Bobine de soie
        4340, // Teinture grise
        4341, // Teinture jaune vif
        4342, // Teinture rouge
        6529, // Huile de feu
        6530, // Huile glaciale
        8343, // Bobine de soie lourde
        8925, // Levure chimique
        18256 // Huile de mana distillée (souvent illimitée chez alchimistes)
    ];

    private async Task<byte[]?> LoadItemIconAsync(int itemId, CancellationToken cancellationToken)
    {
        var details = await _detailsCache.GetOrAdd(itemId, id => FetchItemDetailsAsync(id, cancellationToken))
            .ConfigureAwait(false);
        return await LoadIconFromDetailsAsync(details, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]?> LoadSpellIconAsync(int spellId, CancellationToken cancellationToken)
    {
        var details = await _spellDetailsCache.GetOrAdd(spellId, id => FetchSpellDetailsAsync(id, cancellationToken))
            .ConfigureAwait(false);
        return await LoadIconFromDetailsAsync(details, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]?> LoadIconFromDetailsAsync(WowheadItemDetails? details, CancellationToken cancellationToken)
    {
        if (details == null || string.IsNullOrEmpty(details.IconSlug)) return null;

        try
        {
            var url = $"https://wow.zamimg.com/images/wow/icons/large/{details.IconSlug}.jpg";
            var bytes = await Http.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
            return bytes.Length == 0 ? null : bytes;
        }
        catch
        {
            return null;
        }
    }

    private async Task<WowheadItemDetails?> FetchItemDetailsAsync(int itemId, CancellationToken cancellationToken)
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

    private async Task<WowheadItemDetails?> FetchSpellDetailsAsync(int spellId, CancellationToken cancellationToken)
    {
        foreach (var apiUrl in new[]
        {
            $"https://nether.wowhead.com/classic/fr/tooltip/spell/{spellId}?data=3",
            $"https://nether.wowhead.com/classic-era/fr/tooltip/spell/{spellId}?data=3",
            $"https://nether.wowhead.com/classic/tooltip/spell/{spellId}?data=3&locale=7"
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
