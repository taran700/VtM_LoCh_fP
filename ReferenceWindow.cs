using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Documents;
using System.Diagnostics;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace VtM_LoCh_fP
{
    /// <summary>
    /// Полнотекстовый справочник по DataBase_Globulus_Maximus.json.
    /// Визуальные ресурсы берутся из активной темы приложения.
    /// </summary>
    public class ReferenceWindow : Window
    {
        private readonly List<JObject> _entries;
        private readonly Dictionary<string, JObject> _entriesById;
        private readonly Dictionary<string, DisciplinePowerPathInfo> _disciplinePowerPaths;
        private readonly TextBox _search = new TextBox();
        private ListBox _results = new ListBox();
        private ListBox _sections;
        private StackPanel _details = new StackPanel();
        private readonly Grid _contentHost = new Grid();
        private string _currentGroup;
        private Window _detailWindow;

        public ReferenceWindow()
        {
            Title = "Справочник — VtM V20";
            // Окно открывается развёрнутым, чтобы справочник сразу использовал
            Width = 900;
            Height = 740;
            MinWidth = 620;
            MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowState = WindowState.Normal;
            ResizeMode = ResizeMode.CanResize;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            SetResourceReference(BackgroundProperty, "BgBrush");
            SetResourceReference(ForegroundProperty, "TextBrush");
            ThemeManager.AttachWindow(this);

            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "DataBase_Globulus_Maximus.json");
            if (!File.Exists(file))
                file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataBase_Globulus_Maximus.json");
            if (!File.Exists(file))
                throw new FileNotFoundException("Не найден файл DataBase_Globulus_Maximus.json", file);

            JArray array = JArray.Parse(File.ReadAllText(file));
            _entries = array.OfType<JObject>().ToList();
            _entriesById = _entries
                .Where(x => !string.IsNullOrWhiteSpace(Value(x, "id", null)))
                .GroupBy(x => Value(x, "id", null), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            string characterDataFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "data.json");
            if (!File.Exists(characterDataFile))
                characterDataFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data.json");
            _disciplinePowerPaths = BuildDisciplinePowerPathMap(_entries, characterDataFile);

            PreviewKeyDown += HandleEscapeKey;
            BuildLayout();
            BuildTree();
        }

        private void HandleEscapeKey(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Escape) return;

            if (!string.IsNullOrWhiteSpace(_currentGroup))
                ShowSections();
            else
                Close();

            e.Handled = true;
        }

        private void BuildLayout()
        {
            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Content = root;

            var header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 180 });
            var heading = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            
            heading.Children.Add(MakeText("Справочник", 21, FontWeights.SemiBold, "HeadingBrush"));
            Grid.SetColumn(heading, 0);
            header.Children.Add(heading);

            _search.Height = 34;
            _search.FontSize = 14;
            _search.Padding = new Thickness(10, 4, 10, 4);
            _search.VerticalContentAlignment = VerticalAlignment.Center;
            _search.ToolTip = "Поиск по всем материалам справочника";
            _search.TextChanged += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_currentGroup)) ShowSections();
                else RefreshResults();
            };
            _search.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_search, 1);
            header.Children.Add(_search);
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            Grid.SetRow(_contentHost, 1);
            root.Children.Add(_contentHost);
            StyleList(_results);
            _results.MouseDoubleClick += Results_MouseDoubleClick;
            ShowSections();
        }

        private TextBlock MakeText(string text, double size, FontWeight weight, string brushKey)
        {
            var block = new TextBlock
            {
                Text = text,
                FontSize = size,
                FontWeight = weight,
                TextWrapping = TextWrapping.Wrap
            };
            block.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
            return block;
        }

        private void StyleList(ListBox list)
        {
            list.BorderThickness = new Thickness(0);
            list.Padding = new Thickness(0);
            list.SetResourceReference(Control.BackgroundProperty, "BgBrush");
            list.SetResourceReference(Control.ForegroundProperty, "TextBrush");
            list.FontSize = 14;
            var template = new DataTemplate(typeof(ReferenceListItem));
            var rowBorder = new FrameworkElementFactory(typeof(Border));
            rowBorder.SetValue(Border.PaddingProperty, new Thickness(12, 9, 12, 9));
            rowBorder.SetValue(Border.MarginProperty, new Thickness(0));
            rowBorder.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 1));
            rowBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            var stack = new FrameworkElementFactory(typeof(StackPanel));
            var titleRow = new FrameworkElementFactory(typeof(DockPanel));
            titleRow.SetValue(DockPanel.LastChildFillProperty, true);
            var name = new FrameworkElementFactory(typeof(TextBlock));
            name.SetBinding(TextBlock.TextProperty, new Binding("DisplayName"));
            name.SetValue(TextBlock.FontSizeProperty, 16.0);
            name.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            name.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            name.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            titleRow.AppendChild(name);
            stack.AppendChild(titleRow);

            rowBorder.AppendChild(stack);
            template.VisualTree = rowBorder;
            list.ItemTemplate = template;

            // Внутри раздела материалы сгруппированы по типу; заголовок группы
            // отображается один раз, а элементы внутри неё идут по алфавиту
            var groupHeader = new DataTemplate();
            var groupHeaderText = new FrameworkElementFactory(typeof(TextBlock));
            groupHeaderText.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            groupHeaderText.SetValue(TextBlock.FontSizeProperty, 16.0);
            groupHeaderText.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            groupHeaderText.SetValue(TextBlock.MarginProperty, new Thickness(12, 22, 12, 9));
            groupHeaderText.SetResourceReference(TextBlock.ForegroundProperty, "HeadingBrush");
            groupHeader.VisualTree = groupHeaderText;
            list.GroupStyle.Clear();
            list.GroupStyle.Add(new GroupStyle { HeaderTemplate = groupHeader });

            var itemStyle = new Style(typeof(ListBoxItem));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            itemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            itemStyle.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
            var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("InputBrush")));
            itemStyle.Triggers.Add(selected);
            list.ItemContainerStyle = itemStyle;
        }

        private void BuildTree()
        {
        }

        private void ShowSections()
        {
            _currentGroup = null;
            _contentHost.Children.Clear();
            _sections = new ListBox();
            _sections.BorderThickness = new Thickness(0);
            _sections.Padding = new Thickness(0);
            _sections.SetResourceReference(Control.BackgroundProperty, "BgBrush");
            _sections.SetResourceReference(Control.ForegroundProperty, "TextBrush");
            _sections.FontSize = 14;
            _sections.SelectionChanged += (s, e) =>
            {
                var section = _sections.SelectedItem as ReferenceSectionItem;
                if (section != null) OpenSection(section.Name);
            };

            var query = (_search.Text ?? string.Empty).Trim();
            var groups = _entries
                .GroupBy(x => Value(x, "type_group_label", "Без раздела"))
                .Select(g => new ReferenceSectionItem(g.Key, g.SelectMany(x => new[] { Value(x, "name", ""), Value(x, "description", "") })))
                .Where(x => query.Length == 0 || x.SearchText.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 || x.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            _sections.ItemsSource = groups;
            _sections.ItemTemplate = CreateSectionTemplate();
            _contentHost.Children.Add(_sections);
        }

        private DataTemplate CreateSectionTemplate()
        {
            var template = new DataTemplate(typeof(ReferenceSectionItem));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.PaddingProperty, new Thickness(12, 10, 12, 10));
            border.SetValue(Border.MarginProperty, new Thickness(0));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 1));
            border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            var row = new FrameworkElementFactory(typeof(DockPanel));
            row.SetValue(DockPanel.LastChildFillProperty, true);
            var name = new FrameworkElementFactory(typeof(TextBlock));
            name.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            name.SetValue(TextBlock.FontSizeProperty, 16.0);
            name.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            name.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            name.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            row.AppendChild(name);
            border.AppendChild(row);
            template.VisualTree = border;
            return template;
        }

        private void OpenSection(string group)
        {
            _currentGroup = group;
            _contentHost.Children.Clear();
            var panel = new DockPanel();
            var top = new Grid { Margin = new Thickness(2, 0, 2, 12) };
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var back = new Button { Content = "← Разделы", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
            back.Click += (s, e) => ShowSections();
            Grid.SetColumn(back, 0);
            top.Children.Add(back);
            var title = MakeText(group, 19, FontWeights.SemiBold, "HeadingBrush");
            title.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(title, 1);
            top.Children.Add(title);
            DockPanel.SetDock(top, Dock.Top);
            panel.Children.Add(top);
            _results = new ListBox();
            StyleList(_results);
            _results.MouseDoubleClick += Results_MouseDoubleClick;
            var listHost = new DockPanel();
            listHost.Children.Add(_results);
            panel.Children.Add(listHost);
            _contentHost.Children.Add(panel);
            RefreshResults();
        }

        private void RefreshResults()
        {
            if (_results == null || string.IsNullOrWhiteSpace(_currentGroup)) return;
            string query = (_search.Text ?? string.Empty).Trim();
            var filtered = _entries.Where(x => Value(x, "type_group_label", "Без раздела") == _currentGroup);
            if (query.Length > 0)
                filtered = filtered.Where(x => SearchableText(x).IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0);

            var items = filtered.Select(x => new ReferenceListItem(x, _entriesById, _disciplinePowerPaths))
                .OrderBy(x => x.TypeLabel, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.GroupSortName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.PathSortOrder)
                .ThenBy(x => x.PathSortName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.LevelSort)
                .ThenBy(x => x.SortName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            var view = new ListCollectionView(items);
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ReferenceListItem.GroupLabel)));
            view.SortDescriptions.Add(new SortDescription(nameof(ReferenceListItem.TypeLabel), ListSortDirection.Ascending));
            view.SortDescriptions.Add(new SortDescription(nameof(ReferenceListItem.GroupSortName), ListSortDirection.Ascending));
            view.SortDescriptions.Add(new SortDescription(nameof(ReferenceListItem.PathSortOrder), ListSortDirection.Ascending));
            view.SortDescriptions.Add(new SortDescription(nameof(ReferenceListItem.PathSortName), ListSortDirection.Ascending));
            view.SortDescriptions.Add(new SortDescription(nameof(ReferenceListItem.LevelSort), ListSortDirection.Ascending));
            view.SortDescriptions.Add(new SortDescription(nameof(ReferenceListItem.SortName), ListSortDirection.Ascending));
            _results.ItemsSource = view;
            _results.SelectedIndex = -1;
        }

        private void Results_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var list = sender as ListBox;
            if (list == null) return;
            var container = ItemsControl.ContainerFromElement(list, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (container == null) return;

            var item = container.DataContext as ReferenceListItem;
            if (item == null) return;

            list.SelectedItem = item;
            OpenEntryWindow(item.Entry);
            e.Handled = true;
        }

        private void OpenEntryWindow(JObject entry)
        {
            if (_detailWindow != null)
            {
                _detailWindow.Close();
                _detailWindow = null;
            }
            _details = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(28, 24, 28, 28) };
            var scroll = new ScrollViewer
            {
                Content = _details,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(0)
            };
            var window = new Window
            {
                Title = Value(entry, "name", "Материал справочника"),
                Width = 900,
                Height = 720,
                MinWidth = 560,
                MinHeight = 420,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = scroll,
                Background = Background,
                Foreground = Foreground
            };
            window.SetResourceReference(Window.BackgroundProperty, "BgBrush");
            window.SetResourceReference(Window.ForegroundProperty, "TextBrush");
            ThemeManager.AttachWindow(window);
            _detailWindow = window;
            var sourceList = _results;
            RenderEntry(entry);
            window.Closed += (s, e) =>
            {
                if (ReferenceEquals(_detailWindow, window)) _detailWindow = null;
                if (sourceList != null) sourceList.SelectedIndex = -1;
            };
            window.Show();
        }

        private void RenderEntry(JObject entry)
        {
            _details.Children.Clear();

            string categoryLabel = Value(entry, "type_label", null);
            if (!string.IsNullOrWhiteSpace(categoryLabel))
            {
                var category = MakeText(categoryLabel, 13, FontWeights.SemiBold, "MutedBrush");
                category.Margin = new Thickness(0, 0, 0, 8);
                _details.Children.Add(category);
            }

            var name = MakeText(DisplayName(entry), 24, FontWeights.SemiBold, "HeadingBrush");
            name.Margin = new Thickness(0, 0, 0, 18);
            name.LineHeight = 30;
            _details.Children.Add(name);

            string description = Value(entry, "description", null);
            if (!string.IsNullOrWhiteSpace(description))
            {
                var descriptionBlock = MakeLinkedText(description, 17, FontWeights.Normal, "TextBrush");
                descriptionBlock.LineHeight = 27;
                descriptionBlock.Margin = new Thickness(0, 0, 0, 20);
                _details.Children.Add(descriptionBlock);
            }

            AddRelatedLinks(entry, "related_ids", "Связанные материалы");
        }

        private static string DisplayName(JObject entry)
        {
            string name = Value(entry, "name", "Без названия");
            string rawLevel = Value(entry, "level", null) ?? Value(entry, "skill_level", null);
            int level;
            if (int.TryParse(rawLevel, out level) && level > 0 && level <= 10)
                return name + "  " + string.Concat(Enumerable.Repeat("●", level));
            return name;
        }

        private void InsertSectionHeading(string text, int index)
        {
            var heading = MakeText(text, 19, FontWeights.Bold, "HeadingBrush");
            heading.Margin = new Thickness(0, 8, 0, 10);
            _details.Children.Insert(index, heading);
        }

        private void AddRelatedLinks(JObject entry, string key, string heading)
        {
            JArray ids = entry[key] as JArray;
            if (ids == null) return;

            var targets = ids.Select(x => x == null ? null : x.ToString())
                .Where(id => !string.IsNullOrWhiteSpace(id) && _entriesById.ContainsKey(id))
                .Select(id => _entriesById[id])
                .OrderBy(x => Value(x, "name", ""), StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            if (targets.Count == 0) return;

            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            panel.Children.Add(MakeText(heading.ToUpperInvariant(), 10, FontWeights.Bold, "MutedBrush"));
            var links = new WrapPanel { Margin = new Thickness(0, 5, 0, 0) };
            foreach (JObject target in targets)
            {
                string id = Value(target, "id", "");
                var link = new Hyperlink(new Run(Value(target, "name", id)))
                {
                    Tag = id,
                    TextDecorations = null
                };
                link.SetResourceReference(TextElement.ForegroundProperty, "AccentBrush");
                link.Click += (s, e) => NavigateToEntry((string)((Hyperlink)s).Tag);
                var text = new TextBlock { Margin = new Thickness(0, 0, 12, 5), FontSize = 14 };
                text.Inlines.Add(link);
                links.Children.Add(text);
            }
            panel.Children.Add(links);
            _details.Children.Add(panel);
        }

        private TextBlock MakeLinkedText(string value, double size, FontWeight weight, string brushKey)
        {
            var block = MakeText("", size, weight, brushKey);
            // Поддержка Markdown-ссылок [текст](адрес), если они появятся в данных
            var regex = new Regex(@"\[([^\]]+)\]\((https?://[^)]+)\)", RegexOptions.IgnoreCase);
            int position = 0;
            foreach (Match match in regex.Matches(value))
            {
                if (match.Index > position)
                    block.Inlines.Add(new Run(value.Substring(position, match.Index - position)));
                var hyperlink = new Hyperlink(new Run(match.Groups[1].Value)) { NavigateUri = new Uri(match.Groups[2].Value) };
                hyperlink.SetResourceReference(TextElement.ForegroundProperty, "AccentBrush");
                hyperlink.RequestNavigate += (sender, args) =>
                {
                    try { Process.Start(new ProcessStartInfo(args.Uri.AbsoluteUri) { UseShellExecute = true }); }
                    catch { }
                    args.Handled = true;
                };
                block.Inlines.Add(hyperlink);
                position = match.Index + match.Length;
            }
            if (position < value.Length)
                block.Inlines.Add(new Run(value.Substring(position)));
            return block;
        }

        private void NavigateToEntry(string id)
        {
            JObject target;
            if (string.IsNullOrWhiteSpace(id) || !_entriesById.TryGetValue(id, out target)) return;
            OpenEntryWindow(target);
        }

        private static bool HasDisplayValue(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return false;
            if (token.Type == JTokenType.String) return !string.IsNullOrWhiteSpace(token.ToString());
            if (token is JArray) return ((JArray)token).Any(HasDisplayValue);
            if (token is JObject) return ((JObject)token).Properties().Any(p => HasDisplayValue(p.Value));
            return true;
        }

        private void AddField(JObject entry, string key, string label)
        {
            JToken token = entry[key];
            if (!HasDisplayValue(token)) return;
            AddFieldValue(label, TokenText(token));
        }

        private void AddArrayField(JObject entry, string key, string label)
        {
            JToken token = entry[key];
            if (!HasDisplayValue(token)) return;
            string value = TokenText(token);
            if (!string.IsNullOrWhiteSpace(value) && value != "—")
                AddFieldValue(label, value);
        }

        private void AddFieldValue(string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "—") return;
            var row = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 13) };
            var caption = MakeText(label.ToUpperInvariant(), 10, FontWeights.Bold, "MutedBrush");
            caption.Margin = new Thickness(0, 0, 0, 3);
            row.Children.Add(caption);
            var content = MakeLinkedText(value, 14, FontWeights.Normal, "TextBrush");
            content.LineHeight = 21;
            row.Children.Add(content);
            _details.Children.Add(row);
        }

        private static string PrettyLabel(string key)
        {
            return string.Join(" ", key.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Length == 0 ? part : char.ToUpper(part[0]) + part.Substring(1)));
        }

        private static string TokenText(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return "—";
            if (token is JArray)
                return string.Join(", ", ((JArray)token).Select(x => x.ToString()));
            if (token is JObject)
                return token.ToString(Newtonsoft.Json.Formatting.Indented);
            return token.ToString();
        }

        private static string Value(JObject obj, string key, string fallback)
        {
            JToken token = obj[key];
            return token == null || token.Type == JTokenType.Null || string.IsNullOrWhiteSpace(token.ToString())
                ? fallback : token.ToString();
        }

        private static string SearchableText(JObject obj)
        {
            return string.Join(" ", obj.Properties().Select(p => p.Value == null ? "" : p.Value.ToString()));
        }

        private Dictionary<string, DisciplinePowerPathInfo> BuildDisciplinePowerPathMap(
            IEnumerable<JObject> referenceEntries, string characterDataFile)
        {
            var result = new Dictionary<string, DisciplinePowerPathInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (JObject entry in referenceEntries)
            {
                string entryId = Value(entry, "id", "");
                string pathName = Value(entry, "path_name", "");
                int pathOrder;
                int pathLevel;
                if (!string.IsNullOrWhiteSpace(entryId) &&
                    !string.IsNullOrWhiteSpace(pathName) &&
                    int.TryParse(Value(entry, "path_order", null), out pathOrder) &&
                    int.TryParse(Value(entry, "path_level", null), out pathLevel))
                {
                    result[entryId] = new DisciplinePowerPathInfo
                    {
                        PathName = pathName,
                        PathOrder = pathOrder,
                        Level = pathLevel > 0 ? pathLevel : int.MaxValue
                    };
                }
            }

            if (string.IsNullOrWhiteSpace(characterDataFile) || !File.Exists(characterDataFile))
                return result;

            JObject characterData = JObject.Parse(File.ReadAllText(characterDataFile));
            JArray disciplines = characterData["disciplines"] as JArray;
            if (disciplines == null) return result;

            var referencePowers = referenceEntries
                .Where(x => string.Equals(Value(x, "type", ""), "discipline_power", StringComparison.OrdinalIgnoreCase))
                .Where(x => string.Equals(Value(x, "parent_id", ""), "nekromantiya", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(Value(x, "parent_id", ""), "taumaturgiya", StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => Value(x, "parent_id", "") + "|" + Value(x, "name", ""), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            foreach (JObject discipline in disciplines.OfType<JObject>())
            {
                string disciplineId = Value(discipline, "id", "");
                string referenceParentId = string.Equals(disciplineId, "necromancy", StringComparison.OrdinalIgnoreCase)
                    ? "nekromantiya"
                    : string.Equals(disciplineId, "thaumaturgy", StringComparison.OrdinalIgnoreCase)
                        ? "taumaturgiya" : null;
                if (referenceParentId == null) continue;

                JArray paths = discipline["paths"] as JArray;
                if (paths == null) continue;
                for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
                {
                    JObject path = paths[pathIndex] as JObject;
                    if (path == null) continue;
                    string pathName = Value(path, "name", "Без названия пути");
                    JArray powers = path["powers"] as JArray;
                    if (powers == null) continue;

                    foreach (JObject power in powers.OfType<JObject>())
                    {
                        string powerName = Value(power, "name", "");
                        JObject referencePower;
                        if (string.IsNullOrWhiteSpace(powerName) ||
                            !referencePowers.TryGetValue(referenceParentId + "|" + powerName, out referencePower))
                            continue;

                        string referencePowerId = Value(referencePower, "id", "");
                        if (string.IsNullOrWhiteSpace(referencePowerId) || result.ContainsKey(referencePowerId)) continue;

                        int pathLevel;
                        int.TryParse(Value(power, "level", null), out pathLevel);
                        result[referencePowerId] = new DisciplinePowerPathInfo
                        {
                            PathName = pathName,
                            PathOrder = pathIndex,
                            Level = pathLevel > 0 ? pathLevel : int.MaxValue
                        };
                    }
                }
            }
            return result;
        }

        private sealed class DisciplinePowerPathInfo
        {
            public string PathName { get; set; }
            public int PathOrder { get; set; }
            public int Level { get; set; }
        }

        private sealed class ReferenceListItem
        {
            public JObject Entry { get; private set; }
            public string DisplayName { get; private set; }
            public string TypeLabel { get; private set; }
            public string GroupLabel { get; private set; }
            public string GroupSortName { get; private set; }
            public string PathSortName { get; private set; }
            public int PathSortOrder { get; private set; }
            public int LevelSort { get; private set; }
            public string SortName { get; private set; }

            public ReferenceListItem(JObject entry, Dictionary<string, JObject> entriesById, Dictionary<string, DisciplinePowerPathInfo> disciplinePowerPaths)
            {
                Entry = entry;
                DisplayName = ReferenceWindow.DisplayName(entry);
                TypeLabel = Value(entry, "type_label", "Прочее");
                SortName = Value(entry, "name", "Без названия");

                if (string.Equals(Value(entry, "type", ""), "discipline_power", StringComparison.OrdinalIgnoreCase))
                {
                    string parentId = Value(entry, "parent_id", null);
                    JObject parent;
                    string parentName = parentId != null && entriesById.TryGetValue(parentId, out parent)
                        ? Value(parent, "name", parentId)
                        : (parentId ?? "Без указанной дисциплины");

                    GroupSortName = parentName;
                    DisciplinePowerPathInfo pathInfo = null;
                    bool hasPath = disciplinePowerPaths != null && disciplinePowerPaths.TryGetValue(Value(entry, "id", ""), out pathInfo);
                    bool isNecroOrThaumOrKoldunic = string.Equals(parentId, "nekromantiya", StringComparison.OrdinalIgnoreCase) ||
                                                   string.Equals(parentId, "taumaturgiya", StringComparison.OrdinalIgnoreCase) ||
                                                   string.Equals(parentId, "kolduinskoe_koldovstvo", StringComparison.OrdinalIgnoreCase);
                    if (isNecroOrThaumOrKoldunic)
                    {
                        PathSortName = hasPath ? pathInfo.PathName : "Без указанного пути";
                        PathSortOrder = hasPath ? pathInfo.PathOrder : int.MaxValue;
                        GroupLabel = TypeLabel + " — " + parentName + " — " + PathSortName;
                    }
                    else
                    {
                        PathSortName = "";
                        PathSortOrder = 0;
                        GroupLabel = TypeLabel + " — " + parentName;
                    }

                    int level;
                    LevelSort = hasPath ? pathInfo.Level :
                        (int.TryParse(Value(entry, "level", null), out level) ? level : int.MaxValue);
                }
                else
                {
                    GroupSortName = SortName;
                    PathSortName = "";
                    PathSortOrder = 0;
                    GroupLabel = TypeLabel;
                    LevelSort = 0;
                }
            }
        }

        private sealed class ReferenceSectionItem
        {
            public string Name { get; private set; }
            public string SearchText { get; private set; }
            public ReferenceSectionItem(string name, IEnumerable<string> searchText)
            {
                Name = name;
                SearchText = string.Join(" ", searchText ?? Enumerable.Empty<string>());
            }
        }
    }
}
