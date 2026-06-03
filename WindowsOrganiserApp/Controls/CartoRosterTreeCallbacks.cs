using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SpecialAzerothService.Core.Models.Carto;
using WindowsOrganiserApp.ViewModels;

namespace WindowsOrganiserApp.Controls;

/// <summary>Actions UI roster (drag, drop, cartes perso).</summary>
public sealed class CartoRosterTreeCallbacks
{
    public required CartoViewModel ViewModel { get; init; }

    public Func<WowCharacter, Border>? BuildCharacterCard { get; init; }

    public Action<CharacterStatus, DragEventArgs>? CategoryDragOver { get; init; }
    public Action<CharacterStatus, DragEventArgs>? CategoryDrop { get; init; }
    public Action? CategoryDragLeave { get; init; }
}
