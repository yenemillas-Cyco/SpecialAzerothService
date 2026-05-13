using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowsOrganiserApp.Models.Bounty;
using WindowsOrganiserApp.Services;

namespace WindowsOrganiserApp.ViewModels;

public partial class BountyViewModel : ObservableObject
{
    private readonly IBountyService _bountyService;
    private BountyData _data;

    public static string[] WowClasses => ["Guerrier", "Paladin", "Chasseur", "Voleur", "Prêtre", "Chaman", "Mage", "Démoniste", "Druide"];
    public static string[] WowRaces => ["Humain", "Nain", "Elfe de la nuit", "Gnome", "Orc", "Tauren", "Troll", "Mort-vivant"];

    public BountyViewModel(IBountyService bountyService)
    {
        _bountyService = bountyService;
        _data = _bountyService.Load();
        Bounties = new ObservableCollection<BountyEntry>(_data.Bounties);
        _rules = _data.Rules;
    }

    public ObservableCollection<BountyEntry> Bounties { get; }

    [ObservableProperty] private BountyEntry? _editingBounty;
    [ObservableProperty] private string _rules;
    [ObservableProperty] private bool _isPopupOpen;
    [ObservableProperty] private bool _isNewBounty;

    [ObservableProperty] private string _newContribName = string.Empty;
    [ObservableProperty] private int _newContribGold;
    [ObservableProperty] private int _newContribJewels;

    public ObservableCollection<BountyContributor>? EditingContributors =>
        EditingBounty != null ? new ObservableCollection<BountyContributor>(EditingBounty.Contributors) : null;

    partial void OnEditingBountyChanged(BountyEntry? value)
    {
        OnPropertyChanged(nameof(EditingContributors));
        NewContribName = string.Empty;
        NewContribGold = 0;
        NewContribJewels = 0;
    }

    public void Save()
    {
        _data.Bounties = [.. Bounties];
        _data.Rules = Rules;
        _bountyService.Save(_data);
    }

    [RelayCommand]
    private void NewBounty()
    {
        EditingBounty = new BountyEntry
        {
            TargetName = string.Empty,
            Reason = string.Empty
        };
        IsNewBounty = true;
        IsPopupOpen = true;
    }

    [RelayCommand]
    private void EditBounty(BountyEntry? bounty)
    {
        if (bounty == null) return;
        EditingBounty = bounty;
        IsNewBounty = false;
        IsPopupOpen = true;
        OnPropertyChanged(nameof(EditingContributors));
    }

    [RelayCommand]
    private void SaveBounty()
    {
        if (EditingBounty == null) return;

        if (IsNewBounty)
        {
            if (string.IsNullOrWhiteSpace(EditingBounty.TargetName)) return;
            Bounties.Add(EditingBounty);
        }

        IsPopupOpen = false;
        Save();
        RefreshList();
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsPopupOpen = false;
        if (IsNewBounty)
            EditingBounty = null;
    }

    [RelayCommand]
    private void RemoveBounty(BountyEntry? bounty)
    {
        if (bounty == null) return;
        Bounties.Remove(bounty);
        Save();
    }

    [RelayCommand]
    private void ToggleCompleted(BountyEntry? bounty)
    {
        if (bounty == null) return;
        bounty.IsCompleted = !bounty.IsCompleted;
        Save();
        RefreshList();
    }

    [RelayCommand]
    private void AddContributor()
    {
        if (EditingBounty == null || string.IsNullOrWhiteSpace(NewContribName)) return;

        EditingBounty.Contributors.Add(new BountyContributor
        {
            Name = NewContribName.Trim(),
            GoldAmount = NewContribGold,
            JewelAmount = NewContribJewels
        });

        NewContribName = string.Empty;
        NewContribGold = 0;
        NewContribJewels = 0;

        OnPropertyChanged(nameof(EditingContributors));
    }

    [RelayCommand]
    private void RemoveContributor(BountyContributor? contributor)
    {
        if (EditingBounty == null || contributor == null) return;
        EditingBounty.Contributors.Remove(contributor);
        OnPropertyChanged(nameof(EditingContributors));
    }

    [RelayCommand]
    private void SaveRules()
    {
        Save();
    }

    [RelayCommand]
    private void CopyDiscordMessage()
    {
        var sb = new StringBuilder();
        sb.AppendLine("**🏴‍☠️ AVIS DE RECHERCHE — CHASSEUR DE PRIMES 🏴‍☠️**");
        sb.AppendLine();
        sb.AppendLine("**Règlement :**");
        foreach (var line in Rules.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            sb.AppendLine(line.Trim());
        sb.AppendLine();

        var active = Bounties.Where(b => !b.IsCompleted).ToList();
        if (active.Count > 0)
        {
            sb.AppendLine("**📋 Primes actives :**");
            foreach (var b in active)
            {
                var name = string.IsNullOrWhiteSpace(b.AltName) ? b.TargetName : $"{b.TargetName} ou {b.AltName}";
                var claimTo = b.ContributorNames;

                sb.Append($"-**{name}** : {b.DisplayTotal}.");
                if (!string.IsNullOrWhiteSpace(b.Reason))
                    sb.Append($"     \"{b.Reason}\"");
                if (!string.IsNullOrWhiteSpace(claimTo))
                    sb.Append($" (prime à réclamer à {claimTo})");
                sb.AppendLine();
            }
        }

        var completed = Bounties.Where(b => b.IsCompleted).ToList();
        if (completed.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**✅ Primes réclamées :**");
            foreach (var b in completed)
                sb.AppendLine($"-~~{b.TargetName}~~ : {b.DisplayTotal}");
        }

        Clipboard.SetText(sb.ToString());
    }

    [RelayCommand]
    private void CopyImagePrompt(BountyEntry? bounty)
    {
        if (bounty == null) return;

        var raceClass = new List<string>();
        if (!string.IsNullOrWhiteSpace(bounty.TargetRace)) raceClass.Add(bounty.TargetRace.ToLower());
        if (!string.IsNullOrWhiteSpace(bounty.TargetClass)) raceClass.Add(bounty.TargetClass.ToLower());
        var rcText = raceClass.Count > 0 ? $"il joue un {string.Join(" ", raceClass)}" : "un personnage de WoW";

        var prompt = $"Je veux créer une image style avis de recherche western vintage, " +
                     $"parchemin brûlé, avec marqué \"AVIS DE RECHERCHE\" en haut, " +
                     $"\"Récompense de {bounty.TotalGold} PO pour tuer {bounty.TargetName}\", " +
                     $"mais version World of Warcraft : {rcText} pour l'image. " +
                     $"En bas : \"{bounty.TargetName} — MORT — {bounty.TotalGold} PIÈCES D'OR — À QUI LE TUERA !\". " +
                     $"Style épique, sombre, avec des pièces d'or.";

        Clipboard.SetText(prompt);
    }

    private void RefreshList()
    {
        var snapshot = Bounties.ToList();
        Bounties.Clear();
        foreach (var b in snapshot) Bounties.Add(b);
    }
}
