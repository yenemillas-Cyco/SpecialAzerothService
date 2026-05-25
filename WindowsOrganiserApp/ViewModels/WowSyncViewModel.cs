using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using WindowsOrganiserApp.Models.Carto;
using WindowsOrganiserApp.Models.WowSync;
using WindowsOrganiserApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WindowsOrganiserApp.ViewModels;

public partial class WowSyncViewModel : ObservableObject
{
    private readonly IWowSyncService _wowSyncService;
    private readonly CartoViewModel _cartoVm;

    public MainViewModel? MainVm { get; set; }

    public string AddonVersion => _wowSyncService.AddonVersion;

    public WowSyncViewModel(IWowSyncService wowSyncService, CartoViewModel cartoVm)
    {
        _wowSyncService = wowSyncService;
        _cartoVm = cartoVm;
        _wowPath = _wowSyncService.WowPath;

        if (!string.IsNullOrWhiteSpace(WowPath))
        {
            Application.Current?.Dispatcher.BeginInvoke(
                Refresh,
                DispatcherPriority.ApplicationIdle);
        }
    }

    [ObservableProperty]
    private string _wowPath;

    partial void OnWowPathChanged(string value)
    {
        _wowSyncService.WowPath = value;
    }

    public ObservableCollection<WowAccountData> Accounts { get; } = [];

    [ObservableProperty]
    private WowCharacterData? _selectedCharacter;

    partial void OnSelectedCharacterChanged(WowCharacterData? value) => UpdateCartoPositionHint();

    [ObservableProperty]
    private string _cartoPositionHint = "";

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private string _searchQuery = "";

    public ObservableCollection<WowItemSearchResult> SearchResults { get; } = [];

    partial void OnSearchQueryChanged(string value) => UpdateSearch();

    [RelayCommand]
    private void BrowseWowPath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Sélectionner le dossier WoW Classic"
        };
        if (dialog.ShowDialog() == true)
            WowPath = dialog.FolderName;
    }

    [RelayCommand]
    private void DeployAddon()
    {
        if (string.IsNullOrWhiteSpace(WowPath))
        {
            StatusText = "⚠ Configurez le chemin WoW d'abord.";
            return;
        }

        try
        {
            _wowSyncService.DeployAddon();
            StatusText = $"✅ Addon WowSync v{WowSyncService.AddonVersionValue} déployé vers {Path.Combine(_wowSyncService.WowPath.Trim(), "WowSync")} — /reload puis vérifiez « v{WowSyncService.AddonVersionValue} » sur le panneau in-game.";
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Erreur: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        if (string.IsNullOrWhiteSpace(WowPath))
        {
            StatusText = "⚠ Configurez le chemin WoW d'abord.";
            return;
        }

        try
        {
            var wtfPath = _wowSyncService.ResolvedWtfPath;
            var wtfExists = Directory.Exists(wtfPath);
            var accounts = _wowSyncService.ReadAllAccounts();
            Accounts.Clear();
            foreach (var a in accounts)
                Accounts.Add(a);

            UpdateSearch();
            var synced = SyncAllPositionsToCarto();

            var totalChars = accounts.Sum(a => a.Characters.Count);
            if (!wtfExists)
                StatusText = $"❌ Dossier introuvable: {wtfPath}";
            else if (totalChars == 0)
                StatusText = $"⚠ Dossier OK mais 0 WowSync.lua trouvé. Chemin: {wtfPath}";
            else
                StatusText = synced > 0
                    ? $"✅ {accounts.Count} compte(s), {totalChars} perso(s) — {synced} sur Carto."
                    : $"✅ {accounts.Count} compte(s), {totalChars} personnage(s) trouvé(s).";
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Erreur lecture: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchQuery = "";
    }

    [RelayCommand]
    private void SelectSearchResult(WowItemSearchResult? result)
    {
        if (result == null) return;
        SelectedCharacter = result.Character;
    }

    [RelayCommand]
    private void PlaceOnCarto()
    {
        if (SelectedCharacter == null) return;

        var count = _cartoVm.RefreshCharactersFromWowSync();
        StatusText = count > 0
            ? $"✅ {count} personnage(s) sur Carto (pile en haut à gauche)."
            : "⚠ Aucun personnage — configurez le chemin WoW et actualisez.";

        if (MainVm != null)
        {
            MainVm.IsCartoMode = true;
            MainVm.IsWowSyncMode = false;
        }
    }

    /// <summary>Recharge la liste Carto depuis WowSync.</summary>
    public int SyncAllPositionsToCarto()
    {
        var count = _cartoVm.RefreshCharactersFromWowSync();
        if (SelectedCharacter != null)
            UpdateCartoPositionHint();
        return count;
    }

    private void UpdateCartoPositionHint()
    {
        if (SelectedCharacter == null)
        {
            CartoPositionHint = "";
            return;
        }

        if (SelectedCharacter.X <= 0 && SelectedCharacter.Y <= 0)
        {
            CartoPositionHint = "🗺 Carto : coords à 0 — redéployez l'addon, reconnectez-vous, /reload";
            return;
        }

        var onCarto = _cartoVm.Characters.Any(c =>
            c.SyncKey.Equals(SelectedCharacter.Key, StringComparison.OrdinalIgnoreCase));
        CartoPositionHint = onCarto
            ? "🗺 Carto : affiché en pile (haut gauche) — actualisez pour mettre à jour sac/banque."
            : "🗺 Carto : actualisez WowSync pour l'afficher sur la carte.";
    }

    private void UpdateSearch()
    {
        var results = CartoItemSearch.Search(Accounts, SearchQuery);
        SearchResults.Clear();
        foreach (var r in results)
            SearchResults.Add(r);
    }
}
