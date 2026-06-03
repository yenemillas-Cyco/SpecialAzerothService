using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using SpecialAzerothService.Core.Models.Carto;

namespace WindowsOrganiserApp.Controls;

/// <summary>Œil carte : masque / affiche sur la carte (le roster reste affiché).</summary>
public static class CartoRosterIcons
{
    private sealed class MapVisibilityToggleTag(Func<bool> isVisible, string label, Action onToggled)
    {
        public void Refresh(ToggleButton toggle)
        {
            var visible = isVisible();
            toggle.SetCurrentValue(ToggleButton.IsCheckedProperty, visible);
            toggle.Content = BuildEyeDisc(visible);
            toggle.ToolTip = visible
                ? $"{label} visible sur la carte — masquer"
                : $"{label} masqué sur la carte — afficher";
        }

        public void Click(ToggleButton toggle)
        {
            onToggled();
            Refresh(toggle);
        }
    }

    public static void RefreshMapVisibilityToggles(DependencyObject root)
    {
        if (root == null)
            return;

        foreach (var toggle in EnumerateVisualChildren<ToggleButton>(root))
        {
            if (toggle.Tag is MapVisibilityToggleTag tag)
                tag.Refresh(toggle);
        }
    }

    public static ToggleButton CreateMapSubtreeVisibilityToggle(
        Func<bool> isVisible,
        string scopeLabel,
        Action onToggled)
    {
        var tag = new MapVisibilityToggleTag(isVisible, scopeLabel, onToggled);
        var toggle = CreateShellBase();
        toggle.Tag = tag;
        WirePreviewActivate(toggle, () => tag.Click(toggle));
        tag.Refresh(toggle);
        return toggle;
    }

    private const int ToggleSize = 22;
    private const double DiscSize = 18;

    private static readonly SolidColorBrush GoldDisc = new(Color.FromRgb(204, 162, 74));
    private static readonly SolidColorBrush HiddenDisc = new(Color.FromRgb(58, 62, 72));

    public static ToggleButton CreateMapVisibilityToggle(WowCharacter ch, Action<WowCharacter> onToggled)
    {
        var toggle = CreateShellBase();
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
