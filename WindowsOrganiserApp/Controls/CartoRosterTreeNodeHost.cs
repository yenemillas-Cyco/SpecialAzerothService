using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WindowsOrganiserApp.Converters;
using WindowsOrganiserApp.Models.Carto;

namespace WindowsOrganiserApp.Controls;

/// <summary>Hôte TreeView : template visuel par type de nœud, largeur = TreeView − indentation.</summary>
public sealed class CartoRosterTreeNodeHost : ContentControl
{
    public static readonly DependencyProperty NodeProperty = DependencyProperty.Register(
        nameof(Node),
        typeof(CartoRosterTreeNode),
        typeof(CartoRosterTreeNodeHost),
        new PropertyMetadata(null, OnRebuild));

    public static readonly DependencyProperty CallbacksProperty = DependencyProperty.Register(
        nameof(Callbacks),
        typeof(CartoRosterTreeCallbacks),
        typeof(CartoRosterTreeNodeHost),
        new PropertyMetadata(null, OnRebuild));

    public CartoRosterTreeNode? Node
    {
        get => (CartoRosterTreeNode?)GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }

    public CartoRosterTreeCallbacks? Callbacks
    {
        get => (CartoRosterTreeCallbacks?)GetValue(CallbacksProperty);
        set => SetValue(CallbacksProperty, value);
    }

    private static void OnRebuild(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CartoRosterTreeNodeHost host)
            host.Rebuild();
    }

    private void Rebuild()
    {
        if (Node == null || Callbacks == null)
        {
            Content = null;
            ClearValue(WidthProperty);
            ClearValue(MarginProperty);
            return;
        }

        Content = CartoRosterTreeVisuals.Build(Node, Callbacks);
        ApplyWidthFromTreeView();
        ApplyDepthMargin();
    }

    private void ApplyWidthFromTreeView()
    {
        var binding = new MultiBinding
        {
            Converter = new TreeViewDescendantWidthConverter(),
            Mode = BindingMode.OneWay
        };
        binding.Bindings.Add(new Binding(nameof(TreeView.ActualWidth))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(TreeView), 1)
        });
        if (Node != null)
        {
            binding.Bindings.Add(new Binding(nameof(CartoRosterTreeNode.Depth))
            {
                Source = Node
            });
        }

        SetBinding(WidthProperty, binding);
        HorizontalAlignment = HorizontalAlignment.Left;
    }

    private void ApplyDepthMargin()
    {
        var extra = Node.Kind switch
        {
            CartoRosterNodeKind.Account => 6d,
            _ => 0d
        };
        SetBinding(MarginProperty, new Binding(nameof(CartoRosterTreeNode.Depth))
        {
            Source = Node,
            Converter = new TreeViewDepthMarginConverter(),
            ConverterParameter = extra
        });
    }
}
