using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SpecialAzerothService.Core.Models.Craft;
using SpecialAzerothService.Core.Models.WowSync;

namespace WindowsOrganiserApp.Services;

/// <summary>Adaptateur WPF : délègue Wowhead au Core et convertit les octets d'icône en ImageSource.</summary>
public interface IWowItemLookupService
{
    Task<ImageSource?> GetIconAsync(WowItem item, CancellationToken cancellationToken = default);
    Task<WowheadItemDetails?> GetDetailsAsync(WowItem item, CancellationToken cancellationToken = default);
    Task<ImageSource?> GetSpellIconAsync(int spellId, CancellationToken cancellationToken = default);
    Task<WowheadItemDetails?> GetSpellDetailsAsync(int spellId, CancellationToken cancellationToken = default);
    Task<bool> IsInfinitelyVendorPurchasableAsync(int itemId, CancellationToken cancellationToken = default);
    Task<CraftVendorPurchaseInfo> GetVendorPurchaseInfoAsync(int itemId, CancellationToken cancellationToken = default);
}

public sealed class WowItemLookupService : IWowItemLookupService
{
    private readonly IWowheadDataService _data;
    private readonly ConcurrentDictionary<string, Task<ImageSource?>> _imageCache = new();

    public WowItemLookupService(IWowheadDataService data) => _data = data;

    public Task<WowheadItemDetails?> GetDetailsAsync(WowItem item, CancellationToken cancellationToken = default) =>
        _data.GetDetailsAsync(item, cancellationToken);

    public Task<WowheadItemDetails?> GetSpellDetailsAsync(int spellId, CancellationToken cancellationToken = default) =>
        _data.GetSpellDetailsAsync(spellId, cancellationToken);

    public Task<bool> IsInfinitelyVendorPurchasableAsync(int itemId, CancellationToken cancellationToken = default) =>
        _data.IsInfinitelyVendorPurchasableAsync(itemId, cancellationToken);

    public Task<CraftVendorPurchaseInfo> GetVendorPurchaseInfoAsync(int itemId, CancellationToken cancellationToken = default) =>
        _data.GetVendorPurchaseInfoAsync(itemId, cancellationToken);

    public async Task<ImageSource?> GetIconAsync(WowItem item, CancellationToken cancellationToken = default)
    {
        if (item.ItemId > 0)
        {
            var key = $"item:{item.ItemId}";
            return await _imageCache.GetOrAdd(key, _ => ToImageSourceAsync(_data.GetIconAsync(item, cancellationToken)))
                .ConfigureAwait(false);
        }

        if (item.SpellId > 0)
            return await GetSpellIconAsync(item.SpellId, cancellationToken).ConfigureAwait(false);

        return null;
    }

    public Task<ImageSource?> GetSpellIconAsync(int spellId, CancellationToken cancellationToken = default)
    {
        if (spellId <= 0) return Task.FromResult<ImageSource?>(null);
        var key = $"spell:{spellId}";
        return _imageCache.GetOrAdd(key, _ => ToImageSourceAsync(_data.GetSpellIconAsync(spellId, cancellationToken)));
    }

    private static async Task<ImageSource?> ToImageSourceAsync(Task<byte[]?> bytesTask)
    {
        var bytes = await bytesTask.ConfigureAwait(false);
        if (bytes is not { Length: > 0 }) return null;

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
}
