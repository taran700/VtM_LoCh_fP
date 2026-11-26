using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Data;
using System.Windows.Markup;

namespace VtM_LoCh_fP
{
    internal sealed class CombatManeuversWindow : Window
    {
        private readonly IList<CombatManeuverEntry> _maneuvers;
        private readonly StackPanel _listPanel;
        private readonly List<Action> _collapseManeuvers = new List<Action>();
        private readonly Dictionary<string, ToggleButton> _categoryButtons =
            new Dictionary<string, ToggleButton>(StringComparer.OrdinalIgnoreCase);
        private static readonly string[] Categories = { "defensive", "melee", "ranged" };

        public CombatManeuversWindow(Window owner)
        {
            Owner = owner;
            ThemeManager.AttachWindow(this);
            ThemeManager.ThemeChanged += OnThemeChanged;
            Closed += (s, e) => ThemeManager.ThemeChanged -= OnThemeChanged;
            Title = "Боевые манёвры";
            Width = 820;
            Height = 760;
            MinWidth = 600;
            MinHeight = 460;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(Window.BackgroundProperty, "BgBrush");
            SetResourceReference(Control.ForegroundProperty, "TextBrush");
            _maneuvers = CombatManeuverService.GetAll();

            var shell = new Border { Padding = new Thickness(20) };
            shell.SetResourceReference(Border.BackgroundProperty, "BgBrush");
            Content = shell;
            var root = new DockPanel();
            shell.Child = root;
            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var titleText = new TextBlock
            {
                Text = "БОЕВЫЕ МАНЁВРЫ",
                FontSize = 25,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            titleText.SetResourceReference(TextBlock.ForegroundProperty, "HeadingBrush");
            header.Children.Add(titleText);
            var helpText = new TextBlock
            {
                Text = "Выберите категории, которые хотите видеть в списке. Нажмите на манёвр, чтобы раскрыть описание.",
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14)
            };
            helpText.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            header.Children.Add(helpText);
            var filters = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (string category in Categories)
            {
                var button = new ToggleButton
                {
                    Content = CombatManeuverService.GetCategoryTitle(category),
                    IsChecked = true,
                    Margin = new Thickness(0, 0, 9, 8),
                    Padding = new Thickness(15, 9, 15, 9),
                    MinHeight = 39,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    BorderThickness = new Thickness(1),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Template = CreateCategoryButtonTemplate()
                };
                button.Checked += CategoryFilter_Changed;
                button.Unchecked += CategoryFilter_Changed;
                _categoryButtons.Add(category, button);
                filters.Children.Add(button);
                ApplyCategoryButtonStyle(button);
            }
            header.Children.Add(filters);

            var collapseAllButton = new Button
            {
                Content = "Свернуть всё",
                Padding = new Thickness(13, 7, 13, 7),
                Margin = new Thickness(0, 1, 0, 0),
                MinHeight = 34,
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold
            };
            collapseAllButton.SetResourceReference(Control.BackgroundProperty, "PanelBackgroundBrush");
            collapseAllButton.SetResourceReference(Control.ForegroundProperty, "TextBrush");
            collapseAllButton.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
            collapseAllButton.Click += (sender, args) =>
            {
                foreach (var collapse in _collapseManeuvers)
                    collapse();
            };
            header.Children.Add(collapseAllButton);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                CanContentScroll = false,
                IsDeferredScrollingEnabled = false
            };
            root.Children.Add(scroll);
            _listPanel = new StackPanel();
            scroll.Content = _listPanel;
            RefreshList();
        }

        private void CategoryFilter_Changed(object sender, RoutedEventArgs e)
        {
            var button = sender as ToggleButton;
            if (button != null) ApplyCategoryButtonStyle(button);
            RefreshList();
        }

        private void ApplyCategoryButtonStyle(ToggleButton button)
        {
            bool selected = button.IsChecked == true;
            button.SetResourceReference(Control.BackgroundProperty, selected ? "CombatCategorySelectedBrush" : "PanelBackgroundBrush");
            button.SetResourceReference(Control.BorderBrushProperty, selected ? "CombatCategorySelectedBorderBrush" : "BorderBrush");
            button.SetResourceReference(Control.ForegroundProperty, selected ? "OnAccentBrush" : "TextBrush");
            button.Opacity = selected ? 1.0 : 0.72;
        }

        private ControlTemplate CreateCategoryButtonTemplate()
        {
            const string templateXaml = @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                 TargetType='{x:Type ToggleButton}'>
  <Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
          BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='4' Padding='{TemplateBinding Padding}'>
    <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'
                      RecognizesAccessKey='True' />
  </Border>
</ControlTemplate>";
            return (ControlTemplate)XamlReader.Parse(templateXaml);
        }

        private void OnThemeChanged()
        {
            SetResourceReference(Window.BackgroundProperty, "BgBrush");
            SetResourceReference(Control.ForegroundProperty, "TextBrush");
            foreach (var button in _categoryButtons.Values) ApplyCategoryButtonStyle(button);
            RefreshList();
        }

        private void RefreshList()
        {
            _listPanel.Children.Clear();
            _collapseManeuvers.Clear();
            foreach (string category in Categories)
            {
                ToggleButton filter;
                if (!_categoryButtons.TryGetValue(category, out filter) || filter.IsChecked != true) continue;
                var entries = _maneuvers.Where(x => string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                if (entries.Count == 0) continue;

                _listPanel.Children.Add(new TextBlock
                {
                    Text = CombatManeuverService.GetCategoryTitle(category).ToUpperInvariant(),
                    FontSize = 17,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brush("HeadingBrush", "#D9C18B"),
                    Margin = new Thickness(3, 15, 3, 9)
                });
                foreach (var maneuver in entries)
                {
                    var card = CreateManeuverCard(maneuver);
                    _listPanel.Children.Add(card);
                }
            }

            if (_listPanel.Children.Count == 0)
                _listPanel.Children.Add(new TextBlock
                {
                    Text = "Все категории отключены. Включите хотя бы одну категорию выше.",
                    FontSize = 15,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(4, 16, 4, 4),
                    Foreground = Brush("MutedBrush", "#A7A7A7")
                });
        }

        private Border CreateManeuverCard(CombatManeuverEntry maneuver)
        {
            var card = new Border
            {
                Margin = new Thickness(0, 0, 0, 7),
                Padding = new Thickness(0),
                Background = Brush("SectionBackgroundBrush", "#22252C"),
                BorderBrush = Brush("BorderBrush", "#444A54"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4)
            };
            var layout = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
            card.Child = layout;
            var headerButton = new Button
            {
                Content = CreateExpanderHeader(maneuver.Name),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(16, 13, 16, 13),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = Brush("TextBrush", "#EEEEEE"),
                Focusable = false,
                Cursor = System.Windows.Input.Cursors.Hand,
                MinHeight = 54,
                IsTabStop = false
            };
            headerButton.Template = CreateManeuverHeaderButtonTemplate();
            layout.Children.Add(headerButton);
            var body = new StackPanel { Margin = new Thickness(16, 10, 16, 14) };
            if (!string.IsNullOrWhiteSpace(maneuver.Description))
                body.Children.Add(new TextBlock
                {
                    Text = maneuver.Description,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 15,
                    LineHeight = 23,
                    Foreground = Brush("TextBrush", "#E0E0E0"),
                    Margin = new Thickness(0, 0, 0, 9)
                });

            if (maneuver.Stats != null && maneuver.Stats.Count > 0)
            {
                var statsPanel = new Border
                {
                    Background = Brush("PanelBackgroundBrush", "#292D35"),
                    BorderBrush = Brush("BorderBrush", "#454B55"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(11),
                    Margin = new Thickness(0, 2, 0, 9)
                };
                var statsContent = new StackPanel();
                statsPanel.Child = statsContent;
                statsContent.Children.Add(new TextBlock
                {
                    Text = "ПАРАМЕТРЫ",
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brush("HeadingBrush", "#D9C18B"),
                    Margin = new Thickness(0, 0, 0, 6)
                });
                AddStat(statsContent, maneuver.Stats, "dice_pool", "Пул проверки");
                AddStat(statsContent, maneuver.Stats, "difficulty", "Сложность");
                AddStat(statsContent, maneuver.Stats, "accuracy", "Точность");
                AddStat(statsContent, maneuver.Stats, "damage", "Урон");
                foreach (var pair in maneuver.Stats.Where(x => !new[] { "dice_pool", "difficulty", "accuracy", "damage" }.Contains(x.Key)))
                    AddStat(statsContent, maneuver.Stats, pair.Key, FormatStatName(pair.Key));
                body.Children.Add(statsPanel);
            }

            if (maneuver.TargetModifiers != null && maneuver.TargetModifiers.Count > 0)
            {
                var table = new StackPanel { Margin = new Thickness(0, 0, 0, 9) };
                table.Children.Add(new TextBlock { Text = "Модификаторы по размеру цели", FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
                foreach (var target in maneuver.TargetModifiers)
                {
                    string size = Get(target, "target_size");
                    string difficulty = Get(target, "difficulty");
                    string damage = Get(target, "damage");
                    table.Children.Add(new TextBlock
                    {
                        Text = size + "  •  Сложность: " + difficulty + "  •  Урон: " + damage,
                        FontSize = 14,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 2, 0, 2),
                        Foreground = Brush("TextBrush", "#D7D7D7")
                    });
                }
                body.Children.Add(table);
            }

            if (!string.IsNullOrWhiteSpace(maneuver.Note))
            {
                body.Children.Add(new Border
                {
                    Background = Brush("NoteBackgroundBrush", "#302B25"),
                    BorderBrush = Brush("HeadingBrush", "#A88C61"),
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 1, 0, 0),
                    Child = new TextBlock
                    {
                        Text = maneuver.Note,
                        FontSize = 14,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brush("TextBrush", "#E5DCCB")
                    }
                });
            }
            var bodyContainer = new Border
            {
                Child = body,
                Visibility = Visibility.Collapsed
            };
            layout.Children.Add(bodyContainer);
            var icon = FindManeuverIcon(headerButton.Content as UIElement);
            bool expanded = false;
            headerButton.Click += (sender, args) =>
            {
                expanded = !expanded;
                bodyContainer.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
                card.BorderBrush = Brush("AccentBrush", "#A83232");
                SetChevronRotation(icon, expanded ? 180 : 0);
            };
            card.MouseEnter += (sender, args) =>
            {
                card.Background = Brush("SelectionBrush", "#3A252A");
                card.BorderBrush = Brush("AccentBrush", "#A83232");
            };
            card.MouseLeave += (sender, args) =>
            {
                card.Background = Brush("SectionBackgroundBrush", "#22252C");
                card.BorderBrush = expanded ? Brush("AccentBrush", "#A83232") : Brush("BorderBrush", "#444A54");
            };
            _collapseManeuvers.Add(() =>
            {
                if (!expanded) return;
                expanded = false;
                bodyContainer.Visibility = Visibility.Collapsed;
                card.BorderBrush = Brush("BorderBrush", "#444A54");
                SetChevronRotation(icon, 0);
            });
            return card;
        }

        private ControlTemplate CreateManeuverHeaderButtonTemplate()
        {
            const string templateXaml = @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                 TargetType='{x:Type Button}'>
  <Border Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}'
          HorizontalAlignment='Stretch' VerticalAlignment='Stretch' IsHitTestVisible='True'>
    <ContentPresenter HorizontalAlignment='Stretch' VerticalAlignment='Stretch'
                      RecognizesAccessKey='True' IsHitTestVisible='False'/>
  </Border>
</ControlTemplate>";
            return (ControlTemplate)XamlReader.Parse(templateXaml);
        }

        private Path FindManeuverIcon(UIElement header)
        {
            var panel = header as DockPanel;
            if (panel == null) return null;
            var border = panel.Children.OfType<Border>().FirstOrDefault();
            return border == null ? null : border.Child as Path;
        }

        private UIElement CreateExpanderHeader(string name)
        {
            var panel = new DockPanel { LastChildFill = true, Background = Brushes.Transparent };
            var iconBorder = new Border
            {
                Width = 25,
                Height = 25,
                CornerRadius = new CornerRadius(13),
                Background = Brush("PanelBackgroundBrush", "#343943"),
                BorderBrush = Brush("BorderBrush", "#626977"),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(10, 0, 1, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var chevron = new Path
            {
                Data = Geometry.Parse("M 2.5,2.5 L 7.5,7.5 L 12.5,2.5"),
                Stroke = Brush("HeadingBrush", "#D9C18B"),
                StrokeThickness = 2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Width = 15,
                Height = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
                Tag = "maneuver-expander-icon"
            };
            chevron.RenderTransform = new RotateTransform(0);
            iconBorder.Child = chevron;

            DockPanel.SetDock(iconBorder, Dock.Right);
            panel.Children.Add(iconBorder);
            panel.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush("TextBrush", "#EEEEEE"),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });
            return panel;
        }

        private static void SetChevronRotation(Path chevron, double angle)
        {
            if (chevron == null) return;
            var transform = chevron.RenderTransform as RotateTransform;
            if (transform == null)
            {
                transform = new RotateTransform(angle);
                chevron.RenderTransform = transform;
                return;
            }

            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = angle,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                }
            };
            transform.BeginAnimation(RotateTransform.AngleProperty, animation);
        }

        private void AddStat(Panel panel, Dictionary<string, string> stats, string key, string title)
        {
            string value;
            if (!stats.TryGetValue(key, out value) || string.IsNullOrWhiteSpace(value)) return;
            var row = new TextBlock { FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) };
            row.Inlines.Add(new System.Windows.Documents.Run(title + ": ") { FontWeight = FontWeights.SemiBold, Foreground = Brush("TextBrush", "#EEEEEE") });
            row.Inlines.Add(new System.Windows.Documents.Run(value) { Foreground = Brush("MutedBrush", "#C3C3C3") });
            panel.Children.Add(row);
        }

        private string FormatStatName(string key)
        {
            switch (key)
            {
                case "target_modifiers": return "Модификаторы целей";
                default: return key.Replace('_', ' ');
            }
        }

        private string Get(Dictionary<string, string> data, string key)
        {
            string value;
            return data != null && data.TryGetValue(key, out value) ? value : string.Empty;
        }

        private Brush Brush(string resourceKey, string fallback)
        {
            try { var value = TryFindResource(resourceKey) as Brush; if (value != null) return value; }
            catch { }
            return (Brush)new BrushConverter().ConvertFromString(fallback);
        }
    }
}