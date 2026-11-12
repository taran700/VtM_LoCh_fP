using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using System.Collections.ObjectModel;
using VtM_LoCh_fP.Controls;
using System.Windows.Threading;
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

        private readonly CharacterProfile _character;
        private bool _initialCompletionApplied;
        private readonly DispatcherTimer _autosaveTimer = new DispatcherTimer();

        internal MainWindow(CharacterProfile character)
        {
            ThemeManager.ApplyDarkIfSelected();
            InitializeComponent();
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            ThemeManager.AttachWindow(this);
            _character = character;
            if (_character.CreationState == null) _character.CreationState = new CharacterCreationState();
            if (_character.CreationState.OtherDots == null) _character.CreationState.OtherDots = new Dictionary<string, int>();
            if (_character.CreationState.FreebiePurchases == null) _character.CreationState.FreebiePurchases = new List<CreationPurchase>();
            if (_character.CreationState.ExperiencePurchases == null) _character.CreationState.ExperiencePurchases = new List<ExperiencePurchase>();
            if (_character.CreationState.PurchaseLedger == null) _character.CreationState.PurchaseLedger = new List<PurchaseTransaction>();
            MigratePurchaseLedgerIfNeeded();
            InitializeInitialAllocationSnapshots();
            if (_character.CreationState.ConfirmedFreebieBlocks == null) _character.CreationState.ConfirmedFreebieBlocks = new List<string>();
            if (_character.CreationState.ConfirmedInitialBlocks == null) _character.CreationState.ConfirmedInitialBlocks = new List<string>();
            CharacterPage.InitializeCharacter(character);
            CharacterPage.CharacterNameChanged += name =>
            {
                Title = "VtM V20: " + name + " — " + _character.CreationMode;
                CharacterSelectionWindow.SaveCharacters();
            };
            AdvantagesPage.InitializeCharacter(character);
            DisciplinesPage.InitializeCharacter(character);
            HistoryPage.InitializeCharacter(character);
            EquipmentPage.InitializeCharacter(character);
            NotesPage.InitializeCharacter(character);
            AdvantagesPage.InitialPurchaseProgressChanged += UpdateInitialPurchaseButton;
            CharacterPage.InitialPurchaseRequested += () =>
            {
                UpdateInitialPurchaseButton();
                DisciplinesPage.RefreshGenerationLimits();
            };
            CharacterPage.AdvancementCheck += ChargeAdvancement;
            CharacterPage.ClanChanged += () =>
            {
                DisciplinesPage.RefreshGenerationLimits();
                UpdateInitialPurchaseButton();
            };
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
            UpdateInitialPurchaseButton();
            _autosaveTimer.Interval = TimeSpan.FromSeconds(2);
            _autosaveTimer.Tick += (s, e) => CharacterSelectionWindow.SaveCharacters();
            _autosaveTimer.Start();
            Closed += (s, e) => { _autosaveTimer.Stop(); CharacterSelectionWindow.SaveCharacters(); };
        }


        private bool ChargeAdvancement(string costKey, string itemName, int previous, int next)
        {
            if (_manualPurchaseInProgress || UndoService.IsRestoring) return true;
            if (next < previous) { RefundAdvancement(costKey, itemName, previous, next); return true; }
            if (next == previous) return true;
            bool derivedVirtueTrait = costKey == "humanity_path" || costKey == "willpower";
            bool virtuesConfirmed = _character.CreationState.ConfirmedInitialBlocks.Contains("virtues");
            if (!_character.CreationState.InitialAllocationCompleted && costKey != "attribute" && costKey != "ability" &&
                !(derivedVirtueTrait && virtuesConfirmed))
            {
                ThemedDialog.Show(this, "Сначала завершите повторное распределение характеристик и способностей.", "Распределение не завершено", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            bool isFreeNeonate = string.Equals(_character.CreationMode, "Свободный Неонат", StringComparison.OrdinalIgnoreCase);
            bool isFledgling = string.Equals(_character.CreationMode, "Птенец на поводке Сира", StringComparison.OrdinalIgnoreCase);
            int increase = next - previous;

            // Поддерживаем оба старых написания смешанного режима и применяем
            // выбранную последовательность ресурсов к обоим режимам персонажа.
            // Раньше для Птенца useFreebies всегда был false, поэтому программа
            // требовала опыт даже при ненулевом остатке свободных пунктов.
            string mode = (AppSettings.NeonatePurchaseMode ?? "Свободные пункты затем опыт")
                .Trim().Replace("→", "затем").ToLowerInvariant();
            bool mixedMode = mode == "свободные пункты затем опыт";
            bool freebieOnlyMode = mode == "только свободные пункты";
            bool experienceOnlyMode = mode == "только опыт";
            bool automaticMode = isFreeNeonate || isFledgling;

            // В смешанном режиме всегда сначала расходуются свободные пункты,
            // затем остаток повышения оплачивается опытом.
            bool useFreebies = costKey == "background" || (automaticMode && (mixedMode || freebieOnlyMode));
            bool useExperience = automaticMode && (mixedMode || experienceOnlyMode);
            if (automaticMode && !useFreebies && !useExperience)
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

            // Снимок делается до изменения ресурсов и журнала, но после всех проверок.
            // Это позволяет отменить прокачку целиком: точки, опыт, свободные пункты,
            // журнал покупок и статусы подтверждения.
            string advancementSnapshot = UndoService.CaptureCreationState(_character);

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
                for (int level = 0; level < freebieLevels; level++)
                    RecordTransaction(block, costKey, itemName, previous + level, previous + level + 1, "freebie", freebieUnitCost, costKey == "virtue");
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
                int levelValue = previous + freebieLevels;
                for (int level = levelValue; level < next; level++)
                    RecordTransaction(BlockForCostKey(costKey), costKey, itemName, level, level + 1, "experience",
                        CalculateExperienceCost(costKey, itemName, level, level + 1), costKey == "virtue");
            }

            if (!UndoService.IsRestoring)
            {
                UndoService.Store("прокачку «" + itemName + "»", () =>
                {
                    UndoService.RestoreCreationState(_character, advancementSnapshot);
                    RestoreAdvancementModelValue(costKey, itemName, previous);
                    RefreshGenerationFromCharacter();
                    AdvantagesPage.RefreshBiographyFacts();
                    DisciplinesPage.RefreshGenerationLimits();
                    CharacterSelectionWindow.SaveCharacters();
                });
            }
            return true;
        }

        private void RestoreAdvancementModelValue(string costKey, string itemName, int value)
        {
            string key = NormalizeCostKey(costKey);
            if (key == "humanity_path") AdvantagesPage.SetHumanityPathRating(value);
            else if (key == "background")
            {
                var fact = _character.BiographyFacts.FirstOrDefault(x => string.Equals(x.Name, itemName, StringComparison.OrdinalIgnoreCase));
                if (fact != null) fact.Level = value;
            }
            else if (key == "discipline" || key == "secondary_path")
            {
                var discipline = _character.Disciplines.FirstOrDefault(x =>
                    string.Equals(x.IsSecondaryPath && !string.IsNullOrWhiteSpace(x.PathName) ? x.Name + " — " + x.PathName : x.Name,
                        itemName, StringComparison.OrdinalIgnoreCase));
                if (discipline != null) discipline.Level = value;
            }
            else if (key == "attribute") _character.CreationState.AttributeDots[itemName] = value;
            else if (key == "ability") _character.CreationState.AbilityDots[itemName] = value;
            else if (key == "virtue") _character.CreationState.OtherDots["virtue:" + itemName] = value;
            else if (key == "willpower") _character.CreationState.OtherDots["willpower_permanent"] = value;

            SetMatchingTrackValues(this, itemName, value);
        }

        private static void SetMatchingTrackValues(DependencyObject root, string label, int value)
        {
            if (root == null) return;
            var track = root as TrackControl;
            if (track != null && string.Equals(track.Label, label, StringComparison.OrdinalIgnoreCase))
                track.SetTrackValue(value);
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++) SetMatchingTrackValues(VisualTreeHelper.GetChild(root, i), label, value);
        }

        private void InitializeInitialAllocationSnapshots()
        {
            var state = _character.CreationState;
            var initialRecords = state.InitialPurchases ?? new List<CreationPurchase>();
            state.InitialVirtueBlockEverConfirmed = state.InitialVirtueBlockEverConfirmed || initialRecords.Any(p => p.Block == "Добродетели");
            state.InitialBackgroundBlockEverConfirmed = state.InitialBackgroundBlockEverConfirmed || initialRecords.Any(p => p.Block == "Факты биографии");
            state.InitialDisciplineBlockEverConfirmed = state.InitialDisciplineBlockEverConfirmed || initialRecords.Any(p => p.Block == "Дисциплины");
            if (!state.InitialAllocationCompleted) return;
            state.InitialAllocationEverCompleted = true;
            if (state.InitialAttributeDots == null) state.InitialAttributeDots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (state.InitialAbilityDots == null) state.InitialAbilityDots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (state.InitialAttributeDots.Count == 0)
                foreach (var group in state.PurchaseLedger.Where(t => !t.IsCancelled && t.Resource == "initial_pool" && t.Block == "Характеристики").GroupBy(t => t.ItemName, StringComparer.OrdinalIgnoreCase))
                    state.InitialAttributeDots[group.Key] = group.Sum(t => Math.Max(0, t.NewValue - t.PreviousValue));
            if (state.InitialAbilityDots.Count == 0)
                foreach (var group in state.PurchaseLedger.Where(t => !t.IsCancelled && t.Resource == "initial_pool" && t.Block == "Способности").GroupBy(t => t.ItemName, StringComparer.OrdinalIgnoreCase))
                    state.InitialAbilityDots[group.Key] = group.Sum(t => Math.Max(0, t.NewValue - t.PreviousValue));
            if (state.InitialBackgroundDots == null) state.InitialBackgroundDots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (state.InitialBackgroundDots.Count == 0)
                foreach (var p in (state.InitialPurchases ?? new List<CreationPurchase>()).Where(x => x.Block == "Факты биографии")) state.InitialBackgroundDots[p.ItemName] = p.NewValue;
            if (state.InitialDisciplineDots == null) state.InitialDisciplineDots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (state.InitialDisciplineDots.Count == 0)
                foreach (var p in (state.InitialPurchases ?? new List<CreationPurchase>()).Where(x => x.Block == "Дисциплины")) state.InitialDisciplineDots[p.ItemName] = p.NewValue;
            if (state.InitialVirtueRatings == null) state.InitialVirtueRatings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (state.InitialVirtueRatings.Count == 0)
                foreach (var purchase in (state.InitialPurchases ?? new List<CreationPurchase>()).Where(p => p.Block == "Добродетели"))
                    state.InitialVirtueRatings[purchase.ItemName] = purchase.NewValue;
        }

        private void MigratePurchaseLedgerIfNeeded()
        {
            var state = _character.CreationState;
            if (state.PurchaseLedger.Count > 0) return;
            // Старые записи не содержали порядка. Восстанавливаем порядок внутри каждого показателя
            // по диапазону уровней; этого достаточно для корректного LIFO-сброса конкретного показателя.
            foreach (var p in state.InitialPurchases ?? new List<CreationPurchase>())
            {
                string category = p.Block == "Характеристики" ? "attribute" : p.Block == "Способности" ? "ability" :
                    p.Block == "Добродетели" ? "virtue" : p.Block == "Дисциплины" ? "discipline" : p.Block == "Факты биографии" ? "background" : null;
                if (category == null) continue;
                int baseValue = category == "attribute" ? CharacterCreationRules.GetCreationLimit("attribute_base") :
                    category == "virtue" ? CharacterCreationRules.GetCreationLimit("virtue_base") : 0;
                for (int level = Math.Max(baseValue, p.PreviousValue); level < p.NewValue; level++)
                    state.PurchaseLedger.Add(new PurchaseTransaction { Order = state.NextPurchaseOrder++, Block = p.Block == "Факты биографии" ? "Фоны" : p.Block,
                        Category = category, ItemId = p.ItemId, ItemName = p.ItemName, PreviousValue = level, NewValue = level + 1, Resource = "initial_pool", Cost = 0,
                        AffectsVirtueDerivedValues = category == "virtue" });
            }
            foreach (var p in state.FreebiePurchases ?? new List<CreationPurchase>())
            {
                int span = p.NewValue - p.PreviousValue;
                if (span <= 0) continue;
                int unit = p.Cost / span;
                for (int level = p.PreviousValue; level < p.NewValue; level++)
                    state.PurchaseLedger.Add(new PurchaseTransaction { Order = state.NextPurchaseOrder++, Block = p.Block,
                        ItemId = p.ItemId, ItemName = p.ItemName, PreviousValue = level, NewValue = level + 1, Resource = "freebie", Cost = unit });
            }
            foreach (var p in state.ExperiencePurchases ?? new List<ExperiencePurchase>())
            {
                string costKey = NormalizeCostKey(p.Category);
                p.Category = costKey;
                int span = p.NewValue - p.PreviousValue;
                if (span <= 0) continue;
                var levelCosts = Enumerable.Range(p.PreviousValue, span)
                    .Select(level => CalculateExperienceCost(costKey, p.ItemName, level, level + 1)).ToList();
                int expected = levelCosts.Sum();
                int assigned = 0;
                for (int index = 0; index < span; index++)
                {
                    int each = index == span - 1 ? p.Cost - assigned :
                        (expected > 0 ? (int)Math.Round((double)p.Cost * levelCosts[index] / expected) : p.Cost / span);
                    assigned += each;
                    int level = p.PreviousValue + index;
                    state.PurchaseLedger.Add(new PurchaseTransaction { Order = state.NextPurchaseOrder++, Block = BlockForCostKey(costKey),
                        Category = costKey, ItemId = p.ItemName, ItemName = p.ItemName, PreviousValue = level, NewValue = level + 1, Resource = "experience", Cost = each,
                        AffectsVirtueDerivedValues = costKey == "virtue" });
                }
            }
            foreach (var group in state.PurchaseLedger.GroupBy(t => t.ItemName, StringComparer.OrdinalIgnoreCase))
            {
                var ordered = group.OrderBy(t => t.PreviousValue).ThenBy(t => t.Order).ToList();
                long order = state.NextPurchaseOrder++;
                foreach (var item in ordered) item.Order = order++;
                state.NextPurchaseOrder = Math.Max(state.NextPurchaseOrder, order);
            }
        }

        private static string NormalizeCostKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return key;
            switch (key.Trim().ToLowerInvariant())
            {
                case "характеристика": case "характеристики": return "attribute";
                case "способность": case "способности": return "ability";
                case "добродетель": case "добродетели": return "virtue";
                case "дисциплина": case "дисциплины": return "discipline";
                case "фон": case "фоны": return "background";
                case "сила воли": return "willpower";
                case "путь": case "человечность": return "humanity_path";
                default: return key;
            }
        }

        private static string BlockForCostKey(string key)
        {
            return key == "attribute" ? "Характеристики" : key == "ability" ? "Способности" :
                key == "virtue" ? "Добродетели" : (key == "discipline" || key == "secondary_path") ? "Дисциплины" :
                key == "background" ? "Фоны" : key == "willpower" ? "Сила воли" : "Путь";
        }

        private void RecordTransaction(string block, string category, string itemName, int previous, int next, string resource, int cost, bool virtueDerived)
        {
            var state = _character.CreationState;
            if (state.PurchaseLedger == null) state.PurchaseLedger = new List<PurchaseTransaction>();
            state.PurchaseLedger.Add(new PurchaseTransaction
            {
                Order = state.NextPurchaseOrder++, Block = block, Category = category,
                ItemId = itemName, ItemName = itemName, PreviousValue = previous, NewValue = next,
                Resource = resource, Cost = cost, AffectsVirtueDerivedValues = virtueDerived, Source = "Player"
            });
        }

        private void RefundAdvancement(string costKey, string itemName, int oldValue, int newValue)
        {
            var state = _character.CreationState;
            if (state.PurchaseLedger == null) state.PurchaseLedger = new List<PurchaseTransaction>();
            var matches = state.PurchaseLedger
                .Where(t => !t.IsCancelled && string.Equals(t.ItemName, itemName, StringComparison.OrdinalIgnoreCase) &&
                            t.NewValue > newValue && t.PreviousValue < oldValue)
                .OrderByDescending(t => t.Order).ToList();

            if (matches.Count > 0)
            {
                foreach (var transaction in matches)
                {
                    int from = Math.Max(newValue, transaction.PreviousValue);
                    int to = Math.Min(oldValue, transaction.NewValue);
                    if (to <= from) continue;
                    int levels = to - from;
                    int unitCost = transaction.NewValue > transaction.PreviousValue
                        ? transaction.Cost / (transaction.NewValue - transaction.PreviousValue) : 0;
                    int refund = levels * unitCost;
                    if (transaction.Resource == "freebie") state.FreebiePoints += refund;
                    else if (transaction.Resource == "experience") state.ExperiencePoints += refund;
                    else if (transaction.Resource == "initial_pool")
                    {
                        if (transaction.Block == "Характеристики" || transaction.Block == "Способности")
                        {
                            // Возвращён стартовый пункт: снова показываем существующий inline-интерфейс.
                            state.InitialAllocationCompleted = false;
                            var initialValues = transaction.Block == "Характеристики" ? state.InitialAttributeDots : state.InitialAbilityDots;
                            if (initialValues == null)
                            {
                                initialValues = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                                if (transaction.Block == "Характеристики") state.InitialAttributeDots = initialValues;
                                else state.InitialAbilityDots = initialValues;
                            }
                            int initialValue;
                            if (initialValues.TryGetValue(transaction.ItemName, out initialValue))
                                initialValues[transaction.ItemName] = Math.Max(0, initialValue - levels);
                        }
                        else if (transaction.Block == "Добродетели")
                        {
                            state.UnconfirmInitialBlock("virtues");
                            if (state.InitialVirtueRatings == null) state.InitialVirtueRatings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            int initialValue;
                            if (state.InitialVirtueRatings.TryGetValue(transaction.ItemName, out initialValue))
                                state.InitialVirtueRatings[transaction.ItemName] = Math.Max(CharacterCreationRules.GetCreationLimit("virtue_base"), initialValue - levels);
                            AdvantagesPage.RecalculatePrimaryVirtueDerivedValues();
                        }
                        else if (transaction.Block == "Дисциплины")
                        {
                            state.UnconfirmInitialBlock("disciplines");
                            if (state.InitialDisciplineDots == null) state.InitialDisciplineDots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            int initialValue;
                            if (state.InitialDisciplineDots.TryGetValue(transaction.ItemName, out initialValue))
                                state.InitialDisciplineDots[transaction.ItemName] = Math.Max(0, initialValue - levels);
                            DisciplinesPage.RefreshGenerationLimits();
                        }
                        else if (transaction.Block == "Фоны")
                        {
                            state.UnconfirmInitialBlock("backgrounds");
                            if (state.InitialBackgroundDots == null) state.InitialBackgroundDots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            int initialValue;
                            if (state.InitialBackgroundDots.TryGetValue(transaction.ItemName, out initialValue))
                                state.InitialBackgroundDots[transaction.ItemName] = Math.Max(0, initialValue - levels);
                            AdvantagesPage.RefreshBiographyFacts();
                        }
                    }
                    // Сохраняем исходную запись для аудита. При частичном откате создаём
                    // активную запись на оставшийся диапазон, а исходную помечаем отменённой.
                    transaction.IsCancelled = true;
                    if (from > transaction.PreviousValue)
                    {
                        state.PurchaseLedger.Add(new PurchaseTransaction
                        {
                            GenerationBatchId = transaction.GenerationBatchId,
                            Order = state.NextPurchaseOrder++, Block = transaction.Block, Category = transaction.Category,
                            ItemId = transaction.ItemId, ItemName = transaction.ItemName,
                            PreviousValue = transaction.PreviousValue, NewValue = from,
                            Resource = transaction.Resource, Cost = (from - transaction.PreviousValue) * unitCost,
                            AffectsVirtueDerivedValues = transaction.AffectsVirtueDerivedValues,
                            DependencyGroupId = transaction.DependencyGroupId, Source = transaction.Source
                        });
                    }
                }
                // Синхронизируем legacy-журналы, чтобы старые окна и списки не показывали уже отменённое.
                TrimLegacyPurchaseRecords(itemName, newValue);
                UpdateInitialPurchaseButton();
                DisciplinesPage.RefreshGenerationLimits();
                return;
            }

            // Совместимость с персонажами, созданными до появления единого журнала.
            int refundFreebies = 0;
            var freebies = state.FreebiePurchases;
            if (freebies != null)
            {
                foreach (var purchase in freebies.Where(p => string.Equals(p.Source, "freebie", StringComparison.OrdinalIgnoreCase) && string.Equals(p.ItemName, itemName, StringComparison.OrdinalIgnoreCase) && p.NewValue > newValue && p.PreviousValue < oldValue).OrderByDescending(p => p.NewValue).ToList())
                {
                    int from = Math.Max(newValue, purchase.PreviousValue), to = Math.Min(oldValue, purchase.NewValue);
                    if (to <= from) continue;
                    int unit = purchase.NewValue > purchase.PreviousValue ? purchase.Cost / (purchase.NewValue - purchase.PreviousValue) : 0;
                    refundFreebies += (to - from) * unit;
                    if (from <= purchase.PreviousValue) freebies.Remove(purchase);
                    else { purchase.NewValue = from; purchase.Cost = (from - purchase.PreviousValue) * unit; }
                }
            }
            if (refundFreebies > 0) state.FreebiePoints += refundFreebies;
            var experience = state.ExperiencePurchases;
            if (experience != null)
            {
                foreach (var purchase in experience.Where(p => string.Equals(p.Source, "experience", StringComparison.OrdinalIgnoreCase) && string.Equals(p.ItemName, itemName, StringComparison.OrdinalIgnoreCase) && p.NewValue > newValue && p.PreviousValue < oldValue).OrderByDescending(p => p.NewValue).ToList())
                {
                    int from = Math.Max(newValue, purchase.PreviousValue), to = Math.Min(oldValue, purchase.NewValue);
                    if (to <= from) continue;
                    state.ExperiencePoints += CalculateExperienceCost(costKey, itemName, from, to);
                    if (from <= purchase.PreviousValue) experience.Remove(purchase);
                    else { purchase.NewValue = from; purchase.Cost = CalculateExperienceCost(costKey, itemName, purchase.PreviousValue, from); }
                }
            }
        }

        private void TrimLegacyPurchaseRecords(string itemName, int newValue)
        {
            var state = _character.CreationState;
            if (state.FreebiePurchases != null)
                foreach (var p in state.FreebiePurchases.Where(x => string.Equals(x.ItemName, itemName, StringComparison.OrdinalIgnoreCase) && x.NewValue > newValue).ToList())
                {
                    if (p.PreviousValue >= newValue) state.FreebiePurchases.Remove(p);
                    else { int span = p.NewValue - p.PreviousValue; int unit = span > 0 ? p.Cost / span : 0; p.NewValue = newValue; p.Cost = (newValue - p.PreviousValue) * unit; }
                }
            if (state.ExperiencePurchases != null)
                foreach (var p in state.ExperiencePurchases.Where(x => string.Equals(x.ItemName, itemName, StringComparison.OrdinalIgnoreCase) && x.NewValue > newValue).ToList())
                {
                    if (p.PreviousValue >= newValue) state.ExperiencePurchases.Remove(p);
                    else { p.NewValue = newValue; p.Cost = CalculateExperienceCost(p.Category, itemName, p.PreviousValue, newValue); }
                }
        }

        private int CalculateExperienceCost(string costKey, string itemName, int currentValue, int targetValue)
        {
            bool caitiff = CharacterCreationRules.UsesCaitiffDisciplineRules(_character);
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
            if (_character != null && !_character.CreationState.InitialPurchaseConfirmed)
            {
                _initialCompletionApplied = false;
                CharacterPage?.ApplyGenerationLimits();
                AdvantagesPage?.ApplyTraitPointLimit(5);
                DisciplinesPage?.RefreshGenerationLimits();
            }
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
            if (!_character.CreationState.InitialAllocationCompleted)
            {
                ThemedDialog.Show(this, "Сначала завершите повторное распределение характеристик и способностей.", "Распределение не завершено", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!_character.CreationState.InitialPurchaseConfirmed)
            {
                ThemedDialog.Show(this, "Сначала завершите стартовую закупку и подтвердите её.", "Покупка за опыт", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            bool isFreeNeonate = string.Equals(_character.CreationMode, "Свободный Неонат", StringComparison.OrdinalIgnoreCase);
            if (isFreeNeonate && !AppSettings.ExperiencePurchasesEnabled)
            {
                ThemedDialog.Show(this, "Закуп за опыт отключён в настройках Неоната.", "Покупка за опыт", MessageBoxButton.OK, MessageBoxImage.Information);
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
            if (!_character.CreationState.InitialAllocationCompleted)
            {
                ThemedDialog.Show(this, "Сначала завершите повторное распределение характеристик и способностей.", "Распределение не завершено", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
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
                new FreebiePurchaseWindow(this, _character, BuildAdvancementOptions().Where(x => x.CostKey != "background")).ShowDialog();
            }
            finally { _manualPurchaseInProgress = false; }
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
                    Apply = value => { item.Level = value; DisciplinesPage.RefreshGenerationLimits(); } });
            }
            foreach (var definition in DisciplineService.GetAll().Where(x => !x.IsSorcery))
            {
                if (_character.Disciplines.Any(x => x.DisciplineId == definition.Id)) continue;
                var captured = definition;
                options.Add(new AdvancementOption { Category = "Новая дисциплина", CostKey = "discipline", Name = captured.Name,
                    CurrentValue = 0, Maximum = Math.Max(1, _character.DisciplineMax),
                    CanPurchase = () => false,
                    PurchaseRestrictionMessage = "Сначала добавьте дисциплину на вкладке «Дисциплины».",
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
                        PurchaseRestrictionMessage = "Сначала добавьте этот путь на вкладке «Дисциплины».",
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
            var state = _character.CreationState;
            var selectedNames = new HashSet<string>((purchases ?? new List<CreationPurchase>()).Select(p => p.ItemName), StringComparer.OrdinalIgnoreCase);
            if (block == "Достоинства и недостатки")
            {
                foreach (var purchase in purchases ?? new List<CreationPurchase>())
                {
                    state.FreebiePoints += purchase.Cost;
                    if (state.PurchaseLedger != null)
                        foreach (var transaction in state.PurchaseLedger.Where(t => !t.IsCancelled && t.Resource == "freebie" && string.Equals(t.ItemId, purchase.ItemId, StringComparison.OrdinalIgnoreCase)))
                            transaction.IsCancelled = true;
                }
                HistoryPage.RefreshMeritsFlaws();
                RefreshGenerationFromCharacter();
                return;
            }

            // Сбрасываем весь блок, а не только записи Freebies: XP и первичные точки
            // также возвращаются в свои исходные бюджеты в порядке LIFO.
            var ledgerItems = state.PurchaseLedger == null
                ? new List<string>()
                : state.PurchaseLedger.Where(t => !t.IsCancelled && string.Equals(t.Block, block, StringComparison.OrdinalIgnoreCase)).Select(t => t.ItemName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (string name in selectedNames) if (!ledgerItems.Contains(name, StringComparer.OrdinalIgnoreCase)) ledgerItems.Add(name);

            foreach (string itemName in ledgerItems)
            {
                var itemTransactions = state.PurchaseLedger == null ? new List<PurchaseTransaction>() :
                    state.PurchaseLedger.Where(t => !t.IsCancelled && string.Equals(t.Block, block, StringComparison.OrdinalIgnoreCase) && string.Equals(t.ItemName, itemName, StringComparison.OrdinalIgnoreCase)).ToList();
                int current = 0;
                int baseline = itemTransactions.Count > 0 ? itemTransactions.Min(t => t.PreviousValue) :
                    ((purchases ?? new List<CreationPurchase>()).FirstOrDefault(p => string.Equals(p.ItemName, itemName, StringComparison.OrdinalIgnoreCase))?.PreviousValue ?? 0);
                string costKey = itemTransactions.Select(t => t.Category).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                if (block == "Характеристики")
                {
                    state.AttributeDots.TryGetValue(itemName, out current);
                    baseline = Math.Max(CharacterCreationRules.GetCreationLimit("attribute_base"), baseline);
                    if (string.Equals(itemName, "Привлекательность", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(_character.ClanName) && _character.ClanName.IndexOf("Носферату", StringComparison.OrdinalIgnoreCase) >= 0 && !_character.NosferatuPlasticSurgery)
                        baseline = 0;
                    costKey = "attribute";
                }
                else if (block == "Способности")
                {
                    state.AbilityDots.TryGetValue(itemName, out current);
                    costKey = "ability";
                }
                else if (block == "Добродетели")
                {
                    current = AdvantagesPage.GetVirtueRating(itemName);
                    baseline = Math.Max(CharacterCreationRules.GetCreationLimit("virtue_base"), baseline);
                    costKey = "virtue";
                }
                else if (block == "Дисциплины")
                {
                    var discipline = _character.Disciplines.FirstOrDefault(x =>
                        string.Equals(x.Name, itemName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.Name + " — " + (x.PathName ?? string.Empty), itemName, StringComparison.OrdinalIgnoreCase));
                    if (discipline == null) continue;
                    current = discipline.Level;
                    costKey = discipline.IsSecondaryPath ? "secondary_path" : "discipline";
                }
                else if (block == "Фоны")
                {
                    var fact = _character.BiographyFacts.FirstOrDefault(x => string.Equals(x.Name, itemName, StringComparison.OrdinalIgnoreCase));
                    if (fact == null) continue;
                    current = fact.Level;
                    costKey = "background";
                }
                else if (block == "Сила воли")
                {
                    current = AdvantagesPage.GetWillpowerPermanent();
                    costKey = "willpower";
                }
                else if (block == "Путь")
                {
                    current = _character.HumanityPathRating;
                    costKey = "humanity_path";
                }

                if (current > baseline && !string.IsNullOrWhiteSpace(costKey))
                    RefundAdvancement(costKey, itemName, current, baseline);

                if (block == "Характеристики") state.AttributeDots[itemName] = baseline;
                else if (block == "Способности") state.AbilityDots[itemName] = baseline;
                else if (block == "Добродетели") AdvantagesPage.SetVirtueRating(itemName, baseline);
                else if (block == "Дисциплины")
                {
                    var discipline = _character.Disciplines.FirstOrDefault(x => string.Equals(x.Name, itemName, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Name + " — " + (x.PathName ?? string.Empty), itemName, StringComparison.OrdinalIgnoreCase));
                    if (discipline != null) { if (baseline <= 0) _character.Disciplines.Remove(discipline); else discipline.Level = baseline; }
                }
                else if (block == "Фоны")
                {
                    var fact = _character.BiographyFacts.FirstOrDefault(x => string.Equals(x.Name, itemName, StringComparison.OrdinalIgnoreCase));
                    if (fact != null) { if (baseline <= 0) _character.BiographyFacts.Remove(fact); else fact.Level = baseline; }
                }
                else if (block == "Сила воли") AdvantagesPage.SetWillpowerPermanent(baseline);
                else if (block == "Путь") AdvantagesPage.SetHumanityPathRating(baseline);
            }

            if (state.InitialPurchases != null)
                state.InitialPurchases.RemoveAll(p => string.Equals(p.Block, block, StringComparison.OrdinalIgnoreCase) ||
                    (block == "Фоны" && p.Block == "Факты биографии") || (block == "Добродетели" && p.Block == "Сила воли"));
            if (state.PurchaseLedger != null)
                foreach (var transaction in state.PurchaseLedger.Where(t => !t.IsCancelled && string.Equals(t.Block, block, StringComparison.OrdinalIgnoreCase)))
                    transaction.IsCancelled = true;
            if (block == "Добродетели") state.UnconfirmInitialBlock("virtues");
            if (block == "Дисциплины") state.UnconfirmInitialBlock("disciplines");
            if (block == "Фоны") state.UnconfirmInitialBlock("backgrounds");

            CharacterPage.RefreshSavedAllocations();
            AdvantagesPage.RefreshVirtueTracks();
            AdvantagesPage.RefreshBiographyFacts();
            DisciplinesPage.RefreshGenerationLimits();
            RefreshGenerationFromCharacter();
            UpdateInitialPurchaseButton();
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
            var settings = new SettingsWindow { Owner = this };

            if (settings.ShowDialog() != true)
                return;
            ThemeManager.ApplyDarkIfSelected();
            CharacterPage.ApplyGenerationLimits();
            AdvantagesPage.ApplyTraitPointLimit(_character.CreationState.InitialPurchaseConfirmed
                ? (AppSettings.GenerationPointIncrease ? Math.Max(5, _character.TraitMax) : 5)
                : 5);
            DisciplinesPage.InitializeCharacter(_character);
        }

        public void NavigateToTab(int index)
        {
            if (index < 0 || index >= SheetTabs.Items.Count) return;
            SheetTabs.SelectedIndex = index;
        }

        public bool TryGetDialogNavigation(string message, out string buttonText, out Action action)
        {
            buttonText = null;
            action = null;
            string value = (message ?? string.Empty).ToLowerInvariant();
            if (value.Contains("недостаточно свободных") || value.Contains("оплачиваются свободными") || value.Contains("после создания персонажа")) return false;

            if (value.Contains("дисциплин") && (value.Contains("первоначальн") || value.Contains("необходимо потратить") || value.Contains("не все точки")))
            {
                buttonText = "Открыть каталог";
                action = () => { NavigateToTab(2); DisciplinesPage.OpenDisciplineCatalog(); };
                return true;
            }
            if ((value.Contains("факт биографии") || value.Contains("факты биографии")) &&
                (value.Contains("добав") || value.Contains("выбер") || value.Contains("не все") || value.Contains("не указан") || value.Contains("распредели")))
            {
                buttonText = "Открыть выбор факта";
                action = () => { NavigateToTab(1); AdvantagesPage.OpenBioFactPicker(); };
                return true;
            }
            if (value.Contains("добродетел"))
            {
                buttonText = "Перейти к Добродетелям";
                action = () => { NavigateToTab(1); AdvantagesPage.ScrollToVirtues(); };
                return true;
            }
            if (value.Contains("человечност") || value.Contains("путь"))
            {
                buttonText = "Перейти к Человечности/Пути";
                action = () => { NavigateToTab(1); AdvantagesPage.ScrollToHumanityPath(); };
                return true;
            }
            if (value.Contains("клан"))
            {
                buttonText = "Перейти к листу";
                action = () => NavigateToTab(0);
                return true;
            }
            if (value.Contains("характеристик") || value.Contains("способност") || value.Contains("приоритет"))
            {
                buttonText = "Перейти к листу";
                action = () => NavigateToTab(0);
                return true;
            }
            return false;
        }

        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Z &&
                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0 &&
                (System.Windows.Input.Keyboard.Modifiers & (System.Windows.Input.ModifierKeys.Alt | System.Windows.Input.ModifierKeys.Shift)) == 0)
            {
                // Не перехватываем штатный Ctrl+Z внутри редактируемого текста.
                var focused = System.Windows.Input.Keyboard.FocusedElement as DependencyObject;
                bool textEditing = focused is System.Windows.Controls.Primitives.TextBoxBase ||
                    focused is System.Windows.Controls.PasswordBox ||
                    (focused != null && focused.GetType().Name == "TextArea");
                if (!textEditing && UndoService.TryUndo(this))
                {
                    e.Handled = true;
                    return;
                }
            }
            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) == 0) return;
            int target = -1;
            var pressedKey = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
            int keyCode = (int)pressedKey;
            if (keyCode >= (int)System.Windows.Input.Key.D1 && keyCode <= (int)System.Windows.Input.Key.D6)
                target = keyCode - (int)System.Windows.Input.Key.D1;
            else if (keyCode >= (int)System.Windows.Input.Key.NumPad1 && keyCode <= (int)System.Windows.Input.Key.NumPad6)
                target = keyCode - (int)System.Windows.Input.Key.NumPad1;
            else if (pressedKey == System.Windows.Input.Key.Right)
                target = (SheetTabs.SelectedIndex + 1) % 6;
            else if (pressedKey == System.Windows.Input.Key.Left)
                target = (SheetTabs.SelectedIndex + 5) % 6;
            if (target >= 0 && target < SheetTabs.Items.Count)
            {
                SheetTabs.SelectedIndex = target;
                e.Handled = true;
            }
        }

        private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateInitialPurchaseButton();
            if (!(e.AddedItems.Count > 0) || !(e.AddedItems[0] is TabItem tab)) return;
            if (!(tab.Content is FrameworkElement content)) return;

            // При переключении вкладки всегда показываем её начало
            content.Dispatcher.BeginInvoke(new Action(() =>
            {
                var scroll = FindVisualChild<ScrollViewer>(content);
                scroll?.ScrollToTop();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
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
