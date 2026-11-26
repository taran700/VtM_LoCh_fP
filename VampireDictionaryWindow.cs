using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Markup;
using Newtonsoft.Json;

namespace VtM_LoCh_fP
{
    internal sealed class VampireDictionaryWindow : Window
    {
        private readonly Dictionary<string, List<Entry>> _data;
        private readonly StackPanel _listPanel;
        private readonly TextBox _search;
        private readonly List<Action> _collapseCards = new List<Action>();
        private readonly Dictionary<string, ToggleButton> _categoryButtons =
            new Dictionary<string, ToggleButton>(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] Categories = { "common", "archaic", "vulgar" };
        private static readonly string[] Titles =
        {
            "Общепринятые выражения", "Архаичные выражения", "Вульгарные выражения"
        };
        private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

        private sealed class Entry
        {
            public string Name { get; set; }
            public string Description { get; set; }
        }

        private sealed class ParsedDescription
        {
            public string First;
            public readonly List<string> Others = new List<string>();
            public readonly List<string> Notes = new List<string>();
        }

        public VampireDictionaryWindow(Window owner)
        {
            Owner = owner;
            ThemeManager.AttachWindow(this);
            ThemeManager.ThemeChanged += OnThemeChanged;
            Closed += (s, e) => ThemeManager.ThemeChanged -= OnThemeChanged;

            Title = "Вампирский словарь";
            Width = 820;
            Height = 760;
            MinWidth = 600;
            MinHeight = 460;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(Window.BackgroundProperty, "BgBrush");
            SetResourceReference(Control.ForegroundProperty, "TextBrush");

            _data = LoadDictionary();

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
                Text = "ВАМПИРСКИЙ СЛОВАРЬ",
                FontSize = 25,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            titleText.SetResourceReference(TextBlock.ForegroundProperty, "HeadingBrush");
            header.Children.Add(titleText);

            var searchLabel = new TextBlock
            {
                Text = "Поиск по терминам:",
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 7)
            };
            searchLabel.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            header.Children.Add(searchLabel);

            _search = new TextBox
            {
                MinHeight = 35,
                Padding = new Thickness(8, 5, 8, 5),
                FontSize = 15,
                Margin = new Thickness(0, 0, 0, 10)
            };
            _search.SetResourceReference(Control.BackgroundProperty, "PanelBackgroundBrush");
            _search.SetResourceReference(Control.ForegroundProperty, "TextBrush");
            _search.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
            _search.TextChanged += (s, e) => RefreshList();
            header.Children.Add(_search);

            var filters = new WrapPanel { Orientation = Orientation.Horizontal };
            for (int i = 0; i < Categories.Length; i++)
            {
                string category = Categories[i];
                var button = new ToggleButton
                {
                    Content = Titles[i],
                    IsChecked = i == 0,
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
            collapseAllButton.Click += (s, e) =>
            {
                foreach (var collapse in _collapseCards.ToList()) collapse();
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

        private Dictionary<string, List<Entry>> LoadDictionary()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "data.json");
            if (!System.IO.File.Exists(path))
                throw new System.IO.FileNotFoundException("Не найден файл Data\\data.json.", path);

            var root = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                System.IO.File.ReadAllText(path));
            if (root == null || !root.ContainsKey("vampire_dictionary"))
                throw new InvalidOperationException("В Data\\data.json отсутствует раздел vampire_dictionary.");

            var loaded = JsonConvert.DeserializeObject<Dictionary<string, List<Entry>>>(
                JsonConvert.SerializeObject(root["vampire_dictionary"]));

            var result = new Dictionary<string, List<Entry>>(StringComparer.OrdinalIgnoreCase);
            foreach (string category in Categories)
            {
                List<Entry> entries;
                result[category] = loaded != null && loaded.TryGetValue(category, out entries) && entries != null
                    ? entries.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name)).ToList()
                    : new List<Entry>();
            }
            return result;
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
            button.SetResourceReference(Control.BackgroundProperty,
                selected ? "CombatCategorySelectedBrush" : "PanelBackgroundBrush");
            button.SetResourceReference(Control.BorderBrushProperty,
                selected ? "CombatCategorySelectedBorderBrush" : "BorderBrush");
            button.SetResourceReference(Control.ForegroundProperty,
                selected ? "OnAccentBrush" : "TextBrush");
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
            _collapseCards.Clear();

            string query = (_search.Text ?? string.Empty).Trim();
            var selectedCategories = Categories.Where(category =>
            {
                ToggleButton button;
                return _categoryButtons.TryGetValue(category, out button) && button.IsChecked == true;
            }).ToList();

            var entries = selectedCategories
                .SelectMany(category => _data[category])
                .Where(entry => string.IsNullOrEmpty(query) ||
                    Normalize(entry.Name).IndexOf(Normalize(query), StringComparison.Ordinal) >= 0)
                .OrderBy(entry => entry.Name, Comparer<string>.Create(
                    (a, b) => RussianCulture.CompareInfo.Compare(a, b, CompareOptions.IgnoreCase)))
                .ToList();

            foreach (var category in selectedCategories)
            {
                var categoryEntries = entries.Where(entry => _data[category].Contains(entry)).ToList();
                if (categoryEntries.Count == 0) continue;

                _listPanel.Children.Add(new TextBlock
                {
                    Text = GetCategoryTitle(category).ToUpper(RussianCulture),
                    FontSize = 17,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brush("HeadingBrush", "#D9C18B"),
                    Margin = new Thickness(3, 15, 3, 9)
                });

                foreach (var entry in categoryEntries)
                    _listPanel.Children.Add(CreateDictionaryCard(entry));
            }

            if (_listPanel.Children.Count == 0)
            {
                string message = selectedCategories.Count == 0
                    ? "Все категории отключены. Включите хотя бы одну категорию выше."
                    : "По этому запросу ничего не найдено.";
                var empty = new TextBlock
                {
                    Text = message,
                    FontSize = 15,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(4, 16, 4, 4),
                    Foreground = Brush("MutedBrush", "#A7A7A7")
                };
                _listPanel.Children.Add(empty);
            }
        }

        private string GetCategoryTitle(string category)
        {
            int index = Array.IndexOf(Categories, category);
            return index >= 0 ? Titles[index] : category;
        }

        private Border CreateDictionaryCard(Entry entry)
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
                Content = CreateExpanderHeader(entry.Name),
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
            headerButton.Template = CreateCardHeaderButtonTemplate();
            layout.Children.Add(headerButton);

            var body = new StackPanel { Margin = new Thickness(16, 10, 16, 14) };
            var referenceStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Normalize(entry.Name) };
            ParsedDescription parsed = ParseDescription(ResolveReferences(entry.Description, referenceStack, 0));

            if (!string.IsNullOrWhiteSpace(parsed.First))
                body.Children.Add(CreateDescriptionText(parsed.First, new Thickness(12, 0, 0, 0), "TextBrush"));

            if (parsed.Others.Count > 0)
            {
                var otherValuesPanel = new Border
                {
                    Background = Brush("PanelBackgroundBrush", "#292D35"),
                    BorderBrush = Brush("BorderBrush", "#454B55"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(11),
                    Margin = new Thickness(0, 2, 0, 9)
                };
                var valuesContent = new StackPanel();
                otherValuesPanel.Child = valuesContent;
                valuesContent.Children.Add(new TextBlock
                {
                    Text = "ДРУГИЕ ВАРИАНТЫ",
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brush("HeadingBrush", "#D9C18B"),
                    Margin = new Thickness(0, 0, 0, 6)
                });
                foreach (string value in parsed.Others)
                    valuesContent.Children.Add(CreateDescriptionText(value, new Thickness(0, 0, 0, 6), "TextBrush"));
                body.Children.Add(otherValuesPanel);
            }

            foreach (string note in parsed.Notes)
                body.Children.Add(CreateDescriptionText(note, new Thickness(0, 1, 0, 0), "MutedBrush"));

            var bodyContainer = new Border
            {
                Child = body,
                Visibility = Visibility.Collapsed
            };
            layout.Children.Add(bodyContainer);

            Path icon = FindCardIcon(headerButton.Content as UIElement);
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
            _collapseCards.Add(() =>
            {
                if (!expanded) return;
                expanded = false;
                bodyContainer.Visibility = Visibility.Collapsed;
                card.BorderBrush = Brush("BorderBrush", "#444A54");
                SetChevronRotation(icon, 0);
            });
            return card;
        }

        private ControlTemplate CreateCardHeaderButtonTemplate()
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
                Tag = "dictionary-expander-icon"
            };
            chevron.RenderTransform = new RotateTransform(0);
            iconBorder.Child = chevron;
            DockPanel.SetDock(iconBorder, Dock.Right);
            panel.Children.Add(iconBorder);
            panel.Children.Add(new TextBlock
            {
                Text = CapitalizeSentences(name),
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush("TextBrush", "#EEEEEE"),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });
            return panel;
        }

        private Path FindCardIcon(UIElement header)
        {
            var panel = header as DockPanel;
            if (panel == null) return null;
            var border = panel.Children.OfType<Border>().FirstOrDefault();
            return border == null ? null : border.Child as Path;
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

        private TextBlock CreateDescriptionText(string text, Thickness margin, string resourceKey)
        {
            var block = new TextBlock
            {
                Text = CapitalizeSentences((text ?? string.Empty).Trim()),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 15,
                LineHeight = 23,
                Margin = margin
            };
            block.SetResourceReference(TextBlock.ForegroundProperty, resourceKey);
            return block;
        }
        private static string CapitalizeSentences(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return Regex.Replace(value, @"(^[^\p{L}]*|[.!?…]\s+)(\p{L})", match =>
                match.Groups[1].Value + match.Groups[2].Value.ToUpper(RussianCulture));
        }

        private ParsedDescription ParseDescription(string raw)
        {
            var result = new ParsedDescription();
            raw = (raw ?? string.Empty).Trim();
            if (raw.Length == 0) return result;

            var noteMatch = Regex.Match(raw,
                @"(?:\r?\n\s*)(\d+\.\s*Прим\.\s*пер\.:.*)$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (noteMatch.Success)
            {
                result.Notes.Add(noteMatch.Groups[1].Value.Trim());
                raw = raw.Substring(0, noteMatch.Index).Trim();
            }

            var numbered = Regex.Matches(raw, @"(?<!\S)([1-9]\d*)\)\s*");
            if (numbered.Count >= 2 && raw.Substring(0, numbered[0].Index).Trim().Length == 0)
            {
                var values = new List<string>();
                for (int i = 0; i < numbered.Count; i++)
                {
                    int start = numbered[i].Index + numbered[i].Length;
                    int end = i + 1 < numbered.Count ? numbered[i + 1].Index : raw.Length;
                    string value = raw.Substring(start, end - start).Trim();
                    if (value.Length > 0) values.Add(value);
                }
                if (values.Count > 0)
                {
                    result.First = values[0];
                    result.Others.AddRange(values.Skip(1));
                    return result;
                }
            }

            result.First = raw;
            return result;
        }

        private string ResolveReferences(string text, HashSet<string> visited, int depth)
        {
            if (string.IsNullOrEmpty(text) || depth >= 12) return text ?? string.Empty;

            return Regex.Replace(text, @"(?i)\bсм\.\s*([^\r\n.;!?]+)", match =>
            {
                string target = match.Groups[1].Value.Trim().Trim('«', '»', '"', '\'', '(', ')', ' ');
                if (target.Length == 0) return match.Value;

                Entry referenced = FindEntry(target);
                if (referenced == null) return match.Value;

                string key = Normalize(referenced.Name);
                if (visited.Contains(key)) return match.Value;

                var nextVisited = new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase) { key };
                ParsedDescription parsed = ParseDescription(referenced.Description);
                var meanings = new List<string>();
                if (!string.IsNullOrWhiteSpace(parsed.First))
                    meanings.Add(ResolveReferences(parsed.First, nextVisited, depth + 1));
                foreach (string other in parsed.Others)
                    meanings.Add(ResolveReferences(other, nextVisited, depth + 1));
                foreach (string note in parsed.Notes) meanings.Add(note);

                return meanings.Count == 0 ? match.Value : string.Join(" ", meanings);
            });
        }

        private Entry FindEntry(string target)
        {
            string normalized = Normalize(target);
            if (normalized.Length == 0) return null;

            var matches = Categories.SelectMany(category => _data[category]);
            return matches.FirstOrDefault(entry => Normalize(entry.Name) == normalized);
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string decomposed = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category == UnicodeCategory.NonSpacingMark ||
                    category == UnicodeCategory.SpacingCombiningMark ||
                    category == UnicodeCategory.EnclosingMark) continue;
                builder.Append(c);
            }
            return builder.ToString().Normalize(NormalizationForm.FormC)
                .Replace('ё', 'е').Replace('Ё', 'Е').ToLowerInvariant();
        }

        private Brush Brush(string resourceKey, string fallback)
        {
            try
            {
                var value = TryFindResource(resourceKey) as Brush;
                if (value != null) return value;
            }
            catch { }
            return (Brush)new BrushConverter().ConvertFromString(fallback);
        }
    }
}
