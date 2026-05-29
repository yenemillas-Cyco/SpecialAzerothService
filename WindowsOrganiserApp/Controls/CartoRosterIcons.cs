using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsOrganiserApp.Models.Carto;

namespace WindowsOrganiserApp.Controls;

/// <summary>Boutons roster : visibilité carte / sync / sous-arbre.</summary>
public static class CartoRosterIcons
{
    private const int ToggleSize = 22;
    private const double DiscSize = 18;

    private static readonly SolidColorBrush GoldDisc = new(Color.FromRgb(204, 162, 74));
    private static readonly SolidColorBrush HiddenDisc = new(Color.FromRgb(58, 62, 72));
    private sealed class VisibilityToggleTag(Func<bool> isVisible, string label, Action onToggled)
    {
        public void Refresh(ToggleButton toggle)
        {
            var visible = isVisible();
            toggle.SetCurrentValue(ToggleButton.IsCheckedProperty, visible);
            toggle.Content = BuildEyeDisc(visible);
            toggle.ToolTip = visible
                ? $"{label} visible — masquer"
                : $"{label} masqué — afficher";
        }

        public void Click(ToggleButton toggle)
        {
            onToggled();
            Refresh(toggle);
        }
    }

    public static void RefreshSubtreeVisibilityToggles(DependencyObject root)
    {
        if (root == null)
            return;

        foreach (var toggle in EnumerateVisualChildren<ToggleButton>(root))
        {
            if (toggle.Tag is VisibilityToggleTag tag)
                tag.Refresh(toggle);
        }
    }

    public static ToggleButton CreateSubtreeVisibilityToggle(
        Func<bool> isVisible,
        string scopeLabel,
        Action onToggled)
    {
        var tag = new VisibilityToggleTag(isVisible, scopeLabel, onToggled);
        var toggle = CreateVisibilityShell(tag);
        tag.Refresh(toggle);
        return toggle;
    }

    public static ToggleButton CreateMapVisibilityToggle(WowCharacter ch, Action<WowCharacter> onToggled)
    {
        var toggle = CreateVisibilityShell(null);
        void Refresh()
        {
            var visible = !ch.IsHidden;
            toggle.SetCurrentValue(ToggleButton.IsCheckedProperty, visible);
            toggle.Content = BuildEyeDisc(visible);
            toggle.ToolTip = visible
                ? "Visible sur la carte — masquer"
                : "Masqué sur la carte — afficher";
        }

        Refresh();
        WirePreviewActivate(toggle, () =>
        {
            onToggled(ch);
            Refresh();
        });
        return toggle;
    }

    public static ToggleButton CreateSyncToggle(WowCharacter ch, Action<WowCharacter> onToggled)
    {
        var toggle = CreateShellBase();
        void Refresh()
        {
            var syncOn = !ch.ExcludeFromSync;
            toggle.SetCurrentValue(ToggleButton.IsCheckedProperty, syncOn);
            toggle.Content = PlugIcon(syncOn);
            toggle.ToolTip = syncOn ? "Sync active — désactiver" : "Sync désactivée — activer";
        }

        Refresh();
        WirePreviewActivate(toggle, () =>
        {
            onToggled(ch);
            Refresh();
        });
        return toggle;
    }

    private static ToggleButton CreateVisibilityShell(VisibilityToggleTag? tag)
    {
        var toggle = CreateShellBase();
        toggle.Tag = tag;
        if (tag != null)
            WirePreviewActivate(toggle, () => tag.Click(toggle));

        return toggle;
    }

    private static ToggleButton CreateShellBase()
    {
        var toggle = new ToggleButton
        {
            Width = ToggleSize,
            Height = ToggleSize,
            MinWidth = ToggleSize,
            MinHeight = ToggleSize,
            MaxWidth = ToggleSize,
            MaxHeight = ToggleSize,
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Focusable = false,
            FocusVisualStyle = null,
            OverridesDefaultStyle = true,
            Template = CreateTransparentToggleTemplate()
        };

        return toggle;
    }

    private static ControlTemplate CreateTransparentToggleTemplate()
    {
        var template = new ControlTemplate(typeof(ToggleButton));
        var root = new FrameworkElementFactory(typeof(Border));
        root.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        root.SetValue(Border.BorderThicknessProperty, new Thickness(0));
        root.SetValue(Border.PaddingProperty, new Thickness(0));

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        root.AppendChild(content);

        template.VisualTree = root;
        return template;
    }

    /// <summary>Empêche le clic de replier l'expander ; exécute l'action ici car Handled bloque Click.</summary>
    private static void WirePreviewActivate(ToggleButton toggle, Action activate)
    {
        toggle.PreviewMouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            activate();
        };
    }

    private static UIElement BuildEyeDisc(bool visible)
    {
        var layer = new Grid
        {
            Width = DiscSize,
            Height = DiscSize,
            Background = Brushes.Transparent
        };
        layer.Children.Add(new Ellipse
        {
            Width = DiscSize,
            Height = DiscSize,
            Fill = visible ? GoldDisc : HiddenDisc
        });

        var eye = new Canvas { Width = DiscSize, Height = DiscSize, IsHitTestVisible = false };
        var white = Brushes.White;

        if (visible)
        {
            eye.Children.Add(new Path
            {
                Data = Geometry.Parse("M2.5,9 A6.5,3.2 0 1,0 15.5,9 A6.5,3.2 0 1,0 2.5,9"),
                Stroke = white,
                StrokeThickness = 1.3,
                Fill = Brushes.Transparent,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            });
            var pupil = new Ellipse { Width = 3.2, Height = 3.2, Fill = white };
            Canvas.SetLeft(pupil, 7.4);
            Canvas.SetTop(pupil, 7.4);
            eye.Children.Add(pupil);
        }
        else
        {
            eye.Children.Add(new Path
            {
                Data = Geometry.Parse("M2.5,9 A6.5,3.2 0 1,0 15.5,9 A6.5,3.2 0 1,0 2.5,9"),
                Stroke = white,
                StrokeThickness = 1.3,
                Fill = Brushes.Transparent,
                Opacity = 0.85,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            });
            eye.Children.Add(new Path
            {
                Data = Geometry.Parse("M4,4 L14,14"),
                Stroke = white,
                StrokeThickness = 1.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            });
        }

        layer.Children.Add(eye);
        return layer;
    }

    private static UIElement PlugIcon(bool active)
    {
        var brush = active ? GoldDisc : new SolidColorBrush(Color.FromRgb(100, 95, 85));
        return new Viewbox
        {
            Width = 12,
            Height = 12,
            Stretch = Stretch.Uniform,
            Child = new Path
            {
                Data = Geometry.Parse("M5,1 H11 V4.5 H13 V6 H11 V6 H11 V12 H9 V12 H9 V6 H7 V6 H7 V12 H5 V12 H5 V6 H3 V6 H3 V4.5 H5 Z"),
                Fill = brush
            }
        };
    }

    private static IEnumerable<T> EnumerateVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                yield return match;

            foreach (var nested in EnumerateVisualChildren<T>(child))
                yield return nested;
        }
    }
}
