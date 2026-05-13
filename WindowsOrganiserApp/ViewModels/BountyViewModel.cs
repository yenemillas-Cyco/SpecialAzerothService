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

    [ObservableProperty] private BountyEntry? _selectedBounty;
    [ObservableProperty] private string _rules;
    [ObservableProperty] private bool _isRulesEditorOpen;

    [ObservableProperty] private string _newContribName = string.Empty;
    [ObservableProperty] private int _newContribGold;
    [ObservableProperty] private int _newContribJewels;
    [ObservableProperty] private string _newContribReason = string.Empty;

    public ObservableCollection<BountyContributor>? SelectedContributors =>
        SelectedBounty != null ? new ObservableCollection<BountyContributor>(SelectedBounty.Contributors) : null;

    partial void OnSelectedBountyChanged(BountyEntry? value)
    {
        OnPropertyChanged(nameof(SelectedContributors));
        NewContribName = string.Empty;
        NewContribGold = 0;
        NewContribJewels = 0;
        NewContribReason = string.Empty;
    }

    public void Save()
    {
        _data.Bounties = [.. Bounties];
        _data.Rules = Rules;
        _bountyService.Save(_data);
    }

    [RelayCommand]
    private void AddBounty()
    {
        var bounty = new BountyEntry
        {
            TargetName = "Nouveau joueur",
            Reason = "À définir"
        };
        Bounties.Add(bounty);
        SelectedBounty = bounty;
        Save();
    }

    [RelayCommand]
    private void RemoveBounty(BountyEntry? bounty)
    {
        if (bounty == null) return;
        Bounties.Remove(bounty);
        if (SelectedBounty == bounty)
            SelectedBounty = Bounties.FirstOrDefault();
        Save();
    }

    [RelayCommand]
    private void MarkCompleted()
    {
        if (SelectedBounty == null) return;
        SelectedBounty.IsCompleted = !SelectedBounty.IsCompleted;
        if (SelectedBounty.IsCompleted)
            SelectedBounty.CompletedAt = DateTime.Now;
        else
        {
            SelectedBounty.CompletedAt = null;
            SelectedBounty.KilledBy = null;
        }
        Save();
        RefreshList();
    }

    [RelayCommand]
    private void AddContributor()
    {
        if (SelectedBounty == null || string.IsNullOrWhiteSpace(NewContribName)) return;

        SelectedBounty.Contributors.Add(new BountyContributor
        {
            Name = NewContribName.Trim(),
            GoldAmount = NewContribGold,
            JewelAmount = NewContribJewels,
            Reason = NewContribReason.Trim()
        });

        NewContribName = string.Empty;
        NewContribGold = 0;
        NewContribJewels = 0;
        NewContribReason = string.Empty;

        Save();
        OnPropertyChanged(nameof(SelectedContributors));
        RefreshList();
    }

    [RelayCommand]
    private void RemoveContributor(BountyContributor? contributor)
    {
        if (SelectedBounty == null || contributor == null) return;
        SelectedBounty.Contributors.Remove(contributor);
        Save();
        OnPropertyChanged(nameof(SelectedContributors));
        RefreshList();
    }

    [RelayCommand]
    private void SaveRules()
    {
        IsRulesEditorOpen = false;
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
                var reasons = b.Contributors
                    .Where(c => !string.IsNullOrWhiteSpace(c.Reason))
                    .Select(c => c.Reason).Distinct();
                var reason = string.Join(" / ", reasons);
                var claimTo = b.ContributorNames;

                sb.Append($"-**{name}** : {b.DisplayTotal}.");
                if (!string.IsNullOrWhiteSpace(reason))
                    sb.Append($"     \"{reason}\"");
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
            {
                var killer = string.IsNullOrWhiteSpace(b.KilledBy) ? "???" : b.KilledBy;
                sb.AppendLine($"-~~{b.TargetName}~~ : {b.DisplayTotal} — Tué par **{killer}**");
            }
        }

        Clipboard.SetText(sb.ToString());
    }

    [RelayCommand]
    private void CopyImagePrompt()
    {
        if (SelectedBounty == null) return;
        var b = SelectedBounty;

        var raceClass = new List<string>();
        if (!string.IsNullOrWhiteSpace(b.TargetRace)) raceClass.Add(b.TargetRace.ToLower());
        if (!string.IsNullOrWhiteSpace(b.TargetClass)) raceClass.Add(b.TargetClass.ToLower());
        var rcText = raceClass.Count > 0 ? $"il joue un {string.Join(" ", raceClass)}" : "un personnage de WoW";

        var prompt = $"Je veux créer une image style avis de recherche western vintage, " +
                     $"parchemin brûlé, avec marqué \"AVIS DE RECHERCHE\" en haut, " +
                     $"\"Récompense de {b.TotalGold} PO pour tuer {b.TargetName}\", " +
                     $"mais version World of Warcraft : {rcText} pour l'image. " +
                     $"En bas : \"{b.TargetName} — MORT — {b.TotalGold} PIÈCES D'OR — À QUI LE TUERA !\". " +
                     $"Style épique, sombre, avec des pièces d'or.";

        Clipboard.SetText(prompt);
    }

    public void UpdateSelectedBounty()
    {
        Save();
        RefreshList();
    }

    private void RefreshList()
    {
        var idx = SelectedBounty != null ? Bounties.IndexOf(SelectedBounty) : -1;
        var snapshot = Bounties.ToList();
        Bounties.Clear();
        foreach (var b in snapshot) Bounties.Add(b);
        if (idx >= 0 && idx < Bounties.Count)
            SelectedBounty = Bounties[idx];
    }
}
