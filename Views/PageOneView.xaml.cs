using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Collections.Generic;
using System.Linq;
using VtM_LoCh_fP.Controls;

namespace VtM_LoCh_fP.Views
{
    public partial class PageOneView : UserControl
    {
        private CharacterProfile _character;
        private bool _lastAllocationErrorIsAttributes = true;
        private ComboBox _clanSelector;
        private TextBox _clanInput;
        private Popup _clanSuggestionsPopup;
        private ListBox _clanSuggestions;
        private bool _settingClanText;
        private string _currentClanId;
        private bool _updatingWeakness;
        private TextBox _natureInput;
        private TextBox _demeanorInput;
        private Button _natureDescriptionButton;
        private Button _demeanorDescriptionButton;
        private Popup _naturePopup;
        private Popup _demeanorPopup;
        private ListBox _natureSuggestions;
        private ListBox _demeanorSuggestions;
        private bool _settingArchetypeText;
        private ComboBox _generationSelector;
        private TextBox _generationInput;
        private TextBox _sireInput;
        private Button _sireDescriptionButton;
        private bool _applyingDerivedGeneration;
        public event Action GenerationChanged;
        public event Action ClanChanged;
        public event Action<string> CharacterNameChanged;
        public event Action InitialPurchaseRequested;
        public event Func<string, string, int, int, bool> AdvancementCheck;
        private readonly Dictionary<string, int> _lastTrackValues = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private const string VentrueTasteHelp =
            "Выбор категории смертных согласуется с Рассказчиком при создании персонажа. " +
            "После начала игры изменить его нельзя. Кровь смертных вне выбранной категории, " +
            "включая кровь животных, не пополняет запас крови. Категория может быть узкой " +
            "или широкой. Кровь Сородичей не подпадает под это ограничение.";

        public PageOneView()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                ApplyGenerationLimits();
                if (_character != null)
                {
                    ApplySavedAllocations();
                    AttachAllocationPersistence();
                    UpdateInitialAllocationButton();
                }
            };
            InitializeInlinePrioritySelectors();
            VentrueTasteInput.ToolTip = CreateVentrueTasteToolTip();
            VentrueTasteLabel.ToolTip = CreateVentrueTasteToolTip();
        }


        private void OpenInitialAllocation_Click(object sender, RoutedEventArgs e)
        {
            if (_character == null) return;
            ConfirmAllocationInline_Click(sender, e);
        }

        public void RefreshPurchaseHeader() => UpdateInitialAllocationButton();

        private void UpdateInitialAllocationButton()
        {
            if (InitialAllocationButton != null) InitialAllocationButton.Visibility = Visibility.Collapsed;
            if (_character == null) return;
            bool initialDone = _character.CreationState.InitialAllocationCompleted;
            if (ConfirmAllocationInlineButton != null) ConfirmAllocationInlineButton.Visibility = initialDone ? Visibility.Collapsed : Visibility.Visible;
            foreach (var combo in GetPriorityCombos())
            {
                combo.Visibility = initialDone ? Visibility.Collapsed : Visibility.Visible;
                combo.IsEnabled = !_character.CreationState.InitialAllocationEverCompleted;
            }
            RefreshInitialBlockControls();
        }


        private void ConfirmInitialPurchase_Click(object sender, RoutedEventArgs e) { }

        private int GetAllocatedTotal(bool attributes)
        {
            var values = attributes ? _character.CreationState.AttributeDots : _character.CreationState.AbilityDots;
            return values == null ? 0 : values.Values.Sum();
        }
        private int GetAllocationPool(bool attributes)
        {
            string key = attributes ? "attributes." : "abilities.";
            string[] groups = attributes ? new[] { "Физические", "Социальные", "Ментальные" } : new[] { "Таланты", "Навыки", "Знания" };
            int total = 0;
            foreach (var group in groups)
            {
                string priority;
                if (_character.CreationState.Priorities.TryGetValue(key + group, out priority))
                {
                    string id = priority == "Первичная" ? "primary" : priority == "Вторичная" ? "secondary" : "tertiary";
                    total += CharacterCreationRules.GetPriorityPool(attributes ? "attributes" : "abilities", id, !attributes);
                }
            }
            return total;
        }
        private void RefreshInitialBlockControls()
        {
            if (_character == null) return;
            bool show = !_character.CreationState.InitialAllocationCompleted;
            if (AttributesAllocationHelp != null) AttributesAllocationHelp.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (AbilitiesAllocationHelp != null) AbilitiesAllocationHelp.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            var remainingLabels = new[] { PhysicalRemainingText, SocialRemainingText, MentalRemainingText,
                TalentsRemainingText, SkillsRemainingText, KnowledgeRemainingText };
            foreach (var label in remainingLabels)
                if (label != null) label.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            var combos = GetPriorityCombos();
            string[] labels = { "Физические", "Социальные", "Ментальные" };
            for (int i = 0; i < 3; i++)
                SetRemainingText(i, labels[i], InitialAttributeGroups[i], combos[i], true);
            string[] abilityLabels = { "Таланты", "Навыки", "Знания" };
            for (int i = 0; i < 3; i++)
                SetRemainingText(i + 3, abilityLabels[i], InitialAbilityGroups[i], combos[i + 3], false);
        }

        private void SetRemainingText(int index, string label, string[] names, ComboBox priority, bool attributes)
        {
            var targets = new[] { PhysicalRemainingText, SocialRemainingText, MentalRemainingText,
                TalentsRemainingText, SkillsRemainingText, KnowledgeRemainingText };
            var target = targets[index];
            if (target == null || _character == null) return;
            var values = attributes
                ? _character.CreationState.AttributeDots
                : _character.CreationState.AbilityDots;
            int baseValue = attributes ? CharacterCreationRules.GetCreationLimit("attribute_base") : 0;
            int spent = 0;
            var initialValues = attributes ? _character.CreationState.InitialAttributeDots : _character.CreationState.InitialAbilityDots;
            foreach (string name in names)
            {
                int value;
                if (_character.CreationState.InitialAllocationEverCompleted && initialValues != null && initialValues.TryGetValue(name, out value))
                    spent += Math.Max(0, value);
                else if (values.TryGetValue(name, out value)) spent += Math.Max(0, value - baseValue);
            }

            int pool = GetInitialGroupPool(priority, attributes);
            int remaining = Math.Max(0, pool - spent);
            target.Text = $"Осталось: {remaining} из {pool}";
        }

        private ComboBox[] GetPriorityCombos() => new[] { PhysicalPriority, SocialPriority, MentalPriority, TalentsPriority, SkillsPriority, KnowledgePriority };

        private void InitializeInlinePrioritySelectors()
        {
            foreach (var combo in GetPriorityCombos())
            {
                combo.ItemsSource = new[] { "Первичная", "Вторичная", "Третичная" };
                combo.SelectionChanged += InlinePriority_SelectionChanged;
            }
            if (_character != null) LoadInlinePriorities();
        }

        private bool _loadingInlinePriorities;

        private void LoadInlinePriorities()
        {
            _loadingInlinePriorities = true;
            if (_character.CreationState.Priorities == null) _character.CreationState.Priorities = new Dictionary<string, string>();
            var combos = GetPriorityCombos();
            string[] keys = { "attributes.Физические", "attributes.Социальные", "attributes.Ментальные", "abilities.Таланты", "abilities.Навыки", "abilities.Знания" };
            for (int i = 0; i < combos.Length; i++)
            {
                string value;
                combos[i].SelectedItem = _character.CreationState.Priorities.TryGetValue(keys[i], out value) ? value : new[] { "Первичная", "Вторичная", "Третичная" }[i % 3];
            }
            _loadingInlinePriorities = false;
            RefreshInitialBlockControls();
        }

        private void InlinePriority_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo == null || _character == null || _loadingInlinePriorities || _character.CreationState.InitialAllocationCompleted || _character.CreationState.InitialAllocationEverCompleted || combo.SelectedIndex < 0) return;
            var all = GetPriorityCombos();
            int index = Array.IndexOf(all, combo);
            int start = index < 3 ? 0 : 3;
            var peers = all.Skip(start).Take(3).Where(c => c != combo).ToArray();
            var duplicate = peers.FirstOrDefault(c => c.SelectedIndex == combo.SelectedIndex);
            if (duplicate != null)
            {
                int replacement = Enumerable.Range(0, 3).FirstOrDefault(v => v != combo.SelectedIndex && !peers.Any(c => c != duplicate && c.SelectedIndex == v));
                duplicate.SelectedIndex = replacement;
            }
            int groupIndex = Array.IndexOf(all, combo);
            ResetInitialGroup(groupIndex);
            SaveInlinePriority(groupIndex);
            RefreshInitialBlockControls();
        }

        private void ResetInitialGroup(int index)
        {
            if (index < 0 || index >= 6) return;
            bool attributes = index < 3;
            string[] names = attributes ? InitialAttributeGroups[index] : InitialAbilityGroups[index - 3];
            int baseValue = attributes ? CharacterCreationRules.GetCreationLimit("attribute_base") : 0;
            foreach (var track in FindTracks(this).Where(t => names.Contains(t.Label)))
            {
                int value = attributes && track.IsForcedZero ? 0 : baseValue;
                track.SetTrackValue(value);
                if (attributes)
                {
                    _character.CreationState.AttributeDots[track.Label] = value;
                    if (_character.CreationState.InitialAttributeDots != null) _character.CreationState.InitialAttributeDots[track.Label] = 0;
                }
                else
                {
                    _character.CreationState.AbilityDots[track.Label] = value;
                    if (_character.CreationState.InitialAbilityDots != null) _character.CreationState.InitialAbilityDots[track.Label] = 0;
                }
                _lastTrackValues[track.Label] = value;
            }
        }

        private void SaveInlinePriority(int index)
        {
            string[] keys = { "attributes.Физические", "attributes.Социальные", "attributes.Ментальные", "abilities.Таланты", "abilities.Навыки", "abilities.Знания" };
            var combo = GetPriorityCombos()[index];
            if (combo.SelectedItem != null)
                _character.CreationState.Priorities[keys[index]] = combo.SelectedItem.ToString();
        }

        private static readonly string[][] InitialAttributeGroups =
        {
            new[] { "Сила", "Ловкость", "Выносливость" },
            new[] { "Обаяние", "Манипуляция", "Привлекательность" },
            new[] { "Восприятие", "Интеллект", "Смекалка" }
        };

        private static readonly string[][] InitialAbilityGroups =
        {
            new[] { "Атлетика", "Бдительность", "Драка", "Запугивание", "Красноречие", "Лидерство", "Уличное чутьё", "Хитрость", "Шестое чувство", "Эмпатия" },
            new[] { "Вождение", "Воровство", "Выживание", "Исполнение", "Обращение с животными", "Ремесло", "Скрытность", "Стрельба", "Фехтование", "Этикет" },
            new[] { "Гуманитарные науки", "Естественные науки", "Законы", "Информатика", "Медицина", "Оккультизм", "Политика", "Расследование", "Финансы", "Электроника" }
        };

        private bool TryGetInitialGroup(TrackControl changedTrack, out string[] groupNames, out ComboBox priority, out bool attributes)
        {
            groupNames = null;
            priority = null;
            attributes = false;
            if (changedTrack == null) return false;

            for (int i = 0; i < InitialAttributeGroups.Length; i++)
            {
                if (InitialAttributeGroups[i].Contains(changedTrack.Label))
                {
                    groupNames = InitialAttributeGroups[i];
                    priority = GetPriorityCombos()[i];
                    attributes = true;
                    return true;
                }
            }
            for (int i = 0; i < InitialAbilityGroups.Length; i++)
            {
                if (InitialAbilityGroups[i].Contains(changedTrack.Label))
                {
                    groupNames = InitialAbilityGroups[i];
                    priority = GetPriorityCombos()[i + 3];
                    return true;
                }
            }
            return false;
        }

        private int GetInitialGroupPool(ComboBox priority, bool attributes)
        {
            if (priority == null || priority.SelectedIndex < 0) return 0;
            string rank = new[] { "primary", "secondary", "tertiary" }[priority.SelectedIndex];
            return CharacterCreationRules.GetPriorityPool(attributes ? "attributes" : "abilities", rank, !attributes);
        }

        private int GetInitialGroupSpent(string[] names, bool attributes, TrackControl except = null)
        {
            int baseValue = attributes ? CharacterCreationRules.GetCreationLimit("attribute_base") : 0;
            if (_character.CreationState.InitialAllocationEverCompleted)
            {
                var initialValues = attributes ? _character.CreationState.InitialAttributeDots : _character.CreationState.InitialAbilityDots;
                return names.Where(name => except == null || !string.Equals(name, except.Label, StringComparison.OrdinalIgnoreCase))
                    .Sum(name => { int value; return initialValues != null && initialValues.TryGetValue(name, out value) ? Math.Max(0, value) : 0; });
            }
            return FindTracks(this).Where(t => t != except && names.Contains(t.Label)).Sum(t => Math.Max(0, t.Value - baseValue));
        }

        private bool ValidateInitialGroupBudgets(out string message)
        {
            var combos = GetPriorityCombos();
            for (int i = 0; i < 6; i++)
            {
                bool attributes = i < 3;
                string[] names = attributes ? InitialAttributeGroups[i] : InitialAbilityGroups[i - 3];
                int baseValue = attributes ? CharacterCreationRules.GetCreationLimit("attribute_base") : 0;
                var initialValues = attributes ? _character.CreationState.InitialAttributeDots : _character.CreationState.InitialAbilityDots;
                int spent = _character.CreationState.InitialAllocationEverCompleted && initialValues != null
                    ? names.Sum(name => { int v; return initialValues.TryGetValue(name, out v) ? Math.Max(0, v) : 0; })
                    : FindTracks(this).Where(t => names.Contains(t.Label)).Sum(t => Math.Max(0, t.Value - baseValue));
                int pool = GetInitialGroupPool(combos[i], attributes);
                if (spent != pool)
                {
                    _lastAllocationErrorIsAttributes = attributes;
                    string groupName = attributes
                        ? new[] { "Физические", "Социальные", "Ментальные" }[i]
                        : new[] { "Таланты", "Навыки", "Знания" }[i - 3];
                    message = spent < pool
                        ? $"В группе «{groupName}» распределено {spent} из {pool} пунктов. Распределите оставшиеся {pool - spent} пунктов."
                        : $"В группе «{groupName}» потрачено {spent} из {pool} пунктов. Уменьшите значения или измените приоритет.";
                    return false;
                }
            }
            message = null;
            return true;
        }

        private void ConfirmAllocationInline_Click(object sender, RoutedEventArgs e)
        {
            if (_character == null) return;
            var combos = GetPriorityCombos();
            if (combos.Select(c => c.SelectedIndex).Distinct().Count() != 3 || combos.Take(3).Select(c => c.SelectedIndex).Distinct().Count() != 3 || combos.Skip(3).Select(c => c.SelectedIndex).Distinct().Count() != 3)
            { ThemedDialog.Show(Window.GetWindow(this), "В каждой тройке выберите первичный, вторичный и третичный приоритеты без повторений.", "Приоритеты", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            string budgetError;
            if (!ValidateInitialGroupBudgets(out budgetError))
            {
                ThemedDialog.Show(Window.GetWindow(this), budgetError, "Недостаточно пунктов", MessageBoxButton.OK, MessageBoxImage.Warning,
                    _lastAllocationErrorIsAttributes ? "Перейти к Характеристикам" : "Перейти к Способностям",
                    () => (_lastAllocationErrorIsAttributes ? (FrameworkElement)AttributesHeader : AbilitiesHeader)?.BringIntoView());
                return;
            }
            if (_character.CreationState.Priorities == null) _character.CreationState.Priorities = new Dictionary<string, string>();
            string[] keys = { "attributes.Физические", "attributes.Социальные", "attributes.Ментальные", "abilities.Таланты", "abilities.Навыки", "abilities.Знания" };
            for (int i = 0; i < combos.Length; i++) _character.CreationState.Priorities[keys[i]] = combos[i].SelectedItem.ToString();
            bool wasInitialAllocationCompleted = _character.CreationState.InitialAllocationEverCompleted;
            _character.CreationState.InitialAllocationCompleted = true;
            _character.CreationState.InitialAllocationEverCompleted = true;
            if (_character.CreationState.InitialAttributeDots == null) _character.CreationState.InitialAttributeDots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (_character.CreationState.InitialAbilityDots == null) _character.CreationState.InitialAbilityDots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (!wasInitialAllocationCompleted)
            {
                _character.CreationState.InitialAttributeDots.Clear();
                _character.CreationState.InitialAbilityDots.Clear();
            }
            if (_character.CreationState.PurchaseLedger == null) _character.CreationState.PurchaseLedger = new List<PurchaseTransaction>();
            // Пересобираем только записи стартового распределения характеристик/способностей
            _character.CreationState.PurchaseLedger.RemoveAll(t => t.Resource == "initial_pool" &&
                (t.Block == "Характеристики" || t.Block == "Способности"));
            if (_character.CreationState.InitialPurchases == null) _character.CreationState.InitialPurchases = new List<CreationPurchase>();
            _character.CreationState.InitialPurchases.RemoveAll(p => p.Block == "Характеристики" || p.Block == "Способности");
            foreach (var track in FindTracks(this))
            {
                if (new[] { "Сила", "Ловкость", "Выносливость", "Обаяние", "Манипуляция", "Привлекательность", "Восприятие", "Интеллект", "Смекалка" }.Contains(track.Label))
                {
                    _character.CreationState.AttributeDots[track.Label] = track.Value;
                    int attributeBase = CharacterCreationRules.GetCreationLimit("attribute_base");
                    if (!wasInitialAllocationCompleted)
                        _character.CreationState.InitialAttributeDots[track.Label] = Math.Max(0, track.Value - attributeBase);
                    int initialAttributeValue = attributeBase + (_character.CreationState.InitialAttributeDots.ContainsKey(track.Label) ? _character.CreationState.InitialAttributeDots[track.Label] : 0);
                    _character.CreationState.InitialPurchases.Add(new CreationPurchase { Block = "Характеристики", ItemId = track.Label, ItemName = track.Label, PreviousValue = attributeBase, NewValue = initialAttributeValue, Cost = 0, Source = "initial" });
                    for (int level = attributeBase; level < initialAttributeValue; level++)
                        RecordInitialAllocationPoint("Характеристики", "attribute", track.Label, level, level + 1);
                }
                else
                {
                    _character.CreationState.AbilityDots[track.Label] = track.Value;
                    if (!wasInitialAllocationCompleted)
                        _character.CreationState.InitialAbilityDots[track.Label] = track.Value;
                    int initialAbilityValue = _character.CreationState.InitialAbilityDots.ContainsKey(track.Label) ? _character.CreationState.InitialAbilityDots[track.Label] : 0;
                    _character.CreationState.InitialPurchases.Add(new CreationPurchase { Block = "Способности", ItemId = track.Label, ItemName = track.Label, PreviousValue = 0, NewValue = initialAbilityValue, Cost = 0, Source = "initial" });
                    for (int level = 0; level < initialAbilityValue; level++)
                        RecordInitialAllocationPoint("Способности", "ability", track.Label, level, level + 1);
                }
            }
            UpdateInitialAllocationButton();
            ApplyGenerationLimits();
            InitialPurchaseRequested?.Invoke();
        }

        private void RecordInitialAllocationPoint(string block, string category, string itemName, int previous, int next)
        {
            var state = _character.CreationState;
            state.PurchaseLedger.Add(new PurchaseTransaction
            {
                Order = state.NextPurchaseOrder++, Block = block, Category = category,
                ItemId = itemName, ItemName = itemName, PreviousValue = previous, NewValue = next,
                Resource = "initial_pool", Cost = 0
            });
        }

        private ToolTip CreateVentrueTasteToolTip()
        {
            var content = new StackPanel { MaxWidth = 410 };
            content.Children.Add(new TextBlock
            {
                Text = "УТОНЧЁННЫЙ ВКУС",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = (Brush)FindResource("HeadingBrush"),
                Margin = new Thickness(0, 0, 0, 7)
            });
            content.Children.Add(new TextBlock
            {
                Text = VentrueTasteHelp,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("TextBrush")
            });
            return new ToolTip
            {
                Content = content,
                Style = (Style)FindResource("PopupToolTipStyle"),
                Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse,
                MaxWidth = 430
            };
        }

        private void ApplyThemeResources(Control control, string backgroundKey = "InputBrush")
        {
            if (control == null) return;
            control.SetResourceReference(Control.ForegroundProperty, "TextBrush");
            control.SetResourceReference(Control.BackgroundProperty, backgroundKey);
            control.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
        }

        public void InitializeCharacter(CharacterProfile character)
        {
            _character = character;
            LoadInlinePriorities();
            var nameField = new FieldControl { Label = "Имя Персонажа", InputText = _character.Name ?? string.Empty };
            nameField.InputTextChanged += (sender, args) =>
            {
                _character.Name = nameField.InputText;
                CharacterNameChanged?.Invoke(_character.Name);
            };
            NameFieldHost.Content = nameField;
            PlayerField.InputText = _character.PlayerName ?? string.Empty;
            ChronicleField.InputText = _character.ChronicleName ?? string.Empty;
            RoleField.InputText = _character.RoleName ?? string.Empty;
            ChroniclePillarsField.InputText = _character.ChroniclePillars ?? string.Empty;
            ChronicleTenetsField.InputText = _character.ChronicleTenets ?? string.Empty;
            HuntingStyleField.InputText = _character.HuntingStyle ?? string.Empty;
            _character.CreationState.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CharacterCreationState.FreebiePoints) || e.PropertyName == nameof(CharacterCreationState.FreebiePurchasesConfirmed))
                    UpdateInitialAllocationButton();
            };
            if (!_character.Generation.HasValue) _character.Generation = 13;
            if (!_character.CreationState.BaseGeneration.HasValue)
                _character.CreationState.BaseGeneration = _character.Generation.Value;
            _character.Generation = Math.Max(4, Math.Min(15, _character.Generation.Value));
            ClanFieldHost.Content = CreateClanSelector();
            GenerationFieldHost.Content = CreateGenerationEditor();
            SireFieldHost.Content = CreateSireEditor();
            NatureFieldHost.Content = CreateArchetypeEditor("Натура", true);
            DemeanorFieldHost.Content = CreateArchetypeEditor("Маска", false);
            if (character.Generation.HasValue)
            {
                var existingRule = GenerationService.Find(character.Generation.Value);
                if (existingRule != null)
                {
                    character.BloodPoolMax = existingRule.BloodPoolMax;
                    character.BloodPerTurn = existingRule.BloodPerTurn;
                    character.TraitMax = existingRule.TraitMax;
                    character.DisciplineMax = existingRule.DisciplineMax;
                }
            }
            ApplyGenerationLimits();
            UpdateInitialAllocationButton();

            if (!string.IsNullOrWhiteSpace(character.ClanName))
            {
                var clan = FindClanByName(character.ClanName);
                bool guided = !IsFreeNeonate();
                if (clan != null && (guided || character.ClanSelectedFromSuggestions))
                    ApplyClan(clan, false);
                else
                {
                    _currentClanId = null;
                    ClearAppliedClanRules();
                }
            }
        }



        private void ProfileField_InputTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_character == null) return;
            var field = sender as FieldControl;
            if (field == null) return;
            switch (Convert.ToString(field.Tag))
            {
                case "Player": _character.PlayerName = field.InputText; break;
                case "Chronicle": _character.ChronicleName = field.InputText; break;
                case "Role": _character.RoleName = field.InputText; break;
                case "Pillars": _character.ChroniclePillars = field.InputText; break;
                case "Tenets": _character.ChronicleTenets = field.InputText; break;
                case "HuntingStyle": _character.HuntingStyle = field.InputText; break;
            }
        }

        public void ApplyGenerationLimits()
        {
            if (_character == null) return;
            int max = AppSettings.GenerationPointIncrease
                ? (_character.TraitMax > 0 ? _character.TraitMax : 5)
                : 5;

            // Во время создания атрибуты ограничены 5, способности — 3
            // После подтверждения стартовой закупки применяется лимит поколения
            int attributeMax = _character.CreationState.InitialAllocationCompleted ? max : Math.Min(5, max);
            PhysicalAttributesTrackList.Count = attributeMax;
            SocialAttributes.Count = attributeMax;
            MentalAttributesTrackList.Count = attributeMax;
            int abilityMax = max;
            if (!_character.CreationState.InitialAllocationCompleted)
                abilityMax = Math.Min(abilityMax, CharacterCreationRules.GetCreationLimit("ability_creation_max"));
            TalentsTrackList.Count = abilityMax;
            SkillsTrackList.Count = abilityMax;
            KnowledgeTrackList.Count = abilityMax;
        }

        public void RefreshSavedAllocations()
        {
            ApplySavedAllocations();
        }

        private void ApplySavedAllocations()
        {
            // Применение сохранённого распределения (в том числе перераспределения характеристик для Носферату) не является прокачкой и не должно запускать списание свободных пунктов или опыта.
            foreach (var track in FindTracks(this))
                track.ValueChanged -= AllocationTrack_ValueChanged;
            foreach (var track in FindTracks(this))
            {
                int value;
                if (_character.CreationState.AttributeDots.TryGetValue(track.Label, out value) ||
                    _character.CreationState.AbilityDots.TryGetValue(track.Label, out value))
                    track.SetTrackValue(value);
                else if (new[] { "Сила", "Ловкость", "Выносливость", "Обаяние", "Манипуляция", "Привлекательность", "Восприятие", "Интеллект", "Смекалка" }.Contains(track.Label))
                    track.SetTrackValue(CharacterCreationRules.GetCreationLimit("attribute_base"));
            }
        }

        private void AttachAllocationPersistence()
        {
            foreach (var track in FindTracks(this))
            {
                track.ValueChanged -= AllocationTrack_ValueChanged;
                track.ValueChanged += AllocationTrack_ValueChanged;
                _lastTrackValues[track.Label] = track.Value;
            }
        }

        private void AllocationTrack_ValueChanged(object sender, EventArgs e)
        {
            var track = sender as TrackControl;
            if (track == null || _character == null) return;
            string[] attrs = { "Сила", "Ловкость", "Выносливость", "Обаяние", "Манипуляция", "Привлекательность", "Восприятие", "Интеллект", "Смекалка" };
            bool isAttribute = attrs.Contains(track.Label);
            int previous = _lastTrackValues.ContainsKey(track.Label) ? _lastTrackValues[track.Label] : track.Value;
            if (track.Value != previous && (_character.CreationState.InitialAllocationCompleted ||
                (_character.CreationState.InitialAllocationEverCompleted && track.Value < previous)))
            {
                bool allowed = AdvancementCheck == null || AdvancementCheck(attrs.Contains(track.Label) ? "attribute" : "ability", track.Label, previous, track.Value);
                if (!allowed)
                {
                    track.SetTrackValue(previous);
                    return;
                }
            }
            if (!_character.CreationState.InitialAllocationCompleted)
            {
                bool nosferatuAppearanceLocked = track.IsForcedZero;
                int minimum = isAttribute && !nosferatuAppearanceLocked ? CharacterCreationRules.GetCreationLimit("attribute_base") : 0;
                if (track.Value < minimum)
                {
                    track.SetTrackValue(minimum);
                }
                string[] groupNames;
                ComboBox priority;
                bool isAttributeGroup;
                if (TryGetInitialGroup(track, out groupNames, out priority, out isAttributeGroup))
                {
                    int baseValue = isAttributeGroup ? CharacterCreationRules.GetCreationLimit("attribute_base") : 0;
                    int pool = GetInitialGroupPool(priority, isAttributeGroup);
                    int alreadySpent = GetInitialGroupSpent(groupNames, isAttributeGroup, track);
                    int remaining = Math.Max(0, pool - alreadySpent);
                    int paidDots = _character.CreationState.InitialAllocationEverCompleted && _character.CreationState.PurchaseLedger != null
                        ? _character.CreationState.PurchaseLedger.Where(t => string.Equals(t.ItemName, track.Label, StringComparison.OrdinalIgnoreCase) && t.Resource != "initial_pool")
                            .Sum(t => Math.Max(0, t.NewValue - t.PreviousValue)) : 0;
                    int allowedValue = baseValue + paidDots + remaining;
                    if (track.Value > allowedValue)
                        track.SetTrackValue(allowedValue);
                }
            }
            if (attrs.Contains(track.Label)) _character.CreationState.AttributeDots[track.Label] = track.Value;
            else _character.CreationState.AbilityDots[track.Label] = track.Value;
            if (_character.CreationState.InitialAllocationEverCompleted && !_character.CreationState.InitialAllocationCompleted)
            {
                int baseValue = attrs.Contains(track.Label) ? CharacterCreationRules.GetCreationLimit("attribute_base") : 0;
                int paidDots = _character.CreationState.PurchaseLedger == null ? 0 : _character.CreationState.PurchaseLedger
                    .Where(t => string.Equals(t.ItemName, track.Label, StringComparison.OrdinalIgnoreCase) && t.Resource != "initial_pool")
                    .Sum(t => Math.Max(0, t.NewValue - t.PreviousValue));
                int initialDots = Math.Max(0, track.Value - baseValue - paidDots);
                if (attrs.Contains(track.Label)) _character.CreationState.InitialAttributeDots[track.Label] = initialDots;
                else _character.CreationState.InitialAbilityDots[track.Label] = initialDots;
            }
            _lastTrackValues[track.Label] = track.Value;
            RefreshInitialBlockControls();
        }

        private static IEnumerable<TrackControl> FindTracks(DependencyObject parent)
        {
            if (parent == null) yield break;
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                var track = child as TrackControl;
                if (track != null) yield return track;
                foreach (var nested in FindTracks(child)) yield return nested;
            }
        }

        private UIElement CreateSireEditor()
        {
            var panel = new StackPanel { Margin = new Thickness(2, 0, 4, 4) };
            panel.Children.Add(new TextBlock { Text = "Сир", Style = (Style)FindResource("FieldLabelStyle") });
            _sireInput = new TextBox
            {
                MinHeight = 32, Padding = new Thickness(6, 3, 6, 3),
                Foreground = (Brush)FindResource("TextBrush"),
                Background = (Brush)FindResource("InputBrush"),
                BorderBrush = (Brush)FindResource("BorderBrush"),
                VerticalContentAlignment = VerticalAlignment.Center,
                Text = _character.SireName ?? string.Empty
            };
            ApplyThemeResources(_sireInput);
            _sireInput.TextChanged += (s, e) =>
            {
                _character.SireName = _sireInput.Text;
                _sireDescriptionButton.Visibility = string.IsNullOrWhiteSpace(_sireInput.Text) ? Visibility.Collapsed : Visibility.Visible;
            };
            panel.Children.Add(_sireInput);
            _sireDescriptionButton = HumanityPathEditorWindow.MakeButton(
                string.IsNullOrWhiteSpace(_character.SireDescription) ? "Добавить описание Сиру" : "Изменить описание Сира", false);
            _sireDescriptionButton.Margin = new Thickness(0, 5, 0, 0);
            _sireDescriptionButton.HorizontalAlignment = HorizontalAlignment.Left;
            _sireDescriptionButton.Visibility = string.IsNullOrWhiteSpace(_character.SireName) ? Visibility.Collapsed : Visibility.Visible;
            _sireDescriptionButton.Click += (s, e) =>
            {
                var editor = HumanityPathEditorWindow.CreateWindow(Window.GetWindow(this), "Описание Сира", 560, 420);
                editor.MinWidth = 420;
                editor.MinHeight = 300;
                var root = new DockPanel { Margin = new Thickness(16) };
                var footer = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
                var counter = new TextBlock
                {
                    Text = "Символов: 0 / 750",
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 0, 2, 8),
                    FontSize = 12,
                    Foreground = (Brush)FindResource("MutedBrush")
                };
                var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                var cancel = HumanityPathEditorWindow.MakeButton("Отмена", false);
                cancel.Click += (a, b) => editor.DialogResult = false;
                var save = HumanityPathEditorWindow.MakeButton("Сохранить", true);
                save.Margin = new Thickness(8, 0, 0, 0);
                save.Click += (a, b) => editor.DialogResult = true;
                buttons.Children.Add(cancel); buttons.Children.Add(save);
                footer.Children.Add(counter);
                footer.Children.Add(buttons);
                DockPanel.SetDock(footer, Dock.Bottom);
                root.Children.Add(footer);
                var currentSireDescription = _character.SireDescription ?? string.Empty;
                if (currentSireDescription.Length > 750)
                    currentSireDescription = currentSireDescription.Substring(0, 750);
                var text = new TextBox { Text = currentSireDescription, MaxLength = 750, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(8), FontSize = 15, VerticalContentAlignment = VerticalAlignment.Top, HorizontalContentAlignment = HorizontalAlignment.Left, TextAlignment = TextAlignment.Left };
                text.SetResourceReference(Control.BackgroundProperty, "InputBrush");
                text.SetResourceReference(Control.ForegroundProperty, "TextBrush");
                text.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
                Action updateCounter = () =>
                {
                    counter.Text = "Символов: " + text.Text.Length + " / 750";
                    counter.Foreground = text.Text.Length >= 750
                        ? (Brush)FindResource("AccentBrush")
                        : (Brush)FindResource("MutedBrush");
                };
                text.TextChanged += (a, b) => updateCounter();
                updateCounter();
                root.Children.Add(text); editor.Content = root;
                if (editor.ShowDialog() == true)
                {
                    _character.SireDescription = text.Text.Length > 750 ? text.Text.Substring(0, 750) : text.Text;
                    _sireDescriptionButton.Content = string.IsNullOrWhiteSpace(_character.SireDescription) ? "Добавить описание Сиру" : "Изменить описание Сира";
                    var tip = new ToolTip { Content = new TextBlock { Text = _character.SireDescription, TextWrapping = TextWrapping.Wrap, MaxWidth = 360 },
                        Style = (Style)FindResource("PopupToolTipStyle") };
                    _sireInput.ToolTip = tip;
                    _sireDescriptionButton.ToolTip = tip;
                }
            };
            panel.Children.Add(_sireDescriptionButton);
            if (!string.IsNullOrWhiteSpace(_character.SireDescription))
            {
                var tip = new ToolTip { Content = new TextBlock { Text = _character.SireDescription, TextWrapping = TextWrapping.Wrap, MaxWidth = 360 },
                    Style = (Style)FindResource("PopupToolTipStyle") };
                _sireInput.ToolTip = tip;
                _sireDescriptionButton.ToolTip = tip;
            }
            return panel;
        }

        private UIElement CreateGenerationEditor()
        {
            var field = new StackPanel { Margin = new Thickness(2, 0, 4, 4) };
            field.Children.Add(new TextBlock { Text = "Поколение", Style = (Style)FindResource("FieldLabelStyle") });
            var rules = GenerationService.GetRules();
            if (IsFreeNeonate())
            {
                _generationInput = new TextBox
                {
                    MinHeight = 32, Padding = new Thickness(6, 3, 6, 3),
                    Foreground = (Brush)FindResource("TextBrush"),
                    Background = (Brush)FindResource("InputBrush"),
                    BorderBrush = (Brush)FindResource("BorderBrush"),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    ToolTip = "Введите целочисленное значение от 4 до 15. Поколение показывает удалённость от Каина: чем меньше число, тем ближе Сородич к нему. Большинство Сородичей — 13-го поколения. Значения округляются до ближайшего доступного поколения."
                };
                ApplyThemeResources(_generationInput);
                _generationInput.Text = _character.Generation.HasValue ? _character.Generation.Value.ToString() : string.Empty;
                _generationInput.TextChanged += (s, e) => UpdateGeneration(_generationInput.Text);
                _generationInput.LostFocus += (s, e) => { if (_character.Generation.HasValue) _generationInput.Text = _character.Generation.Value.ToString(); };
                field.Children.Add(_generationInput);
            }
            else
            {
                _generationSelector = new ComboBox
                {
                    MinHeight = 32, ItemsSource = rules, DisplayMemberPath = "DisplayName",
                    SelectedValuePath = "Generation", IsEditable = false,
                    Foreground = (Brush)FindResource("TextBrush"),
                    Background = (Brush)FindResource("InputBrush"),
                    BorderBrush = (Brush)FindResource("BorderBrush"),
                    Padding = new Thickness(6, 3, 6, 3),
                    ToolTip = "Поколение показывает удалённость от Каина. Большинство Сородичей — 13-го поколения и младше. Значения выше 13 относятся к строке «Тринадцатое и младше»."
                };
                ApplyThemeResources(_generationSelector);
                if (_character.Generation.HasValue) _generationSelector.SelectedValue = _character.Generation.Value;
                _generationSelector.SelectionChanged += (s, e) => UpdateGeneration(_generationSelector.SelectedValue == null ? string.Empty : _generationSelector.SelectedValue.ToString());
                field.Children.Add(_generationSelector);
            }
            return field;
        }

        private void UpdateGeneration(string text)
        {
            double entered;
            var valueText = (text ?? string.Empty).Trim().Replace(',', '.');
            if (!double.TryParse(valueText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out entered))
            {
                _character.Generation = null;
                _character.BloodPoolMax = 10;
                _character.BloodPerTurn = 1;
                _character.TraitMax = 5;
                _character.DisciplineMax = 5;
                ApplyGenerationLimits();
                GenerationChanged?.Invoke();
                return;
            }
            int generation = (int)Math.Round(entered, MidpointRounding.AwayFromZero);
            generation = Math.Max(4, Math.Min(15, generation));
            var rule = GenerationService.Find(generation);
            if (rule == null) return;
            _character.Generation = generation;
            if (!_applyingDerivedGeneration) _character.CreationState.BaseGeneration = generation;
            _character.BloodPoolMax = rule.BloodPoolMax;
            _character.BloodPerTurn = rule.BloodPerTurn;
            _character.TraitMax = rule.TraitMax;
            _character.DisciplineMax = rule.DisciplineMax;
            ApplyGenerationLimits();
            GenerationChanged?.Invoke();
        }

        public void ApplyDerivedGeneration(int generation)
        {
            if (_character == null) return;
            generation = Math.Max(4, Math.Min(15, generation));
            _applyingDerivedGeneration = true;
            if (_generationInput != null) _generationInput.Text = generation.ToString();
            if (_generationSelector != null) _generationSelector.SelectedValue = generation;
            UpdateGeneration(generation.ToString());
            _applyingDerivedGeneration = false;
        }

        private bool IsFreeNeonate()
        {
            return string.Equals(_character?.CreationMode, "Свободный Неонат", StringComparison.OrdinalIgnoreCase);
        }

        private UIElement CreateClanSelector()
        {
            var field = new StackPanel { Margin = new Thickness(2, 0, 4, 4) };
            field.Children.Add(new TextBlock { Text = "Клан", Style = (Style)FindResource("FieldLabelStyle") });

            if (IsFreeNeonate())
            {
                var host = new Grid();
                _clanInput = new TextBox
                {
                    MinHeight = 32,
                    Padding = new Thickness(6, 3, 6, 3),
                    Foreground = (Brush)FindResource("TextBrush"),
                    Background = (Brush)FindResource("InputBrush"),
                    BorderBrush = (Brush)FindResource("BorderBrush"),
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                ApplyThemeResources(_clanInput);
                _clanSuggestions = new ListBox
                {
                    MaxHeight = 230,
                    DisplayMemberPath = "Name",
                    Background = (Brush)FindResource("CardBgBrush"),
                    Foreground = (Brush)FindResource("TextBrush"),
                    BorderBrush = (Brush)FindResource("BorderBrush")
                };
                ApplyThemeResources(_clanSuggestions, "CardBgBrush");
                _clanSuggestions.ItemContainerStyle = new Style(typeof(ListBoxItem));
                _clanSuggestions.ItemContainerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
                _clanSuggestions.ItemContainerStyle.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("CardBgBrush")));
                _clanSuggestions.ItemContainerStyle.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("TextBrush")));
                _clanSuggestions.ItemContainerStyle.Setters.Add(new Setter(ToolTipService.ToolTipProperty, new Binding(".")));
                _clanSuggestions.ItemContainerStyle.Setters.Add(new Setter(ToolTipService.InitialShowDelayProperty, 250));
                _clanSuggestions.ItemContainerStyle.Setters.Add(new Setter(ToolTipService.ShowDurationProperty, 60000));
                var tipStyle = new Style(typeof(ToolTip), (Style)FindResource("PopupToolTipStyle"));
                tipStyle.Setters.Add(new Setter(ContentControl.ContentTemplateProperty, CreateClanTooltipTemplate()));
                _clanSuggestions.Resources.Add(typeof(ToolTip), tipStyle);

                _clanSuggestionsPopup = new Popup
                {
                    PlacementTarget = _clanInput,
                    Placement = PlacementMode.Bottom,
                    StaysOpen = false,
                    AllowsTransparency = true,
                    Child = _clanSuggestions
                };
                BindingOperations.SetBinding(_clanSuggestionsPopup, FrameworkElement.WidthProperty,
                    new Binding("ActualWidth") { Source = _clanInput });
                _clanInput.Text = _character.ClanName ?? string.Empty;
                var initialClan = FindClanByName(_character.ClanName);
                if (initialClan != null && _character.ClanSelectedFromSuggestions)
                    SetClanFieldToolTip(_clanInput, initialClan, CreateClanTooltipTemplate());
                _clanInput.TextChanged += FreeClanInput_TextChanged;
                _clanInput.PreviewKeyDown += FreeClanInput_PreviewKeyDown;
                _clanInput.GotKeyboardFocus += (sender, args) => UpdateFreeClanSuggestions();
                _clanSuggestions.SelectionChanged += FreeClanSuggestions_SelectionChanged;
                _clanSuggestions.MouseLeftButtonUp += FreeClanSuggestions_MouseLeftButtonUp;
                host.Children.Add(_clanInput);
                field.Children.Add(host);
                return field;
            }

            _clanSelector = new ComboBox
            {
                MinHeight = 32,
                ItemsSource = ClanService.GetClans(),
                DisplayMemberPath = "Name",
                SelectedValuePath = "Name",
                Foreground = (Brush)FindResource("TextBrush"),
                Background = (Brush)FindResource("InputBrush"),
                BorderBrush = (Brush)FindResource("BorderBrush"),
                Padding = new Thickness(6, 3, 6, 3)
            };
            ApplyThemeResources(_clanSelector);
            ComboBoxWheelSupport.Enable(_clanSelector);
            var tooltipTemplate = CreateClanTooltipTemplate();
            var tooltipStyle = new Style(typeof(ToolTip), (Style)FindResource("PopupToolTipStyle"));
            tooltipStyle.Setters.Add(new Setter(ContentControl.ContentTemplateProperty, tooltipTemplate));
            _clanSelector.Resources.Add(typeof(ToolTip), tooltipStyle);
            var itemStyle = new Style(typeof(ComboBoxItem), (Style)FindResource(typeof(ComboBoxItem)));
            itemStyle.Setters.Add(new Setter(ToolTipService.ToolTipProperty, new Binding(".")));
            itemStyle.Setters.Add(new Setter(ToolTipService.InitialShowDelayProperty, 250));
            itemStyle.Setters.Add(new Setter(ToolTipService.ShowDurationProperty, 60000));
            _clanSelector.ItemContainerStyle = itemStyle;
            ToolTipService.SetInitialShowDelay(_clanSelector, 250);
            ToolTipService.SetShowDuration(_clanSelector, 60000);
            if (!string.IsNullOrWhiteSpace(_character.ClanName)) _clanSelector.SelectedValue = _character.ClanName;
            SetClanFieldToolTip(_clanSelector, _clanSelector.SelectedItem as ClanInfo, tooltipTemplate);
            _clanSelector.SelectionChanged += (sender, args) =>
            {
                if (args.AddedItems.Count > 0 && args.AddedItems[0] is ClanInfo clan)
                {
                    _character.ClanName = clan.Name;
                    _character.ClanSelectedFromSuggestions = true;
                    SetClanFieldToolTip(_clanSelector, clan, tooltipTemplate);
                    ApplyClan(clan, true);
                }
            };
            field.Children.Add(_clanSelector);
            return field;
        }

        private void FreeClanInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_settingClanText || _character == null) return;
            _character.ClanName = _clanInput.Text;
            _character.ClanSelectedFromSuggestions = false;
            _clanInput.ToolTip = null;
            ClearAppliedClanRules();
            UpdateFreeClanSuggestions();
        }

        private void UpdateFreeClanSuggestions()
        {
            if (_clanInput == null || _clanSuggestions == null || _character == null) return;
            string query = _clanInput.Text.Trim();
            var clans = ClanService.GetClans();
            var matches = string.IsNullOrWhiteSpace(query) ? clans.ToList() : clans.Where(c =>
                c.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (!string.IsNullOrEmpty(c.Nickname) && c.Nickname.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            _clanSuggestions.ItemsSource = matches;
            _clanSuggestionsPopup.IsOpen = matches.Count > 0;
        }

        private void FreeClanInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && _clanSuggestions.Items.Count > 0)
            {
                _clanSuggestions.SelectedIndex = 0;
                _clanSuggestions.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape) _clanSuggestionsPopup.IsOpen = false;
            else if (e.Key == Key.Enter && _clanSuggestions.Items.Count > 0)
            {
                if (_clanSuggestions.SelectedItem == null) _clanSuggestions.SelectedIndex = 0;
                SelectSuggestedClan(_clanSuggestions.SelectedItem as ClanInfo);
                e.Handled = true;
            }
        }

        private void FreeClanSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void FreeClanSuggestions_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            SelectSuggestedClan(_clanSuggestions.SelectedItem as ClanInfo);
        }

        private void SelectSuggestedClan(ClanInfo clan)
        {
            if (clan == null) return;
            _settingClanText = true;
            _clanInput.Text = clan.Name;
            _clanInput.CaretIndex = _clanInput.Text.Length;
            _settingClanText = false;
            _character.ClanName = clan.Name;
            _character.ClanSelectedFromSuggestions = true;
            _clanSuggestionsPopup.IsOpen = false;
            SetClanFieldToolTip(_clanInput, clan, CreateClanTooltipTemplate());
            ApplyClan(clan, true);
            _clanInput.Focus();
        }

        private void ClearAppliedClanRules()
        {
            // Сохраняем ID последнего выбранного клана при ручном редактировании
            // текстового поля: он нужен, чтобы последующий выбор другого клана
            // корректно распознал смену и выполнил сброс.
            _character.OriginalClanWeakness = null;
            _character.ClanWeakness = string.Empty;
            SetWeaknessText(string.Empty);
            SocialAttributes.ForcedZeroName = null;
            if (_character != null) ApplySavedAllocations();
            NosferatuPlasticSurgeryButton.Visibility = Visibility.Collapsed;
            VentrueTastePanel.Visibility = Visibility.Collapsed;
        }

        private void SetClanFieldToolTip(FrameworkElement field, ClanInfo clan, DataTemplate template)
        {
            if (field == null) return;

            if (clan == null)
            {
                field.ToolTip = null;
                return;
            }

            var toolTip = new ToolTip
            {
                Content = clan,
                ContentTemplate = template,
                Style = (Style)FindResource("PopupToolTipStyle"),
                MaxWidth = 410
            };
            field.ToolTip = toolTip;
            ToolTipService.SetInitialShowDelay(field, 250);
            ToolTipService.SetShowDuration(field, 60000);
        }

        private DataTemplate CreateClanTooltipTemplate()
        {
            var tooltipTemplate = new DataTemplate();
            var tooltipPanel = new FrameworkElementFactory(typeof(StackPanel));
            tooltipPanel.SetValue(StackPanel.MaxWidthProperty, 390.0);

            var clanTitle = new FrameworkElementFactory(typeof(TextBlock));
            clanTitle.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            clanTitle.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            clanTitle.SetValue(TextBlock.FontSizeProperty, 15.0);
            clanTitle.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 0, 7));
            tooltipPanel.AppendChild(clanTitle);

            var clanQuote = new FrameworkElementFactory(typeof(TextBlock));
            clanQuote.SetBinding(TextBlock.TextProperty, new Binding("Quote"));
            clanQuote.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            clanQuote.SetValue(TextBlock.FontStyleProperty, FontStyles.Italic);
            clanQuote.SetValue(TextBlock.FontSizeProperty, 13.0);
            clanQuote.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 0, 9));
            tooltipPanel.AppendChild(clanQuote);

            var clanDescription = new FrameworkElementFactory(typeof(TextBlock));
            clanDescription.SetBinding(TextBlock.TextProperty, new Binding("Description"));
            clanDescription.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            clanDescription.SetValue(TextBlock.FontSizeProperty, 13.0);
            tooltipPanel.AppendChild(clanDescription);

            tooltipTemplate.VisualTree = tooltipPanel;
            return tooltipTemplate;
        }

        private UIElement CreateArchetypeEditor(string label, bool isNature)
        {
            var panel = new StackPanel { Margin = new Thickness(2, 0, 4, 4) };
            panel.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("FieldLabelStyle") });
            var input = new TextBox
            {
                MinHeight = 32, Padding = new Thickness(6, 3, 6, 3),
                Foreground = (Brush)FindResource("TextBrush"),
                Background = (Brush)FindResource("InputBrush"),
                BorderBrush = (Brush)FindResource("BorderBrush"),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                TextAlignment = TextAlignment.Left
            };
            ApplyThemeResources(input);
            var list = new ListBox
            {
                MaxHeight = 230, DisplayMemberPath = "Name",
                Background = (Brush)FindResource("CardBgBrush"),
                Foreground = (Brush)FindResource("TextBrush"),
                BorderBrush = (Brush)FindResource("BorderBrush")
            };
            ApplyThemeResources(list, "CardBgBrush");
            list.ItemContainerStyle = new Style(typeof(ListBoxItem));
            list.ItemContainerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
            list.ItemContainerStyle.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("CardBgBrush")));
            list.ItemContainerStyle.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("TextBrush")));
            list.ItemContainerStyle.Setters.Add(new Setter(ToolTipService.ToolTipProperty, new Binding("Description")));
            list.ItemContainerStyle.Setters.Add(new Setter(ToolTipService.InitialShowDelayProperty, 250));
            list.ItemContainerStyle.Setters.Add(new Setter(ToolTipService.ShowDurationProperty, 60000));
            var tipStyle = new Style(typeof(ToolTip), (Style)FindResource("PopupToolTipStyle"));
            var tipTemplate = new DataTemplate();
            var tipScroll = new FrameworkElementFactory(typeof(ScrollViewer));
            tipScroll.SetValue(ScrollViewer.MaxHeightProperty, 360.0);
            tipScroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            tipScroll.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            var tipText = new FrameworkElementFactory(typeof(TextBlock));
            tipText.SetBinding(TextBlock.TextProperty, new Binding());
            tipText.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            tipText.SetValue(TextBlock.MaxWidthProperty, 470.0);
            tipScroll.AppendChild(tipText);
            tipTemplate.VisualTree = tipScroll;
            tipStyle.Setters.Add(new Setter(ContentControl.ContentTemplateProperty, tipTemplate));
            tipStyle.Setters.Add(new Setter(FrameworkElement.MaxWidthProperty, 500.0));
            list.Resources.Add(typeof(ToolTip), tipStyle);
            var popup = new Popup { PlacementTarget = input, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, Child = list };
            BindingOperations.SetBinding(popup, FrameworkElement.WidthProperty, new Binding("ActualWidth") { Source = input });
            var addDescription = new Button
            {
                Content = "Добавить описание", HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 5, 0, 0), Padding = new Thickness(9, 4, 9, 4), Visibility = Visibility.Collapsed
            };
            panel.Children.Add(input);
            panel.Children.Add(addDescription);

            if (isNature)
            {
                _natureInput = input; _natureSuggestions = list; _naturePopup = popup; _natureDescriptionButton = addDescription;
                input.Text = _character.Nature ?? string.Empty;
                if (_character.NatureSelectedFromSuggestions)
                {
                    var selected = ArchetypeService.GetArchetypes().FirstOrDefault(a => a.Name == _character.Nature);
                    if (selected != null) input.ToolTip = CreateArchetypeDescriptionToolTip(selected.Description, selected.Name, selected.RegainWillpowerCondition);
                }
                else if (!string.IsNullOrWhiteSpace(_character.NatureCustomDescription) || !string.IsNullOrWhiteSpace(_character.NatureCustomWillpowerRecovery)) input.ToolTip = CreateArchetypeDescriptionToolTip(_character.NatureCustomDescription, input.Text, _character.NatureCustomWillpowerRecovery);
                UpdateArchetypeDescriptionButton(true);
            }
            else
            {
                _demeanorInput = input; _demeanorSuggestions = list; _demeanorPopup = popup; _demeanorDescriptionButton = addDescription;
                input.Text = _character.Demeanor ?? string.Empty;
                if (_character.DemeanorSelectedFromSuggestions)
                {
                    var selected = ArchetypeService.GetArchetypes().FirstOrDefault(a => a.Name == _character.Demeanor);
                    if (selected != null) input.ToolTip = CreateArchetypeDescriptionToolTip(selected.Description, selected.Name, selected.RegainWillpowerCondition);
                }
                else if (!string.IsNullOrWhiteSpace(_character.DemeanorCustomDescription) || !string.IsNullOrWhiteSpace(_character.DemeanorCustomWillpowerRecovery)) input.ToolTip = CreateArchetypeDescriptionToolTip(_character.DemeanorCustomDescription, input.Text, _character.DemeanorCustomWillpowerRecovery);
                UpdateArchetypeDescriptionButton(false);
            }

            input.TextChanged += (sender, args) =>
            {
                if (_settingArchetypeText || _character == null) return;
                string query = input.Text.Trim();
                input.ToolTip = null;
                var matches = string.IsNullOrWhiteSpace(query)
                    ? ArchetypeService.GetArchetypes().ToList()
                    : ArchetypeService.GetArchetypes().Where(a => a.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                list.ItemsSource = matches;
                popup.IsOpen = matches.Count > 0;
                if (isNature)
                {
                    _character.Nature = input.Text;
                    _character.NatureSelectedFromSuggestions = false;
                    _character.NatureCustomDescription = string.Empty;
                    _character.NatureCustomWillpowerRecovery = string.Empty;
                }
                else
                {
                    _character.Demeanor = input.Text;
                    _character.DemeanorSelectedFromSuggestions = false;
                    _character.DemeanorCustomDescription = string.Empty;
                    _character.DemeanorCustomWillpowerRecovery = string.Empty;
                }
                UpdateArchetypeDescriptionButton(isNature);
            };
            input.GotKeyboardFocus += (sender, args) =>
            {
                if (_character == null) return;
                var current = input.Text.Trim();
                var initialMatches = string.IsNullOrWhiteSpace(current)
                    ? ArchetypeService.GetArchetypes().ToList()
                    : ArchetypeService.GetArchetypes().Where(a => a.Name.IndexOf(current, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                list.ItemsSource = initialMatches;
                popup.IsOpen = initialMatches.Count > 0;
            };
            input.PreviewKeyDown += (sender, args) =>
            {
                if (args.Key == Key.Down && list.Items.Count > 0)
                {
                    list.SelectedIndex = 0; list.Focus(); args.Handled = true;
                }
                else if (args.Key == Key.Escape) popup.IsOpen = false;
                else if (args.Key == Key.Enter && list.Items.Count > 0)
                {
                    if (list.SelectedItem == null) list.SelectedIndex = 0;
                    SelectArchetype(list.SelectedItem as ArchetypeInfo, isNature); args.Handled = true;
                }
            };
            list.MouseLeftButtonUp += (sender, args) => SelectArchetype(list.SelectedItem as ArchetypeInfo, isNature);
            addDescription.Click += (sender, args) => OpenArchetypeDescription(isNature);
            return panel;
        }

        private void SelectArchetype(ArchetypeInfo archetype, bool isNature)
        {
            if (archetype == null) return;
            var input = isNature ? _natureInput : _demeanorInput;
            _settingArchetypeText = true;
            input.Text = archetype.Name;
            input.CaretIndex = input.Text.Length;
            _settingArchetypeText = false;
            input.ToolTip = CreateArchetypeDescriptionToolTip(archetype.Description, isNature ? "Натура" : "Маска", archetype.RegainWillpowerCondition);
            if (isNature)
            {
                _character.Nature = archetype.Name;
                _character.NatureSelectedFromSuggestions = true;
                _character.NatureCustomDescription = archetype.Description;
                _character.NatureCustomWillpowerRecovery = archetype.RegainWillpowerCondition;
                _naturePopup.IsOpen = false;
            }
            else
            {
                _character.Demeanor = archetype.Name;
                _character.DemeanorSelectedFromSuggestions = true;
                _character.DemeanorCustomDescription = archetype.Description;
                _character.DemeanorCustomWillpowerRecovery = archetype.RegainWillpowerCondition;
                _demeanorPopup.IsOpen = false;
            }
            UpdateArchetypeDescriptionButton(isNature);
            input.Focus();
        }

        private void UpdateArchetypeDescriptionButton(bool isNature)
        {
            var input = isNature ? _natureInput : _demeanorInput;
            var button = isNature ? _natureDescriptionButton : _demeanorDescriptionButton;
            if (input == null || button == null || _character == null) return;
            bool selected = isNature ? _character.NatureSelectedFromSuggestions : _character.DemeanorSelectedFromSuggestions;
            button.Visibility = !selected && !string.IsNullOrWhiteSpace(input.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OpenArchetypeDescription(bool isNature)
        {
            var input = isNature ? _natureInput : _demeanorInput;
            string current = isNature ? _character.NatureCustomDescription : _character.DemeanorCustomDescription;
            string currentRecovery = isNature ? _character.NatureCustomWillpowerRecovery : _character.DemeanorCustomWillpowerRecovery;
            var window = new Window
            {
                Title = "Описание — " + (isNature ? "Натура" : "Маска"),
                Width = 500, Height = 440, MinWidth = 440, MinHeight = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this), ResizeMode = ResizeMode.NoResize,
                Background = (Brush)FindResource("BgBrush"), Foreground = (Brush)FindResource("TextBrush")
            };
            var root = new StackPanel { Margin = new Thickness(16) };
            root.Children.Add(new TextBlock { Text = "Собственное описание (не более 500 символов)", Margin = new Thickness(0, 0, 0, 8), FontWeight = FontWeights.SemiBold });
            var editor = new TextBox { Text = current ?? string.Empty, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxLength = 500, MinHeight = 145, HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Top, TextAlignment = TextAlignment.Left };
            var count = new TextBlock { Text = editor.Text.Length + "/500", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 5, 0, 10) };
            editor.TextChanged += (sender, args) => count.Text = editor.Text.Length + "/500";
            root.Children.Add(editor); root.Children.Add(count);
            root.Children.Add(new TextBlock { Text = "Условие восстановления Воли", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 6) });
            var recoveryEditor = new TextBox { Text = currentRecovery ?? string.Empty, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxLength = 1000, MinHeight = 90,
                HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Top, TextAlignment = TextAlignment.Left };
            root.Children.Add(recoveryEditor);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var save = new Button { Content = "Сохранить", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var cancel = new Button { Content = "Отмена", Padding = new Thickness(12, 5, 12, 5), IsCancel = true };
            save.Click += (sender, args) => { window.DialogResult = true; window.Close(); };
            buttons.Children.Add(save); buttons.Children.Add(cancel); root.Children.Add(buttons); window.Content = root;
            if (window.ShowDialog() == true)
            {
                if (isNature)
                {
                    _character.NatureCustomDescription = editor.Text;
                    _character.NatureCustomWillpowerRecovery = recoveryEditor.Text;
                }
                else
                {
                    _character.DemeanorCustomDescription = editor.Text;
                    _character.DemeanorCustomWillpowerRecovery = recoveryEditor.Text;
                }
                input.ToolTip = CreateArchetypeDescriptionToolTip(
                    isNature ? _character.NatureCustomDescription : _character.DemeanorCustomDescription,
                    input.Text,
                    isNature ? _character.NatureCustomWillpowerRecovery : _character.DemeanorCustomWillpowerRecovery);
            }
        }

        private ToolTip CreateArchetypeDescriptionToolTip(string description, string title, string recovery = null)
        {
            var content = new StackPanel { MaxWidth = 390 };
            content.Children.Add(new TextBlock { Text = title.ToUpperInvariant(), FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6), Foreground = (Brush)FindResource("HeadingBrush") });
            content.Children.Add(new TextBlock { Text = description ?? string.Empty, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("TextBrush") });
            if (!string.IsNullOrWhiteSpace(recovery))
            {
                content.Children.Add(new Border { Height = 1, Background = (Brush)FindResource("BorderBrush"), Margin = new Thickness(0, 8, 0, 8) });
                content.Children.Add(new TextBlock { Text = "ВОССТАНОВЛЕНИЕ ВОЛИ", FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("HeadingBrush"), Margin = new Thickness(0, 0, 0, 4) });
                content.Children.Add(new TextBlock { Text = recovery, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("TextBrush") });
            }
            return new ToolTip
            {
                Content = content,
                Style = (Style)FindResource("PopupToolTipStyle"),
                MaxWidth = 410,
                ContentTemplate = null
            };
        }
        private ClanInfo FindClanByName(string name)
        {
            foreach (var clan in ClanService.GetClans())
                if (string.Equals(clan.Name, name, StringComparison.OrdinalIgnoreCase)) return clan;
            return null;
        }

        private void ApplyClan(ClanInfo clan, bool selectionChanged)
        {
            string previousClanId = _currentClanId;
            bool wasNosferatu = string.Equals(previousClanId, "nosferatu", StringComparison.OrdinalIgnoreCase);
            bool isNewClan = !string.Equals(_currentClanId, clan.Id, StringComparison.OrdinalIgnoreCase);
            bool actualClanChange = !string.IsNullOrWhiteSpace(previousClanId) &&
                !string.Equals(previousClanId, clan.Id, StringComparison.OrdinalIgnoreCase);

            // При первом выборе клана тоже проверяем уже распределённые характеристики:
            // игрок мог распределить их до того, как указал клан.
            if (selectionChanged && isNewClan)
            {
                int baseValue = CharacterCreationRules.GetCreationLimit("attribute_base");
                bool investedAttributes = GetCurrentAttributeValues().Any(x => x.Value > baseValue);
                if (investedAttributes)
                {
                    var resetResult = ThemedDialog.Show(
                        Window.GetWindow(this),
                        "Сбросить прокачанные Характеристики до базового значения 1? Потраченные на них ресурсы будут возвращены.",
                        "Выбор клана", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (resetResult == MessageBoxResult.Yes)
                        ResetAttributesForClanChange();
                }
            }

            if (actualClanChange)
            {
                // Дисциплины сбрасываются только при фактической смене уже выбранного клана.
                ResetDisciplinesForClanChange(previousClanId);
            }

            _currentClanId = clan.Id;

            if (isNewClan || _character.OriginalClanWeakness == null)
            {
                _character.OriginalClanWeakness = clan.WeaknessDescription ?? string.Empty;
                _character.ClanWeakness = _character.OriginalClanWeakness;
                SetWeaknessText(_character.ClanWeakness);
            }
            else
            {
                SetWeaknessText(_character.ClanWeakness ?? _character.OriginalClanWeakness);
            }

            bool isNosferatu = string.Equals(clan.Id, "nosferatu", StringComparison.OrdinalIgnoreCase);
            bool plasticSurgeryDone = isNosferatu && _character.NosferatuPlasticSurgery;
            string forcedZeroAttribute = isNosferatu && !plasticSurgeryDone ? "Привлекательность" : null;
            // При смене клана восстанавливаем сохранённые точки, а затем применяем
            // ограничение Носферату. Так при уходе из клана точки не теряются.
            SocialAttributes.ForcedZeroName = null;
            ApplySavedAllocations();
            // При уходе из Носферату Привлекательность возвращается к базовой
            // точке 1, даже если в сохранённом распределении для неё было 0.
            if (selectionChanged && wasNosferatu && !isNosferatu)
            {
                int baseValue = CharacterCreationRules.GetCreationLimit("attribute_base");
                _character.CreationState.AttributeDots["Привлекательность"] = baseValue;
                var attractivenessTrack = FindTracks(this).FirstOrDefault(t => t.Label == "Привлекательность");
                if (attractivenessTrack != null) attractivenessTrack.SetTrackValue(baseValue);
                _lastTrackValues["Привлекательность"] = baseValue;
            }
            AttachAllocationPersistence();
            SocialAttributes.ForcedZeroName = forcedZeroAttribute;
            if (forcedZeroAttribute != null)
            {
                _character.CreationState.AttributeDots[forcedZeroAttribute] = 0;
                var forcedTrack = FindTracks(this).FirstOrDefault(t => t.Label == forcedZeroAttribute);
                if (forcedTrack != null) forcedTrack.SetTrackValue(0);
                _lastTrackValues[forcedZeroAttribute] = 0;
            }
            NosferatuPlasticSurgeryButton.Visibility = isNosferatu
                ? Visibility.Visible : Visibility.Collapsed;
            NosferatuPlasticSurgeryButton.Content = plasticSurgeryDone ? "Изуродовать себя" : "Пластическая хирургия";

            bool isVentrue = string.Equals(clan.Id, "ventrue", StringComparison.OrdinalIgnoreCase);
            VentrueTastePanel.Visibility = isVentrue ? Visibility.Visible : Visibility.Collapsed;
            VentrueTasteInput.Text = _character.VentruePreyCategory ?? string.Empty;
            VentrueTasteInput.IsReadOnly = _character.VentruePreyCategoryLocked;
            VentrueTasteInput.TextChanged -= VentrueTasteInput_TextChanged;
            VentrueTasteInput.TextChanged += VentrueTasteInput_TextChanged;
            UpdateRedistributeAttributesButton(isNosferatu && !plasticSurgeryDone);
        }

        private Dictionary<string, int> GetCurrentAttributeValues()
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string[] names = { "Сила", "Ловкость", "Выносливость", "Обаяние", "Манипуляция", "Привлекательность", "Восприятие", "Интеллект", "Смекалка" };
            foreach (string name in names)
            {
                int value;
                if (_character.CreationState.AttributeDots != null && _character.CreationState.AttributeDots.TryGetValue(name, out value))
                    result[name] = value;
                else
                {
                    var track = FindTracks(this).FirstOrDefault(t => string.Equals(t.Label, name, StringComparison.OrdinalIgnoreCase));
                    result[name] = track == null ? CharacterCreationRules.GetCreationLimit("attribute_base") : track.Value;
                }
            }
            return result;
        }

        private void ResetAttributesForClanChange()
        {
            if (_character == null || _character.CreationState == null) return;
            if (_character.CreationState.AttributeDots == null)
                _character.CreationState.AttributeDots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int baseValue = CharacterCreationRules.GetCreationLimit("attribute_base");
            foreach (var pair in GetCurrentAttributeValues())
            {
                if (pair.Value > baseValue && AdvancementCheck != null)
                    AdvancementCheck("attribute", pair.Key, pair.Value, baseValue);
                _character.CreationState.AttributeDots[pair.Key] = baseValue;
                _lastTrackValues[pair.Key] = baseValue;
            }

            _character.CreationState.InitialAllocationCompleted = false;
            _character.CreationState.InitialAllocationEverCompleted = false;
            if (_character.CreationState.InitialAttributeDots != null) _character.CreationState.InitialAttributeDots.Clear();
            if (_character.CreationState.InitialAbilityDots != null) _character.CreationState.InitialAbilityDots.Clear();
            if (_character.CreationState.PurchaseLedger != null)
                _character.CreationState.PurchaseLedger.RemoveAll(t => t.Block == "Характеристики" && t.Resource == "initial_pool");
            if (_character.CreationState.InitialPurchases != null)
                _character.CreationState.InitialPurchases.RemoveAll(p => p.Block == "Характеристики");

            foreach (var track in FindTracks(this).Where(t => _character.CreationState.AttributeDots.ContainsKey(t.Label)))
                track.SetTrackValue(baseValue);

            UpdateInitialAllocationButton();
            RefreshInitialBlockControls();
        }

        private void ResetDisciplinesForClanChange(string previousClanId)
        {
            if (_character == null || _character.CreationState == null) return;
            var state = _character.CreationState;
            var oldDisciplines = _character.Disciplines.ToList();
            var oldItemNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string newClanName = _character.ClanName;
            var previousClan = ClanService.GetClans().FirstOrDefault(c =>
                string.Equals(c.Id, previousClanId, StringComparison.OrdinalIgnoreCase));

            // Расчёт возврата опыта зависит от того, была ли дисциплина клановой
            // и от множителя Каитифов. Во время возврата временно используем старый клан,
            // поскольку новое имя клана уже записано обработчиком SelectionChanged.
            if (previousClan != null) _character.ClanName = previousClan.Name;
            try
            {
                foreach (var item in oldDisciplines)
                {
                    string itemName = item.IsSecondaryPath && !string.IsNullOrWhiteSpace(item.PathName)
                        ? item.Name + " — " + item.PathName : item.Name;
                    oldItemNames.Add(itemName);
                    if (item.Level > 0 && AdvancementCheck != null)
                        AdvancementCheck(item.IsSecondaryPath ? "secondary_path" : "discipline", itemName, item.Level, 0);
                }
            }
            finally
            {
                _character.ClanName = newClanName;
            }

            _character.Disciplines.Clear();
            // Ритуалы принадлежат дисциплинам старого клана.
            if (_character.Rituals != null) _character.Rituals.Clear();

            if (state.InitialDisciplineDots == null)
                state.InitialDisciplineDots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            else state.InitialDisciplineDots.Clear();
            state.InitialDisciplineBlockEverConfirmed = false;
            state.UnconfirmInitialBlock("disciplines");

            // Удаляем все записи стартовой закупки. Оплаченные freebie/experience
            // транзакции к этому моменту уже обработаны RefundAdvancement; очистка
            // ниже защищает от осиротевших записей старых/мигрированных персонажей.
            if (state.InitialPurchases != null)
                state.InitialPurchases.RemoveAll(p => string.Equals(p.Block, "Дисциплины", StringComparison.OrdinalIgnoreCase));
            if (state.PurchaseLedger != null)
                state.PurchaseLedger.RemoveAll(t => string.Equals(t.Block, "Дисциплины", StringComparison.OrdinalIgnoreCase));
            if (state.FreebiePurchases != null)
                state.FreebiePurchases.RemoveAll(p => string.Equals(p.Block, "Дисциплины", StringComparison.OrdinalIgnoreCase) && oldItemNames.Contains(p.ItemName));
            if (state.ExperiencePurchases != null)
                state.ExperiencePurchases.RemoveAll(p => oldItemNames.Contains(p.ItemName) &&
                    (string.Equals(p.Category, "discipline", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(p.Category, "secondary_path", StringComparison.OrdinalIgnoreCase)));

            ClanChanged?.Invoke();
        }

        private void UpdateRedistributeAttributesButton(bool nosferatuLocked)
        {
            if (RedistributeAttributesButton == null || _character == null) return;
            int attractiveness = CharacterCreationRules.GetCreationLimit("attribute_base");
            int storedAttractiveness;
            if (_character.CreationState.AttributeDots != null &&
                _character.CreationState.AttributeDots.TryGetValue("Привлекательность", out storedAttractiveness))
                attractiveness = storedAttractiveness;
            bool needsRedistribution = nosferatuLocked &&
                _character.CreationState.InitialAllocationCompleted &&
                attractiveness > CharacterCreationRules.GetCreationLimit("attribute_base");
            RedistributeAttributesButton.Visibility = needsRedistribution ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RedistributeAttributes_Click(object sender, RoutedEventArgs e)
        {
            if (_character == null) return;
            var dialog = new InitialAllocationWindow(Window.GetWindow(this), _character,
                attributesOnly: true, lockAttractiveness: true);
            if (dialog.ShowDialog() == true)
            {
                ApplySavedAllocations();
                AttachAllocationPersistence();
                UpdateRedistributeAttributesButton(true);
            }
        }

        private void SetWeaknessText(string value)
        {
            _updatingWeakness = true;
            ClanWeaknessInput.Text = value ?? string.Empty;
            _updatingWeakness = false;
            UpdateResetButton();
        }

        private void ClanWeaknessInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_updatingWeakness || _character == null) return;
            _character.ClanWeakness = ClanWeaknessInput.Text;
            UpdateResetButton();
        }

        private void UpdateResetButton()
        {
            bool changed = _character != null &&
                !string.Equals(ClanWeaknessInput.Text, _character.OriginalClanWeakness ?? string.Empty,
                    StringComparison.Ordinal);
            ResetClanWeaknessButton.Visibility = changed ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ResetClanWeakness_Click(object sender, RoutedEventArgs e)
        {
            var result = ThemedDialog.Show(
                "Вернуть исходный изъян выбранного клана? Все внесённые изменения в этом поле будут потеряны.",
                "Сброс кланового изъяна", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            _character.ClanWeakness = _character.OriginalClanWeakness ?? string.Empty;
            SetWeaknessText(_character.ClanWeakness);
        }

        private void NosferatuPlasticSurgery_Click(object sender, RoutedEventArgs e)
        {
            if (_character == null) return;
            bool surgeryDone = _character.NosferatuPlasticSurgery;
            string message = surgeryDone
                ? "Изуродовать себя снова? Привлекательность будет обнулена и заблокирована."
                : "Применить пластическую хирургию? Привлекательность снова станет доступна для распределения.";
            string title = surgeryDone ? "Изуродовать себя" : "Пластическая хирургия";
            var result = ThemedDialog.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            _character.NosferatuPlasticSurgery = !surgeryDone;
            SocialAttributes.ForcedZeroName = _character.NosferatuPlasticSurgery ? null : "Привлекательность";
            var attractivenessTrack = FindTracks(this).FirstOrDefault(t =>
                string.Equals(t.Label, "Привлекательность", StringComparison.OrdinalIgnoreCase));
            if (attractivenessTrack != null)
            {
                int value = _character.NosferatuPlasticSurgery
                    ? CharacterCreationRules.GetCreationLimit("attribute_base") : 0;
                attractivenessTrack.SetTrackValue(value);
                _character.CreationState.AttributeDots["Привлекательность"] = value;
                _lastTrackValues["Привлекательность"] = value;
            }
            NosferatuPlasticSurgeryButton.Content = _character.NosferatuPlasticSurgery ? "Изуродовать себя" : "Пластическая хирургия";
            RefreshInitialBlockControls();
            UpdateRedistributeAttributesButton(!_character.NosferatuPlasticSurgery);
        }

        private void VentrueTasteInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_character == null) return;
            _character.VentruePreyCategory = VentrueTasteInput.Text;
        }
    }
}
