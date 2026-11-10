using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VtM_LoCh_fP.Controls;

namespace VtM_LoCh_fP
{
    internal sealed class AdvancementOption
    {
        public string Category { get; set; }
        public string CostKey { get; set; }
        public string Name { get; set; }
        public int CurrentValue { get; set; }
        public int Maximum { get; set; } = 5;
        public Action<int> Apply { get; set; }
        public Func<bool> CanPurchase { get; set; }
        public string PurchaseRestrictionMessage { get; set; }
        public override string ToString() => Category + " — " + Name + " (" + CurrentValue + ")";
    }

    internal sealed class ExperiencePurchaseWindow : Window
    {
        private readonly CharacterProfile _character;
        private readonly ComboBox _options;
        private readonly TextBlock _info;
        private readonly TextBlock _balanceText;
        private readonly List<AdvancementOption> _items;

        public ExperiencePurchaseWindow(Window owner, CharacterProfile character, IEnumerable<AdvancementOption> options)
        {
            _character = character;
            _items = options.Where(x => x.CurrentValue < x.Maximum).ToList();
            Title = "Покупка за опыт";
            Width = 620; Height = 360; MinWidth = 500; MinHeight = 300;
            Owner = owner; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ThemeManager.AttachWindow(this);

            var root = new DockPanel { Margin = new Thickness(18) };
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var close = HumanityPathEditorWindow.MakeButton("Закрыть", false);
            close.Click += (s, e) => Close();
            var buy = HumanityPathEditorWindow.MakeButton("Купить следующий уровень", true);
            buy.Margin = new Thickness(8, 0, 0, 0);
            buy.Click += Buy_Click;
            footer.Children.Add(close); footer.Children.Add(buy);
            DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);

            var body = new StackPanel();
            _balanceText = new TextBlock { Text = "Доступный опыт: " + character.CreationState.ExperiencePoints, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,10) };
            body.Children.Add(_balanceText);
            _options = new ComboBox { ItemsSource = _items, MinHeight = 34, DisplayMemberPath = null, Margin = new Thickness(0,0,0,12) };
            _options.SelectionChanged += (s, e) => UpdateInfo();
            body.Children.Add(_options);
            _info = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brush("MutedBrush"), Margin = new Thickness(2, 4, 2, 0) };
            body.Children.Add(_info);
            if (_items.Count == 0)
                body.Children.Add(new TextBlock { Text = "Нет показателей, которые можно повысить.", Foreground = Brush("MutedBrush") });
            root.Children.Add(body);
            Content = root;
            if (_items.Count > 0) _options.SelectedIndex = 0;
        }

        private void UpdateInfo()
        {
            _balanceText.Text = "Доступный опыт: " + _character.CreationState.ExperiencePoints;
            var item = _options.SelectedItem as AdvancementOption;
            if (item == null) { _info.Text = string.Empty; return; }
            int next = item.CurrentValue + 1;
            int total = GetExperienceCost(item);
            _info.Text = "Следующий уровень: " + next + ". Стоимость: " + total + " опыта. " +
                (_character.CreationState.ExperiencePoints >= total ? "Опыт достаточен." : "Недостаточно опыта.");
        }

        private int GetExperienceCost(AdvancementOption item)
        {
            bool caitiff = CharacterCreationRules.UsesCaitiffDisciplineRules(_character);
            bool clanDiscipline = false;
            if (item.CostKey == "discipline" && !caitiff)
            {
                try
                {
                    var knownClan = ClanService.GetClans().FirstOrDefault(x =>
                        string.Equals(x.Name, _character.ClanName, StringComparison.OrdinalIgnoreCase));
                    var definition = DisciplineService.GetAll().FirstOrDefault(x =>
                        string.Equals(x.Name, item.Name, StringComparison.OrdinalIgnoreCase));
                    clanDiscipline = knownClan != null && definition != null &&
                        DisciplineService.GetClanDisciplineIds(_character.ClanName).Contains(definition.Id, StringComparer.OrdinalIgnoreCase);
                }
                catch { clanDiscipline = false; }
            }
            return CharacterCreationRules.GetExperienceCostForLevel(item.CostKey, item.CurrentValue, caitiff, clanDiscipline);
        }

        private void Buy_Click(object sender, RoutedEventArgs e)
        {
            var item = _options.SelectedItem as AdvancementOption;
            if (item == null) return;
            int next = item.CurrentValue + 1;
            int cost = GetExperienceCost(item);
            if (_character.CreationState.ExperiencePoints < cost)
            {
                ThemedDialog.Show(this, "Недостаточно опыта.", "Покупка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            bool isFreeNeonate = string.Equals(_character.CreationMode, "Свободный Неонат", StringComparison.OrdinalIgnoreCase);
            if (isFreeNeonate && !AppSettings.ExperiencePurchasesEnabled)
            {
                ThemedDialog.Show(this, "Закуп за опыт отключён в настройках Неоната.", "Покупка", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            int previous = item.CurrentValue;
            item.Apply(next);
            _character.CreationState.ExperiencePoints -= cost;
            if (_character.CreationState.ExperiencePurchases == null)
                _character.CreationState.ExperiencePurchases = new List<ExperiencePurchase>();
            _character.CreationState.ExperiencePurchases.Add(new ExperiencePurchase
            {
                Category = item.CostKey, ItemName = item.Name, PreviousValue = previous, NewValue = next, Cost = cost, Source = "experience"
            });
            AddLedger(item, previous, next, "experience", cost);
            item.CurrentValue = next;
            _options.Items.Refresh();
            UpdateInfo();
            ThemedDialog.Show(this, item.Name + " повышено до " + next + ". Потрачено опыта: " + cost + ".", "Покупка завершена", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddLedger(AdvancementOption item, int previous, int next, string resource, int cost)
        {
            var state = _character.CreationState;
            if (state.PurchaseLedger == null) state.PurchaseLedger = new List<PurchaseTransaction>();
            string block = item.Category == "Характеристика" ? "Характеристики" :
                item.Category == "Способность" ? "Способности" : item.Category == "Добродетель" ? "Добродетели" :
                item.Category.IndexOf("Дисциплина", StringComparison.OrdinalIgnoreCase) >= 0 || item.CostKey == "secondary_path" ? "Дисциплины" :
                item.Category.IndexOf("Фон", StringComparison.OrdinalIgnoreCase) >= 0 ? "Фоны" :
                item.Category == "Сила воли" ? "Сила воли" : "Путь";
            state.PurchaseLedger.Add(new PurchaseTransaction
            {
                Order = state.NextPurchaseOrder++, Block = block, Category = item.CostKey,
                ItemId = item.Name, ItemName = item.Name, PreviousValue = previous, NewValue = next,
                Resource = resource, Cost = cost, AffectsVirtueDerivedValues = item.CostKey == "virtue"
            });
        }

        private static Brush Brush(string key) => Application.Current.Resources[key] as Brush ?? Brushes.Gray;
    }

    internal sealed class FreebiePurchaseJournalWindow : Window
    {
        private readonly CharacterProfile _character;
        private readonly StackPanel _content;
        private readonly Action<string, List<CreationPurchase>> _resetAction;
        private readonly HashSet<string> _confirmedBlocks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public FreebiePurchaseJournalWindow(Window owner, CharacterProfile character, Action<string, List<CreationPurchase>> resetAction = null)
        {
            _character = character;
            _resetAction = resetAction;
            if (_character.CreationState.ConfirmedFreebieBlocks == null) _character.CreationState.ConfirmedFreebieBlocks = new List<string>();
            foreach (var block in _character.CreationState.ConfirmedFreebieBlocks) _confirmedBlocks.Add(block);
            Title = "Закуп за свободные пункты";
            Width = 760; Height = 680; MinWidth = 560; MinHeight = 440;
            Owner = owner; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ThemeManager.AttachWindow(this);
            var root = new DockPanel { Margin = new Thickness(16) };
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var close = HumanityPathEditorWindow.MakeButton("Закрыть", false);
            close.Click += (s, e) => Close();
            footer.Children.Add(close);
            DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
            _content = new StackPanel();
            root.Children.Add(new ScrollViewer { Content = _content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Content = root;
            Render();
        }

        private void Render()
        {
            _content.Children.Clear();
            _content.Children.Add(new TextBlock { Text = "Свободные пункты: " + _character.CreationState.FreebiePoints, FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,12) });
            var purchases = _character.CreationState.FreebiePurchases ?? new List<CreationPurchase>();
            foreach (var group in purchases.GroupBy(x => x.Block).OrderBy(x => x.Key))
            {
                var border = new Border { Padding = new Thickness(12), Margin = new Thickness(0,0,0,10), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6) };
                border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
                border.SetResourceReference(Border.BackgroundProperty, "CardBgBrush");
                var panel = new StackPanel();
                panel.Children.Add(new TextBlock { Text = group.Key, FontSize = 17, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,7) });
                foreach (var purchase in group)
                    panel.Children.Add(new TextBlock { Text = purchase.ItemName + " · " + (purchase.Cost >= 0 ? "-" : "+") + Math.Abs(purchase.Cost) + " свободных пунктов", Margin = new Thickness(2,2,2,2), TextWrapping = TextWrapping.Wrap });
                var reset = HumanityPathEditorWindow.MakeButton("Сбросить закуп и вернуть пункты на " + group.Key, false);
                reset.Margin = new Thickness(0,8,0,0);
                reset.Click += (s, e) => ResetBlock(group.Key);
                panel.Children.Add(reset);
                var confirmBlock = HumanityPathEditorWindow.MakeButton(_confirmedBlocks.Contains(group.Key) ? "Закуп блока подтверждён" : "Подтвердить закуп этого блока", true);
                confirmBlock.Margin = new Thickness(0,6,0,0);
                confirmBlock.IsEnabled = !_confirmedBlocks.Contains(group.Key);
                confirmBlock.Click += (s, e) =>
                {
                    _confirmedBlocks.Add(group.Key);
                    if (!_character.CreationState.ConfirmedFreebieBlocks.Contains(group.Key, StringComparer.OrdinalIgnoreCase))
                        _character.CreationState.ConfirmedFreebieBlocks.Add(group.Key);
                    confirmBlock.Content = "Закуп блока подтверждён";
                    confirmBlock.IsEnabled = false;
                    int blockCount = (_character.CreationState.FreebiePurchases ?? new List<CreationPurchase>()).Select(x => x.Block).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                    if (blockCount > 0 && _confirmedBlocks.Count >= blockCount)
                        _character.CreationState.FreebiePurchasesConfirmed = true;
                };
                panel.Children.Add(confirmBlock);
                border.Child = panel;
                _content.Children.Add(border);
            }
            if (purchases.Count == 0)
                _content.Children.Add(new TextBlock { Text = "Покупок за свободные пункты пока нет.", Foreground = Brush("MutedBrush") });
        }

        private void ResetBlock(string block)
        {
            var purchases = _character.CreationState.FreebiePurchases;
            var selected = purchases.Where(x => string.Equals(x.Block, block, StringComparison.OrdinalIgnoreCase)).ToList();
            if (selected.Count == 0) return;
            if (ThemedDialog.Show(this, "Сбросить все покупки блока «" + block + "»?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (string.Equals(block, "Достоинства и недостатки", StringComparison.OrdinalIgnoreCase))
            {
                var ids = new HashSet<string>(selected.Select(x => x.ItemId), StringComparer.OrdinalIgnoreCase);
                foreach (var owned in _character.MeritsFlaws.Where(x => ids.Contains(x.Id)).ToList())
                    _character.MeritsFlaws.Remove(owned);
                _resetAction?.Invoke(block, selected);
            }
            else _resetAction?.Invoke(block, selected);
            if (_resetAction == null)
                _character.CreationState.FreebiePoints += selected.Sum(x => x.Cost);
            _character.CreationState.FreebiePurchasesConfirmed = false;
            _confirmedBlocks.Remove(block);
            _character.CreationState.ConfirmedFreebieBlocks.RemoveAll(x => string.Equals(x, block, StringComparison.OrdinalIgnoreCase));
            foreach (var purchase in selected) purchases.Remove(purchase);
            Render();
        }

        private static Brush Brush(string key) => Application.Current.Resources[key] as Brush ?? Brushes.Gray;
    }

    internal sealed class FreebiePurchaseWindow : Window
    {
        private readonly CharacterProfile _character;
        private readonly ComboBox _options;
        private readonly TextBlock _info;
        private readonly TextBlock _pointsText;
        private readonly List<AdvancementOption> _items;
        public FreebiePurchaseWindow(Window owner, CharacterProfile character, IEnumerable<AdvancementOption> options)
        {
            _character = character;
            _items = options.Where(x => x.CostKey != "background" && x.CurrentValue < x.Maximum).ToList();
            Title = "Покупка за свободные пункты";
            Width = 620; Height = 360; MinWidth = 500; MinHeight = 300;
            Owner = owner; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ThemeManager.AttachWindow(this);
            var root = new DockPanel { Margin = new Thickness(18) };
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var close = HumanityPathEditorWindow.MakeButton("Закрыть", false);
            close.Click += (s,e) => Close();
            var buy = HumanityPathEditorWindow.MakeButton("Купить следующий уровень", true);
            buy.Margin = new Thickness(8,0,0,0); buy.Click += Buy_Click;
            footer.Children.Add(close); footer.Children.Add(buy);
            DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
            var body = new StackPanel();
            _pointsText = new TextBlock { Text = "Свободные пункты: " + character.CreationState.FreebiePoints, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,10) };
            body.Children.Add(_pointsText);
            _options = new ComboBox { ItemsSource = _items, MinHeight = 34, Margin = new Thickness(0,0,0,12) };
            _options.SelectionChanged += (s,e) => UpdateInfo();
            body.Children.Add(_options);
            _info = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brush("MutedBrush") };
            body.Children.Add(_info);
            if (_items.Count == 0) body.Children.Add(new TextBlock { Text = "Нет доступных для повышения показателей.", Foreground = Brush("MutedBrush") });
            root.Children.Add(body); Content = root;
            if (_items.Count > 0) _options.SelectedIndex = 0;
        }
        private void UpdateInfo()
        {
            _pointsText.Text = "Свободные пункты: " + _character.CreationState.FreebiePoints;
            var item = _options.SelectedItem as AdvancementOption;
            if (item == null) { _info.Text = ""; return; }
            int cost = CharacterCreationRules.GetFreebieCost(item.CostKey);
            _info.Text = "Следующий уровень: " + (item.CurrentValue + 1) + ". Стоимость: " + cost + " свободных пунктов.";
        }
        private void Buy_Click(object sender, RoutedEventArgs e)
        {
            var item = _options.SelectedItem as AdvancementOption;
            if (item == null) return;
            int cost = CharacterCreationRules.GetFreebieCost(item.CostKey);
            if (_character.CreationState.FreebiePoints < cost)
            {
                ThemedDialog.Show(this, "Недостаточно свободных пунктов. Нужно: " + cost + ", доступно: " + _character.CreationState.FreebiePoints + ".", "Покупка недоступна", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (item.CanPurchase != null && !item.CanPurchase())
            {
                ThemedDialog.Show(this, item.PurchaseRestrictionMessage ?? "Для этой покупки требуется дополнительное обоснование.", "Покупка недоступна", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            bool charge = AppSettings.FreebiePurchasesEnabled;
            int previous = item.CurrentValue;
            item.Apply(previous + 1);
            if (charge)
            {
                _character.CreationState.FreebiePoints -= cost;
                if (_character.CreationState.FreebiePurchases == null) _character.CreationState.FreebiePurchases = new List<CreationPurchase>();
                _character.CreationState.FreebiePurchasesConfirmed = false;
                _character.CreationState.FreebiePurchases.Add(new CreationPurchase
                {
                    Block = item.Category == "Характеристика" ? "Характеристики" :
                            item.Category == "Способность" ? "Способности" :
                            item.Category == "Добродетель" ? "Добродетели" :
                            item.Category.IndexOf("Дисциплина", StringComparison.OrdinalIgnoreCase) >= 0 || item.CostKey == "secondary_path" ? "Дисциплины" :
                            item.Category.IndexOf("Фон", StringComparison.OrdinalIgnoreCase) >= 0 ? "Фоны" :
                            item.Category == "Сила воли" ? "Сила воли" : "Путь",
                    ItemId = item.Name, ItemName = item.Name, PreviousValue = previous, NewValue = previous + 1, Cost = cost, Source = "freebie"
                });
                AddLedger(item, previous, previous + 1, "freebie", cost);
            }
            item.CurrentValue = previous + 1;
            _options.Items.Refresh();
            UpdateInfo();
            ThemedDialog.Show(this, item.Name + " повышено до " + item.CurrentValue + ".", "Покупка завершена", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void AddLedger(AdvancementOption item, int previous, int next, string resource, int cost)
        {
            var state = _character.CreationState;
            if (state.PurchaseLedger == null) state.PurchaseLedger = new List<PurchaseTransaction>();
            string block = item.Category == "Характеристика" ? "Характеристики" :
                item.Category == "Способность" ? "Способности" : item.Category == "Добродетель" ? "Добродетели" :
                item.Category.IndexOf("Дисциплина", StringComparison.OrdinalIgnoreCase) >= 0 || item.CostKey == "secondary_path" ? "Дисциплины" :
                item.Category.IndexOf("Фон", StringComparison.OrdinalIgnoreCase) >= 0 ? "Фоны" :
                item.Category == "Сила воли" ? "Сила воли" : "Путь";
            state.PurchaseLedger.Add(new PurchaseTransaction
            {
                Order = state.NextPurchaseOrder++, Block = block, Category = item.CostKey,
                ItemId = item.Name, ItemName = item.Name, PreviousValue = previous, NewValue = next,
                Resource = resource, Cost = cost, AffectsVirtueDerivedValues = item.CostKey == "virtue"
            });
        }

        private static Brush Brush(string key) => Application.Current.Resources[key] as Brush ?? Brushes.Gray;
    }

    internal sealed class InitialPurchaseChecklistWindow : Window
    {
        private readonly CharacterProfile _character;
        private readonly int _disciplineDots;
        private readonly int _backgroundDots;
        private readonly int _virtueDots;
        private readonly bool _allocationComplete;
        private readonly StackPanel _blocks;
        private static readonly string[] BlockKeys = { "attributes", "abilities", "disciplines", "backgrounds", "virtues" };
        private static readonly string[] BlockNames = { "Характеристики", "Способности", "Дисциплины", "Факты биографии", "Добродетели" };

        public InitialPurchaseChecklistWindow(Window owner, CharacterProfile character, int disciplineDots, int backgroundDots, int virtueDots, bool allocationComplete)
        {
            _character = character;
            _allocationComplete = allocationComplete;
            _disciplineDots = disciplineDots;
            _backgroundDots = backgroundDots;
            _virtueDots = virtueDots;
            if (_character.CreationState.ConfirmedInitialBlocks == null)
                _character.CreationState.ConfirmedInitialBlocks = new List<string>();
            Title = "Подтверждение стартовой закупки";
            Width = 620; Height = 650; MinWidth = 520; MinHeight = 480;
            Owner = owner; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ThemeManager.AttachWindow(this);
            var root = new DockPanel { Margin = new Thickness(18) };
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var close = HumanityPathEditorWindow.MakeButton("Закрыть", false);
            close.Click += (s, e) => Close();
            footer.Children.Add(close);
            DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "Начальные значения V20", FontSize = 21, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,12), Foreground = Brush("HeadingBrush") });
            panel.Children.Add(new TextBlock { Text = "Подтверждайте каждый блок отдельно. После подтверждения всех блоков стартовая закупка будет завершена.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,12), Foreground = Brush("MutedBrush") });
            _blocks = new StackPanel();
            panel.Children.Add(_blocks);
            root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Content = root;
            RenderBlocks();
            Closing += (s, e) =>
            {
                if (!_character.CreationState.InitialPurchaseConfirmed)
                    ThemedDialog.Show(this, "Не все блоки стартовой закупки подтверждены. Можно продолжить позже.",
                        "Закупка не завершена", MessageBoxButton.OK, MessageBoxImage.Information);
            };
        }

        private void RenderBlocks()
        {
            _blocks.Children.Clear();
            int[] current = { _allocationComplete ? 1 : 0, _allocationComplete ? 1 : 0, _disciplineDots, _backgroundDots, _virtueDots };
            int[] required = { 1, 1, CharacterCreationRules.GetCreationLimit("disciplines"), CharacterCreationRules.GetCreationLimit("backgrounds"), CharacterCreationRules.GetCreationLimit("virtues") };
            for (int i = 0; i < BlockKeys.Length; i++)
            {
                string key = BlockKeys[i];
                bool confirmed = _character.CreationState.ConfirmedInitialBlocks.Contains(key, StringComparer.OrdinalIgnoreCase);
                bool complete = current[i] == required[i];
                var border = new Border { Padding = new Thickness(12), Margin = new Thickness(0,0,0,10), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6) };
                border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
                border.SetResourceReference(Border.BackgroundProperty, "CardBgBrush");
                var body = new StackPanel();
                body.Children.Add(new TextBlock { Text = BlockNames[i], FontSize = 17, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,5) });
                body.Children.Add(new TextBlock { Text = current[i] + " / " + required[i] + (complete ? "  ✓" : "  — не завершено"), Foreground = complete ? Brush("AccentBrush") : Brush("TextBrush"), Margin = new Thickness(0,0,0,8) });
                var confirm = HumanityPathEditorWindow.MakeButton(confirmed ? "Закуп блока подтверждён" : "Подтвердить закуп: " + BlockNames[i], true);
                confirm.IsEnabled = complete && !confirmed;
                confirm.Click += (s, e) =>
                {
                    if (!_character.CreationState.ConfirmedInitialBlocks.Contains(key, StringComparer.OrdinalIgnoreCase))
                        _character.CreationState.ConfirmedInitialBlocks.Add(key);
                    if (BlockKeys.All(k => _character.CreationState.ConfirmedInitialBlocks.Contains(k, StringComparer.OrdinalIgnoreCase)))
                    {
                        _character.CreationState.InitialPurchaseConfirmed = true;
                        DialogResult = true;
                        return;
                    }
                    RenderBlocks();
                };
                body.Children.Add(confirm);
                border.Child = body;
                _blocks.Children.Add(border);
            }
        }

        private static Brush Brush(string key) => Application.Current.Resources[key] as Brush ?? Brushes.Gray;
    }



    internal sealed class PurchasePriceInfoWindow : Window
    {
        public PurchasePriceInfoWindow(Window owner, bool experience)
        {
            Title = experience ? "Расценки закупа за опыт" : "Расценки свободных пунктов";
            Width = 680;
            Height = experience ? 590 : 470;
            MinWidth = 560;
            MinHeight = 380;
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ThemeManager.AttachWindow(this);

            var root = new DockPanel { Margin = new Thickness(20) };
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var close = HumanityPathEditorWindow.MakeButton("Закрыть", true);
            close.Click += (s, e) => Close();
            footer.Children.Add(close);
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);

            var body = new StackPanel();
            body.Children.Add(new TextBlock
            {
                Text = experience ? "Повышение за опыт" : "Свободные пункты — расценка",
                FontSize = 20, FontWeight = FontWeights.Bold,
                Foreground = Brush("HeadingBrush"), Margin = new Thickness(0, 0, 0, 14)
            });

            var rows = experience
                ? new[] {
                    new[] { "Новая способность", "3" },
                    new[] { "Новая Дисциплина", "10" },
                    new[] { "Новый путь Некромантии или Тауматургии", "7" },
                    new[] { "Характеристика", "Текущее значение × 4" },
                    new[] { "Способность", "Текущее значение × 2" },
                    new[] { "Клановая Дисциплина", "Текущее значение × 5†" },
                    new[] { "Сторонняя Дисциплина", "Текущее значение × 7†" },
                    new[] { "Вторичный путь Некромантии или Тауматургии", "Текущее значение × 4" },
                    new[] { "Добродетель", "Текущее значение × 2" },
                    new[] { "Человечность или Путь Просветления", "Текущее значение × 2" },
                    new[] { "Воля", "Текущее значение × 1" }
                }
                : new[] {
                    new[] { "Характеристика", "5 за пункт" },
                    new[] { "Способность", "2 за пункт" },
                    new[] { "Дисциплина", "7 за пункт (требуется обоснование)" },
                    new[] { "Факт биографии", "1 за пункт" },
                    new[] { "Добродетель", "2 за пункт" },
                    new[] { "Человечность", "2 за пункт" },
                    new[] { "Воля", "1 за пункт" }
                };

            var headers = new[] { "Параметр", "Стоимость" };
            body.Children.Add(EquipmentInfoWindows.Table(headers, rows,
                new[] { 2.0, 3.0 }));

            if (experience)
            {
                body.Children.Add(new TextBlock
                {
                    Text = "† Для Каитифов клановая и сторонняя Дисциплины стоят Текущее значение × 6.\nРазвитие Добродетелей за опыт не влияет на Человечность/Путь и Волю.",
                    TextWrapping = TextWrapping.Wrap, Foreground = Brush("MutedBrush"), Margin = new Thickness(2, 4, 2, 0)
                });
            }
            else
            {
                body.Children.Add(new TextBlock
                {
                    Text = "После завершения создания персонажа факты биографии изменять нельзя.",
                    TextWrapping = TextWrapping.Wrap, Foreground = Brush("MutedBrush"), Margin = new Thickness(2, 4, 2, 0)
                });
            }
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = body };
            root.Children.Add(scroll);
            Content = root;
        }

        private static void AddCell(Grid grid, string value, int row, int column, bool header)
        {
            var border = new Border
            {
                BorderBrush = Brush("BorderBrush"), BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(9, 7, 9, 7),
                Background = header ? Brush("PanelBrush") : Brushes.Transparent
            };
            border.Child = new TextBlock
            {
                Text = value, TextWrapping = TextWrapping.Wrap,
                FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = Brush(header ? "HeadingBrush" : "TextBrush")
            };
            Grid.SetRow(border, row); Grid.SetColumn(border, column); grid.Children.Add(border);
        }

        private static Brush Brush(string key)
        {
            return Application.Current != null && Application.Current.Resources.Contains(key)
                ? Application.Current.Resources[key] as Brush ?? Brushes.Gray : Brushes.Gray;
        }
    }
}
