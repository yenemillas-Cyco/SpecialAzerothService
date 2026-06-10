using System.Windows;
using System.Windows.Controls;
using SpecialAzerothService.Core.Models.WowSync;

namespace WindowsOrganiserApp.Controls;

public partial class CartoItemSearchResultRow : UserControl
{
    public CartoItemSearchResultRow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is WowItemSearchResult result)
            ClassIconImage.Source = WowClassIcon.GetBitmap(result.CharacterClass);
        else
            ClassIconImage.Source = null;
    }
}
