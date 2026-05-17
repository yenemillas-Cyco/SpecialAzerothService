using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowsOrganiserApp.Models.WowSync;
using WindowsOrganiserApp.Services;

namespace WindowsOrganiserApp.ViewModels;

public partial class WowSyncViewModel : ObservableObject
{
    private readonly IWowSyncService _wowSyncService;

    public WowSyncViewModel(IWowSyncService wowSyncService)
    {
        _wowSyncService = wowSyncService;
        _wowPath = _wowSyncService.WowPath;
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

    [ObservableProperty]
    private string _statusText = "";

    [RelayCommand]
    private void BrowseWowPath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Sélectionner le dossier WoW Classic"
        };
        if (dialog.ShowDialog() == true)
        {
            WowPath = dialog.FolderName;
        }
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
            StatusText = "✅ Addon WowSync déployé avec succès !";
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
            var wtfExists = System.IO.Directory.Exists(wtfPath);
            var accounts = _wowSyncService.ReadAllAccounts();
            Accounts.Clear();
            foreach (var a in accounts)
                Accounts.Add(a);

            var totalChars = accounts.Sum(a => a.Characters.Count);
            if (!wtfExists)
                StatusText = $"❌ Dossier introuvable: {wtfPath}";
            else if (totalChars == 0)
                StatusText = $"⚠ Dossier OK mais 0 WowSync.lua trouvé. Chemin: {wtfPath}";
            else
                StatusText = $"✅ {accounts.Count} compte(s), {totalChars} personnage(s) trouvé(s).";
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Erreur lecture: {ex.Message}";
        }
    }
}
