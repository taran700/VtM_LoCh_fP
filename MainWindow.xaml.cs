using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;
using System.Collections.ObjectModel;
using VtM_LoCh_fP.Controls;
using VtM_LoCh_fP.ViewModels;

namespace VtM_LoCh_fP
{
    /// <summary>
    /// Окно-оболочка: заголовок, переключение темы и вкладки-страницы.
    /// Само содержимое страниц лежит во вью (Views/), а не собирается в коде.
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _manualPurchaseInProgress;
        private const double DragThreshold = 6.0;

        private TabItem _draggedTab;
        private Point _dragStart;
        private DragAdorner _adorner;
        private AdornerLayer _adornerLayer;
        private bool _isDragging;

        private readonly CharacterProfile _character;
        private bool _initialCompletionApplied;

        internal MainWindow(CharacterProfile character)
        {
            ThemeManager.ApplyDarkIfSelected();
            InitializeComponent();
            ThemeManager.AttachWindow(this);
            _character = character;
            if (_character.CreationState == null) _character.CreationState = new CharacterCreationState();
            if (_character.CreationState.OtherDots == null) _character.CreationState.OtherDots = new Dictionary<string, int>();
            if (_character.CreationState.FreebiePurchases == null) _character.CreationState.FreebiePurchases = new List<CreationPurchase>();
            if (_character.CreationState.ExperiencePurchases == null) _character.CreationState.ExperiencePurchases = new List<ExperiencePurchase>();
            if (_character.CreationState.ConfirmedFreebieBlocks == null) _character.CreationState.ConfirmedFreebieBlocks = new List<string>();
            if (_character.CreationState.ConfirmedInitialBlocks == null) _character.CreationState.ConfirmedInitialBlocks = new List<string>();
            CharacterPage.InitializeCharacter(character);
            AdvantagesPage.InitializeCharacter(character);
            DisciplinesPage.InitializeCharacter(character);
            HistoryPage.InitializeCharacter(character);
            EquipmentPage.InitializeCharacter(character);
            AdvantagesPage.InitialPurchaseProgressChanged += UpdateInitialPurchaseButton;
            CharacterPage.InitialPurchaseRequested += UpdateInitialPurchaseButton;
            CharacterPage.FreebieJournalRequested += () => FreebieJournal_Click(this, new RoutedEventArgs());
            CharacterPage.AdvancementCheck += ChargeAdvancement;
            AdvantagesPage.AdvancementCheck += ChargeAdvancement;
            AdvantagesPage.VirtueAdvancementCheck += ChargeAdvancement;
            DisciplinesPage.AdvancementCheck += ChargeAdvancement;
            DisciplinesPage.InitialPurchaseProgressChanged += UpdateInitialPurchaseButton;
            CharacterPage.GenerationChanged += () =>
            {
                AdvantagesPage.ApplyGenerationRules(character);
                CharacterPage.ApplyGenerationLimits();
                AdvantagesPage.ApplyTraitPointLimit(_character.CreationState.InitialPurchaseConfirmed
                    ? (AppSettings.GenerationPointIncrease ? Math.Max(5, character.TraitMax) : 5)
                    : 5);
                DisciplinesPage.RefreshGenerationLimits();
            };
            AdvantagesPage.ApplyGenerationRules(character);
            AdvantagesPage.ApplyTraitPointLimit(_character.CreationState.InitialPurchaseConfirmed
                    ? (AppSettings.GenerationPointIncrease ? Math.Max(5, character.TraitMax) : 5)
                    : 5);
            Title = "VtM V20: " + character.Name + " — " + character.CreationMode;
            var rulesService = new CharacterRulesService(character);
            _character.CreationState.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CharacterCreationState.FreebiePoints))
                    UpdateFreebieJournalButton();
            };
            UpdateFreebieJournalButton();
            UpdateInitialPurchaseButton();
        }


        private bool ChargeAdvancement(string costKey, string itemName, int previous, int next)
        {
            if (_manualPurchaseInProgress) return true;
            if (next < previous) { RefundAdvancement(costKey, itemName, previous, next); return true; }
            if (next == previous) return true;

            bool isFreeNeonate = string.Equals(_character.CreationMode, "Свободный Неонат", StringComparison.OrdinalIgnoreCase);
            bool isFledgling = string.Equals(_character.CreationMode, "Птенец на поводке Сира", StringComparison.OrdinalIgnoreCase);
            int increase = next - previous;

            string mode = AppSettings.NeonatePurchaseMode ?? "Свободные пункты → опыт";
            bool useFreebies = isFreeNeonate && (mode == "Свободные пункты → опыт" || mode == "Только свободные пункты");
            bool useExperience = isFledgling || (isFreeNeonate && (mode == "Свободные пункты → опыт" || mode == "Только опыт"));
            if (isFreeNeonate && !useFreebies && !useExperience)
                return true; // Вручную, без автоматического списания.

            int freebieUnitCost = useFreebies ? CharacterCreationRules.GetFreebieCost(costKey) : 0;
            int freebieLevels = freebieUnitCost > 0
                ? Math.Min(increase, _character.CreationState.FreebiePoints / freebieUnitCost)
                : 0;
            int experienceLevels = increase - freebieLevels;

            // Биографические факты приобретаются за свободные пункты; отдельной
            // цены опыта для них в заданной таблице нет.
            if (experienceLevels > 0 && costKey == "background")
            {
                ThemedDialog.Show(this, "Факты биографии оплачиваются свободными пунктами. Для этого изменения недостаточно свободных пунктов.", "Недостаточно свободных пунктов", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (experienceLevels > 0 && !useExperience)
            {
                string message = useFreebies
                    ? "Свободных пунктов недостаточно, а закуп за опыт отключён."
                    : "Автоматическая закупка отключена в настройках.";
                ThemedDialog.Show(this, message, "Прокачка недоступна", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            int experienceCost = 0;
            if (experienceLevels > 0 && useExperience)
            {
                int firstExperienceLevel = previous + freebieLevels;
                experienceCost = CalculateExperienceCost(costKey, itemName, firstExperienceLevel, next);
            }

            if (experienceLevels > 0 && _character.CreationState.ExperiencePoints < experienceCost)
            {
                ThemedDialog.Show(this, "Недостаточно опыта для улучшения «" + itemName + "». Нужно: " + experienceCost + ", доступно: " + _character.CreationState.ExperiencePoints + ".", "Недостаточно опыта", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Списываем ресурсы только после проверки полной стоимости.
            if (freebieLevels > 0)
            {
                int spent = freebieLevels * freebieUnitCost;
                _character.CreationState.FreebiePoints -= spent;
                if (_character.CreationState.FreebiePurchases == null)
                    _character.CreationState.FreebiePurchases = new List<CreationPurchase>();
                string block = costKey == "attribute" ? "Характеристики" :
                               costKey == "ability" ? "Способности" :
                               costKey == "virtue" ? "Добродетели" :
                               (costKey == "discipline" || costKey == "secondary_path") ? "Дисциплины" :
                               costKey == "background" ? "Фоны" :
                               costKey == "willpower" ? "Сила воли" : "Путь";
                _character.CreationState.ConfirmedFreebieBlocks.RemoveAll(x => string.Equals(x, block, StringComparison.OrdinalIgnoreCase));
                _character.CreationState.FreebiePurchases.Add(new CreationPurchase
                {
                    Block = block,
                    ItemId = itemName,
                    ItemName = itemName,
                    PreviousValue = previous,
                    NewValue = previous + freebieLevels,
                    Cost = spent,
                    Source = "freebie"
                });
            }

            if (experienceCost > 0)
            {
                _character.CreationState.ExperiencePoints -= experienceCost;
                if (_character.CreationState.ExperiencePurchases == null)
                    _character.CreationState.ExperiencePurchases = new List<ExperiencePurchase>();
                _character.CreationState.ExperiencePurchases.Add(new ExperiencePurchase
                {
                    Category = costKey,
                    ItemName = itemName,
                    PreviousValue = previous + freebieLevels,
                    NewValue = next,
                    Cost = experienceCost,
                    Source = "experience"
                });
            }

            return true;
        }

        private void RefundAdvancement(string costKey, string itemName, int oldValue, int newValue)
        {
            int refundFreebies = 0;
            var freebies = _character.CreationState.FreebiePurchases;
            if (freebies != null)
            {
                foreach (var purchase in freebies.Where(p => string.Equals(p.Source, "freebie", StringComparison.OrdinalIgnoreCase) && string.Equals(p.ItemName, itemName, StringComparison.OrdinalIgnoreCase) && p.NewValue > newValue && p.PreviousValue < oldValue).OrderByDescending(p => p.NewValue).ToList())
                {
                    int from = Math.Max(newValue, purchase.PreviousValue);
                    int to = Math.Min(oldValue, purchase.NewValue);
                    if (to <= from) continue;
                    int levels = to - from;
                    int unit = purchase.NewValue > purchase.PreviousValue ? purchase.Cost / (purchase.NewValue - purchase.PreviousValue) : 0;
                    refundFreebies += levels * unit;
                    if (from <= purchase.PreviousValue) freebies.Remove(purchase);
                    else { purchase.NewValue = from; purchase.Cost = Math.Max(0, (from - purchase.PreviousValue) * unit); }
                }
            }
            if (refundFreebies > 0) _character.CreationState.FreebiePoints += refundFreebies;

            var experience = _character.CreationState.ExperiencePurchases;
            if (experience != null)
            {
                foreach (var purchase in experience.Where(p => string.Equals(p.Source, "experience", StringComparison.OrdinalIgnoreCase) && string.Equals(p.ItemName, itemName, StringComparison.OrdinalIgnoreCase) && p.NewValue > newValue && p.PreviousValue < oldValue).OrderByDescending(p => p.NewValue).ToList())
                {
                    int from = Math.Max(newValue, purchase.PreviousValue);
                    int to = Math.Min(oldValue, purchase.NewValue);
                    if (to <= from) continue;
                    int refund = CalculateExperienceCost(costKey, itemName, from, to);
                    _character.CreationState.ExperiencePoints += refund;
                    if (from <= purchase.PreviousValue) experience.Remove(purchase);
                    else { purchase.NewValue = from; purchase.Cost = CalculateExperienceCost(costKey, itemName, purchase.PreviousValue, from); }
                }
            }
        }

        private int CalculateExperienceCost(string costKey, string itemName, int currentValue, int targetValue)
        {
            bool caitiff = !string.IsNullOrWhiteSpace(_character.ClanName) &&
                _character.ClanName.IndexOf("каити", StringComparison.OrdinalIgnoreCase) >= 0;
            bool clanDiscipline = costKey == "discipline" && IsClanDiscipline(itemName);
            int total = 0;
            for (int level = currentValue; level < targetValue; level++)
                total += CharacterCreationRules.GetExperienceCostForLevel(costKey, level, caitiff, clanDiscipline);
            return total;
        }

        private bool IsClanDiscipline(string itemName)
        {
            if (string.IsNullOrWhiteSpace(_character.ClanName) || string.IsNullOrWhiteSpace(itemName))
                return false;
            try
            {
                var knownClan = ClanService.GetClans().FirstOrDefault(x =>
                    string.Equals(x.Name, _character.ClanName, StringComparison.OrdinalIgnoreCase));
                if (knownClan == null) return false;
                var clanIds = new HashSet<string>(DisciplineService.GetClanDisciplineIds(_character.ClanName), StringComparer.OrdinalIgnoreCase);
                var definition = DisciplineService.GetAll().FirstOrDefault(x =>
                    string.Equals(x.Name, itemName, StringComparison.OrdinalIgnoreCase) ||
                    itemName.StartsWith(x.Name + ".", StringComparison.OrdinalIgnoreCase));
                return definition != null && clanIds.Contains(definition.Id);
            }
            catch { return false; }
        }

        private void UpdateInitialPurchaseButton()
        {
            CharacterPage?.RefreshPurchaseHeader();
            if (_character != null && _character.CreationState.InitialPurchaseConfirmed && !_initialCompletionApplied)
            {
                _initialCompletionApplied = true;
                CharacterPage.ApplyGenerationLimits();
                DisciplinesPage.RefreshGenerationLimits();
                AdvantagesPage.RefreshBiographyFacts();
                AdvantagesPage.RefreshVirtueTracks();
                AdvantagesPage.ApplyTraitPointLimit(AppSettings.GenerationPointIncrease ? Math.Max(5, _character.TraitMax) : 5);
            }
        }

        private void InitialPurchase_Click(object sender, RoutedEventArgs e)
        {
            // Подтверждение стартовой закупки выполняется непосредственно в соответствующих блоках листа.
        }

        private void UpdateFreebieJournalButton()
        {
            CharacterPage?.RefreshPurchaseHeader();
        }

        private void FreebieJournal_Click(object sender, RoutedEventArgs e)
        {
            new FreebiePurchaseJournalWindow(this, _character, ResetFreebieBlock).ShowDialog();
            UpdateFreebieJournalButton();
        }

        public void RefreshGenerationFromCharacter()
        {
            int generation = _character.CreationState.BaseGeneration ?? 13;
            var generationFact = _character.BiographyFacts.FirstOrDefault(x =>
                string.Equals(x.Name, "Поколение", StringComparison.OrdinalIgnoreCase));
            if (generationFact != null && generationFact.Level > 0)
                generation = Math.Max(8, 13 - generationFact.Level);

            if (_character.MeritsFlaws.Any(x => x.Id == "fifteenth_generation"))
                generation = 15;
            else if (_character.MeritsFlaws.Any(x => x.Id == "fourteenth_generation"))
                generation = 14;

            CharacterPage.ApplyDerivedGeneration(generation);
            AdvantagesPage.ApplyGenerationRules(_character);
            AdvantagesPage.ApplyTraitPointLimit(_character.CreationState.InitialPurchaseConfirmed
                    ? (AppSettings.GenerationPointIncrease ? Math.Max(5, _character.TraitMax) : 5)
                    : 5);
            DisciplinesPage.RefreshGenerationLimits();
        }

        private void ExperiencePurchase_Click(object sender, RoutedEventArgs e)
        {
            if (!_character.CreationState.InitialPurchaseConfirmed)
            {
                ThemedDialog.Show(this, "Сначала завершите стартовую закупку и подтвердите её.", "Покупка за опыт", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!AppSettings.ExperiencePurchasesEnabled)
            {
                ThemedDialog.Show(this, "Закуп за опыт отключён в настройках.", "Покупка за опыт", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _manualPurchaseInProgress = true;
            try
            {
                new ExperiencePurchaseWindow(this, _character,
                    BuildAdvancementOptions().Where(x => x.CostKey != "background")).ShowDialog();
            }
            finally { _manualPurchaseInProgress = false; }
        }

        private void FreebiePurchase_Click(object sender, RoutedEventArgs e)
        {
            if (!_character.CreationState.InitialPurchaseConfirmed)
            {
                ThemedDialog.Show(this, "Сначала завершите стартовую закупку и подтвердите её.", "Покупка за свободные пункты", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!AppSettings.FreebiePurchasesEnabled)
            {
                ThemedDialog.Show(this, "Закуп за свободные пункты отключён. Лист можно редактировать вручную.", "Покупка", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!string.Equals(_character.CreationMode, "Свободный Неонат", StringComparison.OrdinalIgnoreCase))
            {
                ThemedDialog.Show(this, "Закуп за свободные пункты доступен в режиме Неоната.", "Покупка", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _manualPurchaseInProgress = true;
            try
            {
                new FreebiePurchaseWindow(this, _character, BuildAdvancementOptions()).ShowDialog();
            }
            finally { _manualPurchaseInProgress = false; }
            UpdateFreebieJournalButton();
        }

        private List<AdvancementOption> BuildAdvancementOptions()
        {
            var options = new List<AdvancementOption>();
            foreach (var track in FindTracks(CharacterPage))
            {
                if (track.IsForcedZero) continue;
                string label = track.Label;
                if (_character.CreationState.AttributeDots.ContainsKey(label))
                {
                    options.Add(new AdvancementOption { Category = "Характеристика", CostKey = "attribute", Name = label,
                        CurrentValue = track.Value, Maximum = track.Count,
                        Apply = value => { track.SetTrackValue(value); _character.CreationState.AttributeDots[label] = value; } });
                }
                else if (_character.CreationState.AbilityDots.ContainsKey(label))
                {
                    options.Add(new AdvancementOption { Category = "Способность", CostKey = "ability", Name = label,
                        CurrentValue = track.Value, Maximum = track.Count,
                        Apply = value => { track.SetTrackValue(value); _character.CreationState.AbilityDots[label] = value; } });
                }
            }
            foreach (string virtueName in AdvantagesPage.GetVirtueNames())
            {
                string label = virtueName;
                options.Add(new AdvancementOption { Category = "Добродетель", CostKey = "virtue", Name = label,
                    CurrentValue = AdvantagesPage.GetVirtueRating(label), Maximum = 5,
                    Apply = value => AdvantagesPage.SetVirtueRating(label, value) });
            }
            foreach (var discipline in _character.Disciplines)
            {
                var item = discipline;
                string disciplineCostKey = item.IsSecondaryPath ? "secondary_path" : "discipline";
                string disciplineDisplayName = item.IsSecondaryPath && !string.IsNullOrWhiteSpace(item.PathName)
                    ? item.Name + " — " + item.PathName : item.Name;
                options.Add(new AdvancementOption { Category = item.IsSecondaryPath ? "Вторичный путь" : "Дисциплина",
                    CostKey = disciplineCostKey, Name = disciplineDisplayName,
                    CurrentValue = item.Level, Maximum = Math.Max(1, _character.DisciplineMax),
                    CanPurchase = () => !string.IsNullOrWhiteSpace(item.Justification),
                    PurchaseRestrictionMessage = "Укажите обоснование изучения дисциплины на вкладке «Дисциплины».",
                    Apply = value => { item.Level = value; DisciplinesPage.RefreshGenerationLimits(); } });
            }
            foreach (var definition in DisciplineService.GetAll().Where(x => !x.IsSorcery))
            {
                if (_character.Disciplines.Any(x => x.DisciplineId == definition.Id)) continue;
                var captured = definition;
                options.Add(new AdvancementOption { Category = "Новая дисциплина", CostKey = "discipline", Name = captured.Name,
                    CurrentValue = 0, Maximum = Math.Max(1, _character.DisciplineMax),
                    CanPurchase = () => false,
                    PurchaseRestrictionMessage = "Сначала добавьте дисциплину на вкладке «Дисциплины» и заполните её обоснование.",
                    Apply = value =>
                    {
                        var existing = _character.Disciplines.FirstOrDefault(x => x.DisciplineId == captured.Id);
                        if (existing == null)
                            _character.Disciplines.Add(new CharacterDiscipline { DisciplineId = captured.Id, Name = captured.Name, Level = value, IsCustom = false });
                        else existing.Level = value;
                        DisciplinesPage.RefreshGenerationLimits();
                    } });
            }
            foreach (var ownedSorcery in _character.Disciplines
                .GroupBy(x => x.DisciplineId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()).ToList())
            {
                var definition = DisciplineService.GetAll().FirstOrDefault(x =>
                    string.Equals(x.Id, ownedSorcery.DisciplineId, StringComparison.OrdinalIgnoreCase) && x.IsSorcery);
                if (definition == null) continue;
                foreach (var path in definition.Paths)
                {
                    if (_character.Disciplines.Any(x =>
                        string.Equals(x.DisciplineId, definition.Id, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(x.PathId ?? string.Empty, path.Id ?? string.Empty, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    var capturedDefinition = definition;
                    var capturedPath = path;
                    options.Add(new AdvancementOption
                    {
                        Category = "Новый путь",
                        CostKey = "secondary_path",
                        Name = capturedDefinition.Name + " — " + capturedPath.Name,
                        CurrentValue = 0,
                        Maximum = Math.Max(1, _character.DisciplineMax),
                        CanPurchase = () => false,
                        PurchaseRestrictionMessage = "Сначала добавьте этот путь на вкладке «Дисциплины» и заполните обоснование.",
                        Apply = value =>
                        {
                            var existing = _character.Disciplines.FirstOrDefault(x =>
                                string.Equals(x.DisciplineId, capturedDefinition.Id, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(x.PathId ?? string.Empty, capturedPath.Id ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                            if (existing == null)
                                _character.Disciplines.Add(new CharacterDiscipline
                                {
                                    DisciplineId = capturedDefinition.Id,
                                    Name = capturedDefinition.Name,
                                    Level = value,
                                    IsCustom = capturedDefinition.IsCustom,
                                    PathId = capturedPath.Id,
                                    PathName = capturedPath.Name,
                                    IsSecondaryPath = true
                                });
                            else existing.Level = value;
                            DisciplinesPage.RefreshGenerationLimits();
                        }
                    });
                }
            }
            foreach (var fact in _character.BiographyFacts)
            {
                var captured = fact;
                options.Add(new AdvancementOption { Category = "Фон", CostKey = "background", Name = captured.Name,
                    CurrentValue = captured.Level, Maximum = 5, Apply = value => { captured.Level = value; AdvantagesPage.RefreshBiographyFacts(); RefreshGenerationFromCharacter(); } });
            }
            foreach (var definition in BioFactService.GetAll())
            {
                if (_character.BiographyFacts.Any(x => string.Equals(x.Name, definition.Name, StringComparison.OrdinalIgnoreCase))) continue;
                var captured = definition;
                options.Add(new AdvancementOption { Category = "Новый фон", CostKey = "background", Name = captured.Name,
                    CurrentValue = 0, Maximum = 5, Apply = value =>
                    {
                        var existing = _character.BiographyFacts.FirstOrDefault(x => string.Equals(x.Name, captured.Name, StringComparison.OrdinalIgnoreCase));
                        if (existing == null)
                            _character.BiographyFacts.Add(new BioFactEntry { Name = captured.Name, Level = value });
                        else existing.Level = value;
                        AdvantagesPage.RefreshBiographyFacts();
                        RefreshGenerationFromCharacter();
                    } });
            }
            options.Add(new AdvancementOption { Category = "Сила воли", CostKey = "willpower", Name = "Постоянная сила воли",
                CurrentValue = AdvantagesPage.GetWillpowerPermanent(), Maximum = 10,
                Apply = value => AdvantagesPage.SetWillpowerPermanent(value) });
            options.Add(new AdvancementOption { Category = "Путь", CostKey = "humanity_path", Name = _character.HumanityPathIsCustom ? _character.CustomHumanityPathName : _character.HumanityPathId,
                CurrentValue = _character.HumanityPathRating, Maximum = 10,
                Apply = value => AdvantagesPage.SetHumanityPathRating(value) });
            return options;
        }

        private void ResetFreebieBlock(string block, List<CreationPurchase> purchases)
        {
            if (block == "Достоинства и недостатки")
            {
                HistoryPage.RefreshMeritsFlaws();
                RefreshGenerationFromCharacter();
                return;
            }
            foreach (var itemGroup in purchases.GroupBy(x => x.ItemName))
            {
                var first = itemGroup.First();
                if (block == "Характеристики")
                    _character.CreationState.AttributeDots[first.ItemName] = first.PreviousValue;
                else if (block == "Способности")
                    _character.CreationState.AbilityDots[first.ItemName] = first.PreviousValue;
                else if (block == "Добродетели")
                    AdvantagesPage.SetVirtueRating(first.ItemName, first.PreviousValue);
                else if (block == "Дисциплины")
                {
                    var discipline = _character.Disciplines.FirstOrDefault(x =>
                        string.Equals(x.Name, first.ItemName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.Name + " — " + (x.PathName ?? string.Empty), first.ItemName, StringComparison.OrdinalIgnoreCase));
                    if (discipline != null)
                    {
                        if (first.PreviousValue == 0) _character.Disciplines.Remove(discipline);
                        else discipline.Level = first.PreviousValue;
                    }
                }
                else if (block == "Фоны")
                {
                    var fact = _character.BiographyFacts.FirstOrDefault(x => x.Name == first.ItemName);
                    if (fact != null)
                    {
                        if (first.PreviousValue == 0) _character.BiographyFacts.Remove(fact);
                        else fact.Level = first.PreviousValue;
                    }
                }
                else if (block == "Сила воли" || block == "Путь")
                {
                    if (block == "Сила воли") AdvantagesPage.SetWillpowerPermanent(first.PreviousValue);
                    if (block == "Путь") AdvantagesPage.SetHumanityPathRating(first.PreviousValue);
                }
            }
            CharacterPage.RefreshSavedAllocations();
            AdvantagesPage.RefreshVirtueTracks();
            AdvantagesPage.RefreshBiographyFacts();
            DisciplinesPage.RefreshGenerationLimits();
            RefreshGenerationFromCharacter();
        }

        private static IEnumerable<TrackControl> FindTracks(DependencyObject parent)
        {
            if (parent == null) yield break;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is TrackControl track) yield return track;
                foreach (var nested in FindTracks(child)) yield return nested;
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            bool previousTheme = AppSettings.DarkTheme;
            var settings = new SettingsWindow { Owner = this };

            if (settings.ShowDialog() != true)
                return;

            if (previousTheme != AppSettings.DarkTheme)
            {
                ThemeManager.ApplyDarkIfSelected();

                var replacement = new MainWindow(_character)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = Left,
                    Top = Top,
                    Width = Width,
                    Height = Height,
                    WindowState = WindowState
                };

                Application.Current.MainWindow = replacement;
                replacement.Show();
                Close();
            }
            else
            {
                ThemeManager.ApplyDarkIfSelected();
                CharacterPage.ApplyGenerationLimits();
                AdvantagesPage.ApplyTraitPointLimit(_character.CreationState.InitialPurchaseConfirmed
                    ? (AppSettings.GenerationPointIncrease ? Math.Max(5, _character.TraitMax) : 5)
                    : 5);
                DisciplinesPage.InitializeCharacter(_character);
            }
        }

        // ===================== Перетаскивание вкладок =====================

        private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
                EndDrag();
        }

        private void Tabs_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _draggedTab = FindVisualParent<TabItem>(e.OriginalSource as DependencyObject);
            _dragStart = e.GetPosition(SheetTabs);
        }

        private void Tabs_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedTab == null || e.LeftButton != MouseButtonState.Pressed) return;

            Point current = e.GetPosition(SheetTabs);
            if (!_isDragging &&
                Math.Abs(current.X - _dragStart.X) < DragThreshold &&
                Math.Abs(current.Y - _dragStart.Y) < DragThreshold) return;

            if (!_isDragging) BeginDrag(_dragStart);

            _adorner?.MoveTo(current);
            e.Handled = true;
        }

        private void BeginDrag(Point grabOffset)
        {
            _isDragging = true;
            _adorner = new DragAdorner(SheetTabs, _draggedTab, grabOffset);
            _adornerLayer = AdornerLayer.GetAdornerLayer(SheetTabs);
            _adornerLayer?.Add(_adorner);
            Mouse.OverrideCursor = Cursors.SizeAll;
        }

        private void Tabs_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging)
            {
                _draggedTab = null;
                return;
            }

            EndDrag();
            e.Handled = true;
        }

        private void EndDrag()
        {
            if (_adorner != null && _adornerLayer != null) _adornerLayer.Remove(_adorner);
            _adorner = null;
            _adornerLayer = null;
            _draggedTab = null;
            _isDragging = false;
            Mouse.OverrideCursor = null;
        }

        private void Tabs_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void Tabs_Drop(object sender, DragEventArgs e)
        {
            EndDrag();
            e.Handled = true;
        }

        private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateInitialPurchaseButton();
            if (!(e.AddedItems.Count > 0) || !(e.AddedItems[0] is TabItem tab)) return;
            if (!(tab.Content is FrameworkElement content)) return;

            // При переключении вкладки всегда показываем её начало.
            content.Dispatcher.BeginInvoke(new Action(() =>
            {
                var scroll = FindVisualChild<ScrollViewer>(content);
                scroll?.ScrollToTop();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        // ===================== Поиск в визуальном дереве =====================

        private static T FindVisualParent<T>(DependencyObject node) where T : DependencyObject
        {
            while (node != null)
            {
                if (node is T parent) return parent;
                node = node is Visual || node is System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(node)
                    : LogicalTreeHelper.GetParent(node);
            }
            return null;
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match) return match;
                var nested = FindVisualChild<T>(child);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}
