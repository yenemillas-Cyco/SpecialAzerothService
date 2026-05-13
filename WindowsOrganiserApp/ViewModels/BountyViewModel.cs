using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
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
        RefreshStats();
    }

    public ObservableCollection<BountyEntry> Bounties { get; }

    [ObservableProperty] private BountyEntry? _editingBounty;
    [ObservableProperty] private string _rules;
    [ObservableProperty] private bool _isPopupOpen;
    [ObservableProperty] private bool _isNewBounty;

    [ObservableProperty] private string _newContribName = string.Empty;
    [ObservableProperty] private int _newContribGold;

    public ObservableCollection<BountyContributor>? EditingContributors =>
        EditingBounty != null ? new ObservableCollection<BountyContributor>(EditingBounty.Contributors) : null;

    [ObservableProperty] private string _discordCharCount = "0/2000";
    [ObservableProperty] private System.Windows.Media.SolidColorBrush _discordCharBrush =
        new(System.Windows.Media.Color.FromArgb(136, 255, 255, 255));

    public ObservableCollection<ContributorTotal> ContributorTotals { get; } = [];
    [ObservableProperty] private int _grandTotal;

    partial void OnEditingBountyChanged(BountyEntry? value)
    {
        OnPropertyChanged(nameof(EditingContributors));
        NewContribName = string.Empty;
        NewContribGold = 0;
    }

    public void Save()
    {
        _data.Bounties = [.. Bounties];
        _data.Rules = Rules;
        _bountyService.Save(_data);
        RefreshStats();
    }

    private void RefreshStats()
    {
        var text = BuildDiscordBountiesText();
        DiscordCharCount = $"{text.Length}/2000";
        DiscordCharBrush = text.Length > 2000
            ? new(System.Windows.Media.Color.FromRgb(255, 107, 107))
            : new(System.Windows.Media.Color.FromArgb(136, 255, 255, 255));

        ContributorTotals.Clear();
        var groups = Bounties
            .Where(b => !b.IsCompleted)
            .SelectMany(b => b.Contributors)
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Sum(c => c.GoldAmount));
        foreach (var g in groups)
            ContributorTotals.Add(new ContributorTotal(g.Key, g.Sum(c => c.GoldAmount)));
        GrandTotal = Bounties.Where(b => !b.IsCompleted).Sum(b => b.TotalGold);
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
        var result = MessageBox.Show(
            $"Supprimer la prime sur {bounty.TargetName} ({bounty.DisplayTotal}) ?",
            "Confirmation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
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
            GoldAmount = NewContribGold
        });

        NewContribName = string.Empty;
        NewContribGold = 0;

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
    private void ToggleExport(BountyEntry? bounty)
    {
        if (bounty == null) return;
        bounty.IsSelectedForExport = !bounty.IsSelectedForExport;
        RefreshList();
    }

    [RelayCommand]
    private void ToggleAllExport()
    {
        var allSelected = Bounties.All(b => b.IsSelectedForExport);
        foreach (var b in Bounties)
            b.IsSelectedForExport = !allSelected;
        RefreshList();
    }

    [RelayCommand]
    private void SaveRules()
    {
        Save();
    }

    [RelayCommand]
    private void CopyDiscordRules()
    {
        var sb = new StringBuilder();
        sb.AppendLine("**🏴‍☠️ AVIS DE RECHERCHE — CHASSEUR DE PRIMES 🏴‍☠️**");
        sb.AppendLine();
        sb.AppendLine("**Règlement :**");
        foreach (var line in Rules.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            sb.AppendLine(line.Trim());
        Clipboard.SetText(sb.ToString());
    }

    [RelayCommand]
    private void CopyDiscordBounties()
    {
        Clipboard.SetText(BuildDiscordBountiesText());
    }

    private string BuildDiscordBountiesText()
    {
        var sb = new StringBuilder();

        var active = Bounties.Where(b => !b.IsCompleted && b.IsSelectedForExport).ToList();
        if (active.Count > 0)
        {
            var nameWidth = active.Max(b =>
            {
                var n = string.IsNullOrWhiteSpace(b.AltName) ? b.TargetName : $"{b.TargetName} ou {b.AltName}";
                return n.Length;
            });
            var goldWidth = active.Max(b => b.DisplayTotal.Length);
            var reasonWidth = active.Max(b => string.IsNullOrWhiteSpace(b.Reason) ? 0 : b.Reason.Length + 2);
            nameWidth = Math.Max(nameWidth, 8);

            sb.AppendLine("**📋 Primes actives :**");
            sb.AppendLine("```");
            foreach (var b in active)
                sb.AppendLine(FormatBountyLineAligned(b, nameWidth, goldWidth, reasonWidth));
            sb.AppendLine("```");
        }

        var completed = Bounties.Where(b => b.IsCompleted).ToList();
        if (completed.Count > 0)
        {
            sb.AppendLine("**✅ Primes réclamées :**");
            foreach (var b in completed)
                sb.AppendLine($"-~~{b.TargetName}~~ {b.DisplayTotal}");
        }

        return sb.ToString();
    }

    [RelayCommand]
    private void CopyIndividualDiscord(BountyEntry? bounty)
    {
        if (bounty == null) return;
        Clipboard.SetText(FormatBountyLine(bounty));
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

    private static string FormatBountyLine(BountyEntry b)
    {
        var name = string.IsNullOrWhiteSpace(b.AltName) ? b.TargetName : $"{b.TargetName} ou {b.AltName}";
        var parts = new List<string> { $"-**{name}** {b.DisplayTotal}" };
        if (!string.IsNullOrWhiteSpace(b.Reason))
            parts.Add($"\"{b.Reason}\"");
        if (!string.IsNullOrEmpty(b.ContributorNames))
            parts.Add($"({b.ContributorNames})");
        return string.Join(" ", parts);
    }

    private static string FormatBountyLineAligned(BountyEntry b, int nameWidth, int goldWidth, int reasonWidth)
    {
        var name = string.IsNullOrWhiteSpace(b.AltName) ? b.TargetName : $"{b.TargetName} ou {b.AltName}";
        var reason = string.IsNullOrWhiteSpace(b.Reason) ? "" : $"\"{b.Reason}\"";
        var claimTo = b.ContributorNames;

        return $" {name.PadRight(nameWidth)} {b.DisplayTotal.PadRight(goldWidth)} {reason.PadRight(reasonWidth)} {claimTo}";
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [RelayCommand]
    private void ExportBounties()
    {
        var dlg = new SaveFileDialog
        {
            Title = "Exporter les primes",
            Filter = "Fichier JSON|*.json",
            FileName = "primes-export.json"
        };
        if (dlg.ShowDialog() != true) return;

        var data = new BountyData { Rules = Rules, Bounties = [.. Bounties] };
        var json = JsonSerializer.Serialize(data, JsonOpts);
        File.WriteAllText(dlg.FileName, json);
    }

    [RelayCommand]
    private void ImportBounties()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Importer des primes",
            Filter = "Fichier JSON|*.json"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var json = File.ReadAllText(dlg.FileName);
            var imported = JsonSerializer.Deserialize<BountyData>(json, JsonOpts);
            if (imported == null) return;

            var result = MessageBox.Show(
                $"{imported.Bounties.Count} prime(s) trouvée(s).\n\nRemplacer les primes actuelles ou les ajouter ?",
                "Import",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel) return;

            if (result == MessageBoxResult.Yes)
            {
                Bounties.Clear();
                Rules = imported.Rules;
            }

            foreach (var b in imported.Bounties)
            {
                if (Bounties.All(x => x.Id != b.Id))
                    Bounties.Add(b);
            }

            Save();
            RefreshList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur d'import : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshList()
    {
        var snapshot = Bounties.ToList();
        Bounties.Clear();
        foreach (var b in snapshot) Bounties.Add(b);
        RefreshStats();
    }
}

public sealed record ContributorTotal(string Name, int Gold);
