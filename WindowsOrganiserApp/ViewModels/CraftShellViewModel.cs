using CommunityToolkit.Mvvm.ComponentModel;

namespace WindowsOrganiserApp.ViewModels;

public partial class CraftShellViewModel : ObservableObject
{
    public CraftViewModel ProfessionsVm { get; }
    public CraftCraftingViewModel CraftingVm { get; }

    public CraftShellViewModel(CraftViewModel professionsVm, CraftCraftingViewModel craftingVm)
    {
        ProfessionsVm = professionsVm;
        CraftingVm = craftingVm;
    }
}
