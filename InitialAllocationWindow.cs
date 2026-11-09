using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VtM_LoCh_fP
{
    internal sealed class InitialAllocationWindow : Window
    {
        private readonly Dictionary<string, ComboBox> _priorities = new Dictionary<string, ComboBox>();
        private readonly Dictionary<string, int> _previousPriorities = new Dictionary<string, int>();
        private bool _changingPriority;
        private bool _updatingDots;
        private readonly Dictionary<string, int> _dotValues = new Dictionary<string, int>();
        private readonly Dictionary<string, StackPanel> _dotPanels = new Dictionary<string, StackPanel>();
        private readonly Dictionary<string, TextBlock> _remainingLabels = new Dictionary<string, TextBlock>();
        private readonly CharacterProfile _character;
        private readonly bool _attributesOnly;
        private readonly bool _lockAttractiveness;

        private static readonly string[] PriorityNames = { "Первичная", "Вторичная", "Третичная" };
        private static readonly string[] AttributeGroups = { "Физические", "Социальные", "Ментальные" };
        private static readonly string[] AbilityGroups = { "Таланты", "Навыки", "Знания" };
        private readonly string[][] _attributeNames;
        private readonly string[][] _abilityNames;
        private static readonly string[] AttributeGroupKeys = { "physical", "social", "mental" };
        private static readonly string[] AbilityGroupKeys = { "talent", "skill", "knowledge" };

        public InitialAllocationWindow(Window owner, CharacterProfile character, bool attributesOnly = false, bool lockAttractiveness = false)
        {
            _character = character;
            _attributesOnly = attributesOnly;
            _lockAttractiveness = lockAttractiveness;
            _attributeNames = LoadNames("attributes", AttributeGroupKeys);
            _abilityNames = LoadNames("abilities", AbilityGroupKeys);
            Title = _attributesOnly ? "Перераспределение характеристик" : "Начальное распределение персонажа";
            Width = 1120; Height = 820; MinWidth = 980; MinHeight = 620;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Owner = owner; ResizeMode = ResizeMode.CanResize; ShowInTaskbar = false;
            ThemeManager.AttachWindow(this);

            var root = new DockPanel { Margin = new Thickness(16) };
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var apply = HumanityPathEditorWindow.MakeButton(_attributesOnly ? "Сохранить перераспределение" : "Завершить распределение", true);
            apply.Click += Apply_Click;
            footer.Children.Add(apply);
            DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var content = new StackPanel();
            scroll.Content = content; root.Children.Add(scroll);
            content.Children.Add(new TextBlock { Text = "ХАРАКТЕРИСТИКИ", FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,8), Foreground = Brush("HeadingBrush") });
            content.Children.Add(new TextBlock { Text = _attributesOnly
                ? "Распределите заново только характеристики. Способности и остальные показатели не изменяются."
                : "У каждого атрибута уже есть 1 точка. Распределите дополнительные 7 / 5 / 3 точки.",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,10), Foreground = Brush("MutedBrush") });
            AddGroup(content, "attributes", AttributeGroups, _attributeNames, true);
            if (!_attributesOnly)
            {
                content.Children.Add(new Border { Height = 1, Background = Brush("BorderBrush"), Margin = new Thickness(0,12,0,12) });
                content.Children.Add(new TextBlock { Text = "СПОСОБНОСТИ", FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,8), Foreground = Brush("HeadingBrush") });
                content.Children.Add(new TextBlock { Text = "Распределите 13 / 9 / 5 точек. В каждой способности можно поставить не более трёх точек.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,10), Foreground = Brush("MutedBrush") });
                AddGroup(content, "abilities", AbilityGroups, _abilityNames, false);
            }
            Content = root;
            Closing += (s, e) =>
            {
                if (!_character.CreationState.InitialAllocationCompleted && DialogResult != true)
                {
                    ThemedDialog.Show(this,
                        "Распределение не завершено. Введённые в этом окне данные не будут сохранены. Кнопка запуска распределения останется на листе.",
                        "Распределение не сохранено", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
        }

        private void AddGroup(Panel parent, string key, string[] groups, string[][] names, bool attributes)
        {
            var grid = new Grid { Margin = new Thickness(0,0,0,8) };
            for (int c=0;c<3;c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i=0;i<3;i++)
            {
                int groupIndex = i;
                var border = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10), Margin = new Thickness(0,0,8,0), BorderBrush = Brush("BorderBrush"), Background = Brush("CardBgBrush") };
                var panel = new StackPanel();
                panel.Children.Add(new TextBlock { Text = groups[i], FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,6), Foreground = Brush("HeadingBrush") });
                int initialPriority = i;
                string savedPriority;
                if (_attributesOnly && attributes &&
                    _character.CreationState.Priorities.TryGetValue("attributes." + groups[i], out savedPriority))
                {
                    int savedIndex = Array.IndexOf(PriorityNames, savedPriority);
                    if (savedIndex >= 0) initialPriority = savedIndex;
                }
                var priority = new ComboBox { ItemsSource = PriorityNames, SelectedIndex = initialPriority, Margin = new Thickness(0,0,0,8), MinHeight = 30 };
                string priorityId = key + "." + i;
                _priorities[priorityId] = priority;
                _previousPriorities[priorityId] = initialPriority;
                priority.SelectionChanged += (s, e) =>
                {
                    if (_changingPriority || priority.SelectedIndex < 0) return;
                    int previous = _previousPriorities[priorityId];
                    int selected = priority.SelectedIndex;
                    var duplicate = _priorities.FirstOrDefault(x => x.Key != priorityId &&
                        x.Key.StartsWith(key + ".", StringComparison.Ordinal) && x.Value.SelectedIndex == selected);
                    if (!string.IsNullOrEmpty(duplicate.Key))
                    {
                        _changingPriority = true;
                        duplicate.Value.SelectedIndex = previous;
                        _previousPriorities[duplicate.Key] = previous;
                        _changingPriority = false;
                    }
                    _previousPriorities[priorityId] = selected;
                    for (int j = 0; j < 3; j++) UpdateRemaining(key, j, names[j]);
                };
                panel.Children.Add(priority);
                int maxDots = attributes
                    ? 5 - CharacterCreationRules.GetCreationLimit("attribute_base")
                    : CharacterCreationRules.GetCreationLimit("ability_creation_max");
                foreach (var name in names[i])
                {
                    string itemKey = key + "." + name;
                    _dotValues[itemKey] = 0;
                    var row = new DockPanel { Margin = new Thickness(0,3,0,3), LastChildFill = false };
                    row.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MinWidth = 145 });
                    var dots = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                    _dotPanels[itemKey] = dots;
                    int visibleDots = attributes ? 5 : maxDots;
                    for (int dot = 1; dot <= visibleDots; dot++)
                    {
                        bool baseDot = attributes && dot == 1;
                        int capturedDot = attributes ? dot - 1 : dot;
                        var pip = new CheckBox
                        {
                            Style = (Style)FindResource("DotPipStyle"),
                            IsChecked = baseDot,
                            IsEnabled = !baseDot,
                            ToolTip = baseDot ? "Начальная точка атрибута" : "Установить " + capturedDot + " дополнительных точек",
                            Tag = capturedDot
                        };
                        if (!baseDot)
                        {
                            if (attributes && _lockAttractiveness && string.Equals(name, "Привлекательность", StringComparison.OrdinalIgnoreCase))
                            {
                                pip.IsEnabled = false;
                                pip.ToolTip = "Для Носферату Привлекательность недоступна.";
                            }
                            else
                            {
                                pip.Checked += (sender, args) => SetDots(itemKey, capturedDot, key, groupIndex, names[groupIndex], attributes);
                                pip.Unchecked += (sender, args) => SetDots(itemKey, capturedDot - 1, key, groupIndex, names[groupIndex], attributes);
                            }
                        }
                        dots.Children.Add(pip);
                    }
                    DockPanel.SetDock(dots, Dock.Right);
                    row.Children.Add(dots);
                    panel.Children.Add(row);
                }
                var remaining = new TextBlock { Margin = new Thickness(2,7,0,2), FontWeight = FontWeights.SemiBold, Foreground = Brush("AccentBrush") };
                _remainingLabels[key + "." + groupIndex] = remaining;
                panel.Children.Add(remaining);
                UpdateRemaining(key, groupIndex, names[groupIndex]);
                border.Child = panel; Grid.SetColumn(border,i); grid.Children.Add(border);
            }
            parent.Children.Add(grid);
        }

        private void SetDots(string itemKey, int value, string key, int groupIndex, string[] names, bool attributes)
        {
            if (_updatingDots) return;
            _updatingDots = true;
            int current = _dotValues.ContainsKey(itemKey) ? _dotValues[itemKey] : 0;
            int pool = CharacterCreationRules.GetPriorityPool(key,
                new[] { "primary", "secondary", "tertiary" }[_priorities[key + "." + groupIndex].SelectedIndex], key == "abilities");
            int usedByOthers = names.Where(n => n != itemKey.Substring(key.Length + 1))
                .Sum(n => _dotValues.ContainsKey(key + "." + n) ? _dotValues[key + "." + n] : 0);
            int maxAffordable = Math.Max(0, pool - usedByOthers);
            value = Math.Max(0, Math.Min(attributes ? 4 : CharacterCreationRules.GetCreationLimit("ability_creation_max"), Math.Min(value, maxAffordable)));
            // Clicking an unaffordable pip fills only the remaining pool; if no point remains, value stays unchanged.
            _dotValues[itemKey] = value;
            var panel = _dotPanels[itemKey];
            for (int i = 0; i < panel.Children.Count; i++)
            {
                var pip = (CheckBox)panel.Children[i];
                bool isBase = attributes && i == 0;
                bool shouldBeChecked = isBase || (attributes ? i <= value : i < value);
                if (pip.IsChecked != shouldBeChecked) pip.IsChecked = shouldBeChecked;
            }
            _updatingDots = false;
            UpdateRemaining(key, groupIndex, names);
        }

        private void UpdateRemaining(string key, int groupIndex, string[] names)
        {
            if (!_remainingLabels.ContainsKey(key + "." + groupIndex)) return;
            string[] priorityKeys = { "primary", "secondary", "tertiary" };
            int pool = CharacterCreationRules.GetPriorityPool(key, priorityKeys[_priorities[key + "." + groupIndex].SelectedIndex], key == "abilities");
            int used = names.Sum(n => _dotValues.ContainsKey(key + "." + n) ? _dotValues[key + "." + n] : 0);
            _remainingLabels[key + "." + groupIndex].Text = "Осталось распределить: " + Math.Max(0, pool - used) + " из " + pool;
            _remainingLabels[key + "." + groupIndex].Foreground = used == pool ? Brush("AccentBrush") : Brush("MutedBrush");
            foreach (var name in names)
            {
                string itemKey = key + "." + name;
                int current = _dotValues.ContainsKey(itemKey) ? _dotValues[itemKey] : 0;
                foreach (CheckBox pip in _dotPanels[itemKey].Children)
                {
                    // Keep pips clickable: SetDots clamps the requested value to the remaining pool.
                    pip.IsEnabled = true;
                }
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            foreach (var key in (_attributesOnly ? new[] { "attributes" } : new[] { "attributes", "abilities" }))
            {
                var chosen = Enumerable.Range(0,3).Select(i => _priorities[key + "." + i].SelectedIndex).ToArray();
                if (chosen.Distinct().Count() != 3)
                {
                    ThemedDialog.Show(this, "Для каждой из трёх категорий выберите отдельный приоритет.", "Приоритеты", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                int[] pools = new int[3];
                string[] priorityKeys = { "primary", "secondary", "tertiary" };
                for (int i=0;i<3;i++)
                    pools[i] = CharacterCreationRules.GetPriorityPool(key, priorityKeys[chosen[i]], key == "abilities");
                string[][] names = key == "attributes" ? _attributeNames : _abilityNames;
                for (int i=0;i<3;i++)
                {
                    int sum = names[i].Sum(name => _dotValues[key + "." + name]);
                    if (sum != pools[i])
                    {
                        string message = sum < pools[i]
                            ? groupsName(key,i) + ": распределено " + sum + " из " + pools[i] + " точек. Распределите оставшиеся " + (pools[i] - sum) + "."
                            : groupsName(key,i) + ": превышен пул: " + sum + " из " + pools[i] + " точек.";
                        ThemedDialog.Show(this, message, "Распределение", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
            }
            var state = _character.CreationState;
            state.AttributeDots.Clear();
            if (!_attributesOnly)
            {
                state.AbilityDots.Clear();
                state.Priorities.Clear();
            }
            for (int i=0;i<3;i++)
            {
                state.Priorities["attributes." + AttributeGroups[i]] = PriorityNames[_priorities["attributes." + i].SelectedIndex];
                if (!_attributesOnly)
                    state.Priorities["abilities." + AbilityGroups[i]] = PriorityNames[_priorities["abilities." + i].SelectedIndex];
                int attributeBase = CharacterCreationRules.GetCreationLimit("attribute_base");
                foreach (var name in _attributeNames[i])
                    state.AttributeDots[name] = attributeBase + _dotValues["attributes." + name];
                if (!_attributesOnly)
                    foreach (var name in _abilityNames[i]) state.AbilityDots[name] = _dotValues["abilities." + name];
            }
            state.InitialAllocationCompleted = true;
            DialogResult = true;
        }

        private static string[][] LoadNames(string dataKey, string[] groupKeys)
        {
            dynamic root = DataRules.Load();
            IList rows = root[dataKey] as IList;
            var result = new string[groupKeys.Length][];
            for (int i = 0; i < groupKeys.Length; i++)
            {
                string group = groupKeys[i];
                result[i] = rows == null ? new string[0] : rows.Cast<object>()
                    .Select(x => x as IDictionary<string, object>)
                    .Where(x => x != null && x.ContainsKey("group") && string.Equals(Convert.ToString(x["group"]), group, StringComparison.OrdinalIgnoreCase))
                    .Select(x => Convert.ToString(x["name"]))
                    .Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            }
            return result;
        }

        private static string groupsName(string key, int index) => (key == "attributes" ? AttributeGroups : AbilityGroups)[index];
        private static Brush Brush(string key) => Application.Current.Resources[key] as Brush ?? Brushes.Gray;
    }
}
