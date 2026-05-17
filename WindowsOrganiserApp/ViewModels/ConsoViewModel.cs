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

    public string Label => $"{Boss.Name}  ({string.Join(" + ", Boss.Consumables.Select(c => $"{c.ConsumableCode}x{c.Quantity}"))})";

    public event Action? SelectionChanged;

    public SelectableBoss(BossInfo boss) => Boss = boss;
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
}

public sealed class MaterialSummary : ObservableObject
{
    public string Name { get; init; } = "";
    public int Total { get; init; }
}

public sealed partial class ConsoViewModel : ObservableObject
{
    public ObservableCollection<SelectableBoss> Bosses { get; } = [];
    public ObservableCollection<UiConsoCategory> ExtraCategories { get; } = [];
    public ObservableCollection<ConsoLine> Consumables { get; } = [];
    public ObservableCollection<MaterialSummary> Materials { get; } = [];

    [ObservableProperty]
    private bool _selectAll;

    public ConsoViewModel()
    {
        foreach (var boss in NaxxData.Bosses)
        {
            var sb = new SelectableBoss(boss);
            sb.SelectionChanged += Recalculate;
            Bosses.Add(sb);
        }

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
            foreach (var bc in sb.Boss.Consumables)
            {
                if (consoTotals.ContainsKey(bc.ConsumableCode))
                    consoTotals[bc.ConsumableCode] += bc.Quantity;
                else
                    consoTotals[bc.ConsumableCode] = bc.Quantity;
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
            Consumables.Add(new ConsoLine
            {
                Code = code,
                FullName = recipe?.FullName ?? code,
                Quantity = qty
            });
        }

        var matTotals = new Dictionary<string, int>();
        foreach (var (code, qty) in consoTotals)
        {
            var recipe = NaxxData.Recipes.FirstOrDefault(r => r.Code == code);
            if (recipe == null) continue;
            foreach (var ing in recipe.Ingredients)
            {
                if (matTotals.ContainsKey(ing.Name))
                    matTotals[ing.Name] += ing.Quantity * qty;
                else
                    matTotals[ing.Name] = ing.Quantity * qty;
            }
        }

        Materials.Clear();
        foreach (var (name, total) in matTotals.OrderBy(kv => kv.Key))
        {
            Materials.Add(new MaterialSummary { Name = name, Total = total });
        }
    }
}
