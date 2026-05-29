using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WindowsOrganiserApp.Models.Conso;

namespace WindowsOrganiserApp.ViewModels;

public sealed class SelectableBoss : ObservableObject
{
    public BossInfo Boss { get; }
    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
                SelectionChanged?.Invoke();
        }
    }

    public string Label => Boss.Name;
    public ObservableCollection<EditableBossConsumable> EditableConsumables { get; } = [];

    public event Action? SelectionChanged;

    public SelectableBoss(BossInfo boss, Action onChanged)
    {
        Boss = boss;
        SelectionChanged = onChanged;
        foreach (var bc in boss.Consumables)
        {
            var ebc = new EditableBossConsumable(bc.ConsumableCode, bc.Quantity);
            ebc.QuantityChanged += onChanged;
            EditableConsumables.Add(ebc);
        }
    }
}

public sealed class EditableBossConsumable : ObservableObject
{
    public string Code { get; }
    private int _quantity;

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (value < 0) value = 0;
            if (SetProperty(ref _quantity, value))
                QuantityChanged?.Invoke();
        }
    }

    public event Action? QuantityChanged;

    public EditableBossConsumable(string code, int defaultQty)
    {
        Code = code;
        _quantity = defaultQty;
    }
}

public sealed class UiConsoCategory : ObservableObject
{
    public string Name { get; }
    public ObservableCollection<UiSelectableConsoItem> Items { get; } = [];

    public UiConsoCategory(ConsoCategory cat, Action onChanged)
    {
        Name = cat.Name;
        foreach (var item in cat.Items)
        {
            var ui = new UiSelectableConsoItem(item);
            ui.SelectionChanged += onChanged;
            Items.Add(ui);
        }
    }
}

public sealed class UiSelectableConsoItem : ObservableObject
{
    public SelectableConsoItem Source { get; }
    private bool _isSelected;
    private int _quantity;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                Source.IsSelected = value;
                SelectionChanged?.Invoke();
            }
        }
    }

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                Source.Quantity = value;
                SelectionChanged?.Invoke();
            }
        }
    }

    public string Code => Source.Item.Code;
    public string FullName => Source.Item.FullName;

    public event Action? SelectionChanged;

    public UiSelectableConsoItem(SelectableConsoItem source)
    {
        Source = source;
        _quantity = source.Quantity;
    }
}

public sealed class ConsoLine : ObservableObject
{
    public string Code { get; init; } = "";
    public string FullName { get; init; } = "";
    public int Quantity { get; init; }
    private bool _isOwned;
    public bool IsOwned
    {
        get => _isOwned;
        set
        {
            if (SetProperty(ref _isOwned, value))
                IsOwnedChanged?.Invoke();
        }
    }
    public event Action? IsOwnedChanged;
}

public sealed class MaterialSummary : ObservableObject
{
    public string Name { get; init; } = "";
    public int Total { get; init; }
    private bool _isOwned;
    public bool IsOwned
    {
        get => _isOwned;
        set => SetProperty(ref _isOwned, value);
    }
}

public sealed partial class ConsoViewModel : ObservableObject
{
    public ObservableCollection<SelectableBoss> Bosses { get; } = [];
    public ObservableCollection<UiConsoCategory> ExtraCategories { get; } = [];
    public ObservableCollection<ConsoLine> Consumables { get; } = [];
    public ObservableCollection<MaterialSummary> Materials { get; } = [];

    private readonly HashSet<string> _ownedConsos = [];
    private readonly HashSet<string> _ownedMats = [];

    [ObservableProperty]
    private bool _selectAll;

    public ConsoViewModel()
    {
        // Naxx / boss — masqué (UI retirée) ; données conservées dans NaxxData pour une phase ultérieure.
        /*
        foreach (var boss in NaxxData.Bosses)
        {
            var sb = new SelectableBoss(boss, Recalculate);
            Bosses.Add(sb);
        }
        */

        foreach (var cat in NaxxData.ExtraCategories)
        {
            ExtraCategories.Add(new UiConsoCategory(cat, Recalculate));
        }
    }

    partial void OnSelectAllChanged(bool value)
    {
        foreach (var b in Bosses)
            b.IsSelected = value;
    }

    private void Recalculate()
    {
        var consoTotals = new Dictionary<string, int>();

        foreach (var sb in Bosses.Where(b => b.IsSelected))
        {
            foreach (var ebc in sb.EditableConsumables)
            {
                if (consoTotals.ContainsKey(ebc.Code))
                    consoTotals[ebc.Code] += ebc.Quantity;
                else
                    consoTotals[ebc.Code] = ebc.Quantity;
            }
        }

        foreach (var cat in ExtraCategories)
        {
            foreach (var item in cat.Items.Where(i => i.IsSelected))
            {
                if (consoTotals.ContainsKey(item.Code))
                    consoTotals[item.Code] += item.Quantity;
                else
                    consoTotals[item.Code] = item.Quantity;
            }
        }

        Consumables.Clear();
        foreach (var (code, qty) in consoTotals.OrderBy(kv => kv.Key))
        {
            var recipe = NaxxData.Recipes.FirstOrDefault(r => r.Code == code);
            var line = new ConsoLine
            {
                Code = code,
                FullName = recipe?.FullName ?? code,
                Quantity = qty,
                IsOwned = _ownedConsos.Contains(code)
            };
            line.IsOwnedChanged += () =>
            {
                if (line.IsOwned) _ownedConsos.Add(line.Code);
                else _ownedConsos.Remove(line.Code);
                RecalculateMaterials();
            };
            Consumables.Add(line);
        }

        RecalculateMaterials();
    }

    private void RecalculateMaterials()
    {
        var matTotals = new Dictionary<string, int>();

        foreach (var conso in Consumables.Where(c => !c.IsOwned))
        {
            var recipe = NaxxData.Recipes.FirstOrDefault(r => r.Code == conso.Code);
            if (recipe == null) continue;
            foreach (var ing in recipe.Ingredients)
            {
                if (matTotals.ContainsKey(ing.Name))
                    matTotals[ing.Name] += ing.Quantity * conso.Quantity;
                else
                    matTotals[ing.Name] = ing.Quantity * conso.Quantity;
            }
        }

        Materials.Clear();
        foreach (var (name, total) in matTotals.OrderBy(kv => kv.Key))
        {
            var mat = new MaterialSummary
            {
                Name = name, Total = total,
                IsOwned = _ownedMats.Contains(name)
            };
            mat.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName != nameof(MaterialSummary.IsOwned)) return;
                if (mat.IsOwned) _ownedMats.Add(mat.Name);
                else _ownedMats.Remove(mat.Name);
            };
            Materials.Add(mat);
        }
    }
}
