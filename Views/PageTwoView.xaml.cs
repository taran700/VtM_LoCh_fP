using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Threading;
using VtM_LoCh_fP.Controls;

namespace VtM_LoCh_fP.Views
{
    public partial class PageTwoView : UserControl
    {
        private CharacterProfile _character;
        private CharacterCreationState _subscribedCreationState;
        private bool _loadingHumanityPath;
        private bool _loadingBlood;
        private List<HumanityPathDefinition> _humanityPaths;
        private HumanityPathDefinition _selectedOfficialPath;
        public event Action InitialPurchaseProgressChanged;
        public event Func<string, string, int, int, bool> AdvancementCheck;
        public event Func<string, string, int, int, bool> VirtueAdvancementCheck;

        public PageTwoView()
        {
            InitializeComponent();
            AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(PageTwo_PreviewMouseDown), true);
            Loaded += (s, e) =>
            {
                EnsureVirtueTracks();
                EnsureWillpowerTrack();
                InitializeHumanityFromVirtues();
                if (_character != null)
                {
                    if (!_character.TemporaryWillpower.HasValue)
                        _character.TemporaryWillpower = GetWillpowerPermanent();
                    WillpowerTemporaryTrack.SetTrackValue(Math.Max(0, Math.Min(WillpowerTemporaryTrack.Count, _character.TemporaryWillpower.Value)));
                }
            };
            _humanityPaths = HumanityPathService.GetAll();
            HumanityPathCombo.ItemsSource = _humanityPaths;
            HumanityPathSuggestions.ItemsSource = _humanityPaths;
            HumanityPathInput.LostKeyboardFocus += CloseHumanityPathSuggestionsWhenFocusLeaves;
            HumanityPathSuggestions.LostKeyboardFocus += CloseHumanityPathSuggestionsWhenFocusLeaves;
            HumanityPathTrack.ValueChanged += HumanityPathTrack_ValueChanged;
            HumanityPathTrack.InfoLevelAdditionalTextProvider = level =>
            {
                if (_character == null) return null;
                string bearingName = _character.HumanityPathIsCustom
                    ? _character.CustomHumanityBearingName
                    : (_selectedOfficialPath == null ? null : _selectedOfficialPath.BearingName);
                string bearingEffect = _character.HumanityPathIsCustom
                    ? _character.CustomHumanityBearingEffect
                    : (_selectedOfficialPath == null ? null : _selectedOfficialPath.BearingEffect);
                if (string.IsNullOrWhiteSpace(bearingName) && string.IsNullOrWhiteSpace(bearingEffect)) return null;

                string bearing = "Столп: " + (bearingName ?? string.Empty).Trim() +
                    (string.IsNullOrWhiteSpace(bearingEffect) ? string.Empty : " — " + bearingEffect.Trim());
                string difficultyModifier = GetHumanityDifficultyModifier(level);
                return string.IsNullOrWhiteSpace(difficultyModifier)
                    ? bearing
                    : bearing + "  " + difficultyModifier;
            };
            var willpowerInfo = TrackDescriptionService.Find("Сила воли");
            if (willpowerInfo != null)
            {
                WillpowerPermanentTrack.InfoDescription = willpowerInfo.Description;
                WillpowerTemporaryTrack.InfoDescription = willpowerInfo.Description;
            }
            WillpowerPermanentTrack.InfoAdditionalTextProvider = GetWillpowerRecoveryInfo;
            WillpowerTemporaryTrack.InfoAdditionalTextProvider = GetWillpowerRecoveryInfo;
            HealthTrack.TrySpendBlood = amount =>
            {
                if (BloodTrack.Value < amount) return false;
                BloodTrack.SetTrackValue(BloodTrack.Value - amount);
                if (_character != null) _character.CurrentBlood = BloodTrack.Value;
                UpdateHealthHeader();
                return true;
            };
            HealthTrack.HealingMode = () => AppSettings.WoundHealingMode;
            HealthTrack.AllowAggravatedDamage = () => AggravatedDamageToggle != null && AggravatedDamageToggle.IsChecked == true;
            HealthTrack.StateChanged = SaveHealthStates;
            BloodTrack.ValueChanged += (s, e) =>
            {
                if (_character != null && !_loadingBlood) _character.CurrentBlood = BloodTrack.Value;
                UpdateHealthHeader();
            };
            WillpowerTemporaryTrack.ValueChanged += (s, e) =>
            {
                if (_character != null) _character.TemporaryWillpower = WillpowerTemporaryTrack.Value;
            };
            WillpowerPermanentTrack.ValueChanged += (s, e) =>
            {
                if (_character == null) return;
                int oldValue; _character.CreationState.OtherDots.TryGetValue("willpower_permanent", out oldValue);
                int nextValue = WillpowerPermanentTrack.Value;
                if (!_character.CreationState.ConfirmedInitialBlocks.Contains("virtues") && nextValue != oldValue)
                {
                    WillpowerPermanentTrack.SetTrackValue(oldValue);
                    ThemedDialog.Show(Window.GetWindow(this), "Сначала подтвердите распределение Добродетелей.", "Добродетели не подтверждены", MessageBoxButton.OK, MessageBoxImage.Information,
                        "Перейти к Добродетелям", () => VirtuesHeader?.BringIntoView());
                    return;
                }
                int courageFloor = GetVirtueRating("Смелость");
                if (nextValue < oldValue && nextValue <= courageFloor)
                {
                    var answer = ThemedDialog.Show(Window.GetWindow(this),
                        "Это значение достигает или опускается ниже уровня постоянной Силы воли, обеспеченного Доблестью (" + courageFloor + ").\nПродолжить снижение?",
                        "Предупреждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (answer != MessageBoxResult.Yes) { WillpowerPermanentTrack.SetTrackValue(oldValue); return; }
                }
                if (nextValue != oldValue && _character.CreationState.ConfirmedInitialBlocks.Contains("virtues") &&
                    AdvancementCheck != null && !AdvancementCheck("willpower", "Постоянная сила воли", oldValue, nextValue))
                { WillpowerPermanentTrack.SetTrackValue(oldValue); return; }
                _character.CreationState.OtherDots["willpower_permanent"] = nextValue;
            };
        }

        private void PageTwo_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            if (HumanityPathSuggestionsContainer != null &&
                !IsWithinPathElement(source, HumanityPathInput) &&
                !IsWithinPathElement(source, HumanityPathSuggestionsContainer))
                HumanityPathSuggestionsContainer.Visibility = Visibility.Collapsed;
        }

        private static bool IsWithinPathElement(DependencyObject source, DependencyObject target)
        {
            while (source != null)
            {
                if (ReferenceEquals(source, target)) return true;
                if (source is FrameworkContentElement contentElement)
                    source = contentElement.Parent;
                else if (source is Visual)
                    source = VisualTreeHelper.GetParent(source);
                else
                    source = LogicalTreeHelper.GetParent(source);
            }
            return false;
        }

        private void CloseHumanityPathSuggestionsWhenFocusLeaves(object sender, KeyboardFocusChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (HumanityPathInput.IsKeyboardFocusWithin || HumanityPathSuggestions.IsKeyboardFocusWithin) return;
                HumanityPathSuggestionsContainer.Visibility = Visibility.Collapsed;
            }), DispatcherPriority.Input);
        }

        public void InitializeCharacter(CharacterProfile character)
        {
            _character = character;
            if (_character.BiographyFacts == null)
                _character.BiographyFacts = new System.Collections.ObjectModel.ObservableCollection<BioFactEntry>();
            if (_character.CustomHumanityHierarchy == null)
                _character.CustomHumanityHierarchy = new List<HumanityPathSinEntry>();
            if (_character.HealthDamageStates == null)
                _character.HealthDamageStates = new System.Collections.ObjectModel.ObservableCollection<int>();
            if (_character.CreationState == null) _character.CreationState = new CharacterCreationState();
            if (_character.CreationState.OtherDots == null) _character.CreationState.OtherDots = new Dictionary<string, int>();
            ExperienceInput.InputText = _character.CreationState.ExperiencePoints.ToString();
            FreebiePointsInput.InputText = _character.CreationState.FreebiePoints.ToString();
            FreebiePointsInput.IsReadOnly = !IsFreeNeonate;
            ExperienceInput.InputTextChanged += (s, e) =>
            {
                if (_character == null) return;
                int value;
                if (int.TryParse(ExperienceInput.InputText, out value) && value >= 0)
                    _character.CreationState.ExperiencePoints = value;
            };
            FreebiePointsInput.InputTextChanged += (s, e) =>
            {
                if (_character == null || !IsFreeNeonate) return;
                int value;
                if (int.TryParse(FreebiePointsInput.InputText, out value) && value >= 0)
                    _character.CreationState.FreebiePoints = value;
            };
            if (_subscribedCreationState != null)
                _subscribedCreationState.PropertyChanged -= CreationState_PropertyChanged;
            _subscribedCreationState = _character.CreationState;
            _subscribedCreationState.PropertyChanged += CreationState_PropertyChanged;
            AggravatedDamageToggle.IsChecked = _character.AllowAggravatedDamage;
            if (_character.TemporaryWillpower.HasValue)
                WillpowerTemporaryTrack.SetTrackValue(Math.Max(0, Math.Min(WillpowerTemporaryTrack.Count, _character.TemporaryWillpower.Value)));
            HealthTrack.LoadStates(_character.HealthDamageStates);
            BloodTrack.Count = _character.BloodPoolMax > 0 ? _character.BloodPoolMax : 10;
            _loadingBlood = true;
            BloodTrack.SetTrackValue(Math.Max(0, Math.Min(BloodTrack.Count, _character.CurrentBlood)));
            _loadingBlood = false;
            UpdateHealthHeader();
            RenderBioFacts();
            LoadHumanityPath();
            UpdateAddBioFactButton();
            foreach (string virtueName in VirtuesTrackList.Names)
            {
                string key = "virtue:" + virtueName;
                if (!_character.CreationState.OtherDots.ContainsKey(key))
                    _character.CreationState.OtherDots[key] = CharacterCreationRules.GetCreationLimit("virtue_base");
            }
            if (!_character.CreationState.OtherDots.ContainsKey("willpower_permanent"))
                _character.CreationState.OtherDots["willpower_permanent"] = GetVirtueRating("Смелость");
            Dispatcher.BeginInvoke(new Action(() => { EnsureVirtueTracks(); EnsureWillpowerTrack(); }), DispatcherPriority.Loaded);
        }






        private void ExperiencePrices_Click(object sender, RoutedEventArgs e)
        {
            new PurchasePriceInfoWindow(Window.GetWindow(this), true).ShowDialog();
        }

        private void FreebiePrices_Click(object sender, RoutedEventArgs e)
        {
            new PurchasePriceInfoWindow(Window.GetWindow(this), false).ShowDialog();
        }

        private void CreationState_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_character == null) return;
            if (e.PropertyName == nameof(CharacterCreationState.FreebiePoints))
            {
                string value = _character.CreationState.FreebiePoints.ToString();
                if (FreebiePointsInput.InputText != value)
                    FreebiePointsInput.InputText = value;
                RefreshInitialBlockControls();
                UpdateAddBioFactButton();
                RefreshHumanityPathActionButtons();
            }
            else if (e.PropertyName == nameof(CharacterCreationState.ExperiencePoints))
            {
                string value = _character.CreationState.ExperiencePoints.ToString();
                if (ExperienceInput.InputText != value)
                    ExperienceInput.InputText = value;
            }
        }

        public void RefreshVirtueTracks()
        {
            EnsureVirtueTracks();
        }

        private void EnsureVirtueTracks()
        {
            if (_character == null || VirtuesTrackList == null) return;
            foreach (var track in FindTracks(VirtuesTrackList))
            {
                string key = "virtue:" + track.Label;
                int value;
                if (_character.CreationState.OtherDots.TryGetValue(key, out value))
                    track.SetTrackValue(value);
                else
                {
                    int baseDots = CharacterCreationRules.GetCreationLimit("virtue_base");
                    track.SetTrackValue(baseDots);
                    _character.CreationState.OtherDots[key] = baseDots;
                }
                track.ValueChanged -= VirtueTrack_ValueChanged;
                track.ValueChanged += VirtueTrack_ValueChanged;
            }
            RefreshInitialBlockControls();
            ApplyVirtuePoolLimits();
            InitialPurchaseProgressChanged?.Invoke();
        }

        private void VirtueTrack_ValueChanged(object sender, EventArgs e)
        {
            var track = sender as TrackControl;
            if (track == null || _character == null) return;
            string key = "virtue:" + track.Label;
            int previous;
            _character.CreationState.OtherDots.TryGetValue(key, out previous);
            bool virtueBlockConfirmed = _character.CreationState.ConfirmedInitialBlocks.Contains("virtues");
            bool virtueBlockWasConfirmed = _character.CreationState.InitialVirtueBlockEverConfirmed;
            if (track.Value != previous && (virtueBlockConfirmed || (virtueBlockWasConfirmed && track.Value < previous)) &&
                VirtueAdvancementCheck != null && !VirtueAdvancementCheck("virtue", track.Label, previous, track.Value))
            { track.SetTrackValue(previous); return; }
            if (!virtueBlockConfirmed)
            {
                int remaining = Math.Max(0, CharacterCreationRules.GetCreationLimit("virtues") - GetVirtueExtraDots());
                int paidDots = virtueBlockWasConfirmed && _character.CreationState.PurchaseLedger != null
                    ? _character.CreationState.PurchaseLedger.Where(t => !t.IsCancelled && string.Equals(t.ItemName, track.Label, StringComparison.OrdinalIgnoreCase) && t.Resource != "initial_pool")
                        .Sum(t => Math.Max(0, t.NewValue - t.PreviousValue)) : 0;
                int allowed = previous + remaining + (track.Value > previous ? paidDots : 0);
                int clamped = Math.Min(track.Value, allowed);
                if (clamped != track.Value) track.SetTrackValue(clamped);
                if (virtueBlockWasConfirmed)
                    _character.CreationState.InitialVirtueRatings[track.Label] = Math.Max(CharacterCreationRules.GetCreationLimit("virtue_base"), track.Value - paidDots);
            }
            _character.CreationState.OtherDots[key] = track.Value;
            RefreshInitialBlockControls();
            ApplyVirtuePoolLimits();
            InitialPurchaseProgressChanged?.Invoke();
        }

        private void InitializeHumanityFromVirtues()
        {
            if (_character == null) return;
            if (_character.CreationState.ConfirmedInitialBlocks.Contains("virtues"))
            {
                if (_character.CreationState.HumanityPathConfirmedRating < 0 &&
                    CharacterCreationRules.IsHumanityPathLimitActive(_character) &&
                    _character.HumanityPathRating > 5 && !HasExperiencePathPurchaseAboveFive())
                    SetHumanityPathRating(Math.Min(5, GetCurrentPathVirtueRating()));
                return;
            }
            int initialRating = GetCurrentPathVirtueRating();
            if (CharacterCreationRules.IsHumanityPathLimitActive(_character)) initialRating = Math.Min(5, initialRating);
            SetHumanityPathRating(initialRating);
            RefreshHumanityPathActionButtons();
        }

        private int GetCurrentPathVirtueRating()
        {
            var names = GetVirtueNames();
            if (names.Count < 2) return Math.Min(10, _character == null ? 0 : _character.HumanityPathRating);
            var initial = _character == null ? null : _character.CreationState.InitialVirtueRatings;
            int first, second;
            if (initial != null && initial.TryGetValue(names[0], out first) && initial.TryGetValue(names[1], out second))
                return Math.Min(10, first + second);
            return Math.Min(10, GetVirtueRating(names[0]) + GetVirtueRating(names[1]));
        }

        public void RefreshHumanityPathLimitSetting()
        {
            if (_character == null || CharacterCreationRules.IsHumanityPath(_character)) return;

            // При изменении настройки пересчитываем именно Путь по первичным
            // Добродетелям даже у уже подтверждённых/мигрировавших персонажей.
            // Сохраняем оплаченные повышения: бесплатные уровни выше нового
            // лимита отсекаются, а повышения за опыт остаются исключением.
            RecalculatePrimaryVirtueDerivedValues(true, true);
        }

        private void RefreshPathRatingAfterSelection()
        {
            if (_character == null) return;
            if (!_character.CreationState.InitialPurchaseConfirmed || CharacterCreationRules.IsHumanityPath(_character))
            {
                RecalculatePrimaryVirtueDerivedValues();
                if (!_character.CreationState.InitialVirtueBlockEverConfirmed)
                    InitializeHumanityFromVirtues();
                return;
            }

            // При смене/редактировании Пути используем только первичные
            // значения его двух добродетелей, а не текущие значения после опыта.
            if (!HasExperiencePathPurchaseAboveFive())
                RecalculatePrimaryVirtueDerivedValues();
        }

        private bool HasExperiencePathPurchaseAboveFive()
        {
            if (_character == null || _character.HumanityPathRating <= 5) return false;
            var state = _character.CreationState;
            if (state == null) return false;

            // Считаем превышение легитимным только тогда, когда КАЖДЫЙ уровень
            // от 6 до текущего рейтинга покрыт отдельной покупкой за опыт.
            // Одной записи о покупке за опыт выше 5 недостаточно: смешанные
            // покупки (опыт + свободные пункты) не должны обходить миграцию.
            var ledgerPath = state.PurchaseLedger == null
                ? new List<PurchaseTransaction>()
                : state.PurchaseLedger.Where(t => !t.IsCancelled && t.Cost > 0 &&
                    (string.Equals(t.Category, "humanity_path", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(t.Block, "Путь", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(t.Block, "Человечность", StringComparison.OrdinalIgnoreCase))).ToList();

            if (ledgerPath.Count > 0)
            {
                for (int level = 6; level <= _character.HumanityPathRating; level++)
                {
                    bool paidByExperience = ledgerPath.Any(t =>
                        string.Equals(t.Resource, "experience", StringComparison.OrdinalIgnoreCase) &&
                        Math.Min(t.PreviousValue, t.NewValue) < level &&
                        Math.Max(t.PreviousValue, t.NewValue) >= level);
                    if (!paidByExperience) return false;
                }
                return true;
            }

            // Совместимость со старыми листами без PurchaseLedger.
            var legacy = state.ExperiencePurchases == null
                ? new List<ExperiencePurchase>()
                : state.ExperiencePurchases.Where(t => t.Cost > 0 &&
                    (string.Equals(t.Category, "humanity_path", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(t.Category, "path", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(t.Category, "путь", StringComparison.OrdinalIgnoreCase))).ToList();
            for (int level = 6; level <= _character.HumanityPathRating; level++)
            {
                bool paidByExperience = legacy.Any(t =>
                    Math.Min(t.PreviousValue, t.NewValue) < level &&
                    Math.Max(t.PreviousValue, t.NewValue) >= level);
                if (!paidByExperience) return false;
            }
            return legacy.Count > 0;
        }

        private void ShowHumanityPathLimitWarning()
        {
            if (!CharacterCreationRules.IsHumanityPathLimitActive(_character)) return;
            string message = "Чтобы отбросить Человечность и принять Путь, постоянная Сила воли персонажа должна быть не менее 5: при меньшем значении ему не хватает целеустремлённости и решимости отказаться от Человечности. Для любого Пути, кроме Человечности, при создании рейтинг не может быть выше 5. Повышение выше 5 за счёт добродетелей, свободных пунктов и обычного изменения шкалы заблокировано. Опыт остаётся исключением и позволяет повышать рейтинг выше 5. Если вернуться к Человечности, рейтинг будет пересчитан по двум её добродетелям.";
            if (CharacterCreationRules.IsFledgling(_character))
            {
                ThemedDialog.Show(Window.GetWindow(this), message + " В режиме Птенца это ограничение обязательно и не отключается.",
                    "Ограничение рейтинга Пути", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ThemedDialog.Show(Window.GetWindow(this), message,
                "Ограничение рейтинга Пути", MessageBoxButton.OK, MessageBoxImage.Warning,
                "Отключить ограничение", () =>
                {
                    // Настройка относится только к текущему листу персонажа.
                    _character.HumanityPathLimitEnabled = false;
                    CharacterSelectionWindow.SaveCharacters();
                    RefreshHumanityPathLimitSetting();
                });
        }

        private int GetHumanityVirtueFloor()
        {
            return Math.Min(10, GetVirtueRating("Совесть") + GetVirtueRating("Самоконтроль"));
        }

        private void EnsureWillpowerTrack()
        {
            if (_character == null || WillpowerPermanentTrack == null) return;
            int value;
            if (_character.CreationState.OtherDots.TryGetValue("willpower_permanent", out value))
                WillpowerPermanentTrack.SetTrackValue(value);
        }

        public List<string> GetVirtueNames()
        {
            return VirtuesTrackList.Names.ToList();
        }

        public int GetVirtueRating(string name)
        {
            int value;
            return _character != null && _character.CreationState.OtherDots.TryGetValue("virtue:" + name, out value) ? value : 1;
        }

        public void SetVirtueRating(string name, int value)
        {
            if (_character == null) return;
            int previous = GetVirtueRating(name);
            if (_character.CreationState.ConfirmedInitialBlocks.Contains("virtues") && value != previous &&
                VirtueAdvancementCheck != null && !VirtueAdvancementCheck("virtue", name, previous, value)) return;
            if (!_character.CreationState.ConfirmedInitialBlocks.Contains("virtues"))
            {
                int baseTotal = VirtuesTrackList.Names.Count * CharacterCreationRules.GetCreationLimit("virtue_base");
                int remaining = Math.Max(0, CharacterCreationRules.GetCreationLimit("virtues") - GetVirtueExtraDots());
                value = Math.Min(value, previous + remaining);
            }
            _character.CreationState.OtherDots["virtue:" + name] = value;
            foreach (var track in FindTracks(VirtuesTrackList))
                if (string.Equals(track.Label, name, StringComparison.OrdinalIgnoreCase)) track.SetTrackValue(value);
            InitialPurchaseProgressChanged?.Invoke();
        }

        public int GetWillpowerPermanent()
        {
            int value;
            return _character != null && _character.CreationState.OtherDots.TryGetValue("willpower_permanent", out value) ? value : 0;
        }

        public void SetWillpowerPermanent(int value)
        {
            if (_character == null) return;
            _character.CreationState.OtherDots["willpower_permanent"] = value;
            WillpowerPermanentTrack.SetTrackValue(value);
        }

        public void RecalculatePrimaryVirtueDerivedValues(bool forceConfirmed = false, bool preservePathPurchases = false)
        {
            if (_character == null) return;
            var state = _character.CreationState;
            if (state == null) return;

            // При обычном открытии подтверждённый Путь не пересчитываем.
            // При повторном подтверждении Добродетелей пересобираем его явно,
            // сохраняя уже оплаченные повышения.
            if (!forceConfirmed && state.HumanityPathConfirmedRating >= 0) return;

            var initial = state.InitialVirtueRatings;
            bool usePrimarySnapshot = state.InitialVirtueBlockEverConfirmed ||
                (initial != null && initial.Count > 0);

            int courage;
            if (usePrimarySnapshot && initial != null && initial.TryGetValue("Смелость", out courage))
                SetWillpowerPermanent(courage);

            var names = GetVirtueNames();
            if (names.Count < 2) return;

            int first = GetPrimaryVirtueRating(names[0], usePrimarySnapshot, initial);
            int second = GetPrimaryVirtueRating(names[1], usePrimarySnapshot, initial);
            int primaryRating = Math.Min(10, first + second);
            bool limited = CharacterCreationRules.IsHumanityPathLimitActive(_character);
            if (limited) primaryRating = Math.Min(5, primaryRating);

            int nextPath = primaryRating;
            int refundedFreebies = 0;
            int refundedExperience = 0;

            if (preservePathPurchases)
            {
                if (state.PurchaseLedger == null)
                    state.PurchaseLedger = new List<PurchaseTransaction>();

                // Для старых листов, где журнал ещё не был материализован,
                // переносим записи о покупках Пути в единый журнал.
                bool hasPathLedger = state.PurchaseLedger.Any(t => !t.IsCancelled &&
                    (string.Equals(t.Category, "humanity_path", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(t.Block, "Путь", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(t.Block, "Человечность", StringComparison.OrdinalIgnoreCase)));
                if (!hasPathLedger)
                {
                    foreach (var p in state.FreebiePurchases ?? new List<CreationPurchase>())
                    {
                        if (!string.Equals(p.Block, "Путь", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(p.Block, "Человечность", StringComparison.OrdinalIgnoreCase)) continue;
                        int span = Math.Max(0, p.NewValue - p.PreviousValue);
                        int unitCost = span > 0 ? p.Cost / span : 0;
                        for (int level = 0; level < span; level++)
                            state.PurchaseLedger.Add(new PurchaseTransaction {
                                Order = state.NextPurchaseOrder++, Block = p.Block, Category = "humanity_path",
                                ItemId = p.ItemId, ItemName = p.ItemName,
                                PreviousValue = p.PreviousValue + level, NewValue = p.PreviousValue + level + 1,
                                Resource = "freebie", Cost = unitCost, Source = "Legacy"
                            });
                    }
                    foreach (var p in state.ExperiencePurchases ?? new List<ExperiencePurchase>())
                    {
                        if (!string.Equals(p.Category, "humanity_path", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(p.Category, "path", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(p.Category, "путь", StringComparison.OrdinalIgnoreCase)) continue;
                        int span = Math.Max(0, p.NewValue - p.PreviousValue);
                        int assigned = 0;
                        for (int level = 0; level < span; level++)
                        {
                            int each = level == span - 1 ? p.Cost - assigned : p.Cost / span;
                            assigned += each;
                            state.PurchaseLedger.Add(new PurchaseTransaction {
                                Order = state.NextPurchaseOrder++, Block = "Путь", Category = "humanity_path",
                                ItemId = p.ItemName, ItemName = p.ItemName,
                                PreviousValue = p.PreviousValue + level, NewValue = p.PreviousValue + level + 1,
                                Resource = "experience", Cost = each, Source = "Legacy"
                            });
                        }
                    }
                }

                var active = state.PurchaseLedger.Where(t => !t.IsCancelled &&
                    (string.Equals(t.Category, "humanity_path", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(t.Block, "Путь", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(t.Block, "Человечность", StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(t => string.Equals(t.Resource, "freebie", StringComparison.OrdinalIgnoreCase) ? 0 :
                                  string.Equals(t.Resource, "experience", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
                    .ThenBy(t => t.Order).ToList();

                // Сначала архивируем прежние диапазоны. Новые записи ниже будут
                // привязаны к новому рейтингу от Добродетелей.
                foreach (var transaction in active) transaction.IsCancelled = true;

                int freebieLimit = limited ? Math.Max(0, 5 - primaryRating) : Math.Max(0, 10 - primaryRating);
                int retainedFreebieDots = 0;
                int retainedExperienceDots = 0;

                foreach (var transaction in active)
                {
                    int span = Math.Max(0, transaction.NewValue - transaction.PreviousValue);
                    if (span == 0) continue;
                    int unitCost = transaction.Cost / span;
                    bool isFreebie = string.Equals(transaction.Resource, "freebie", StringComparison.OrdinalIgnoreCase);
                    bool isExperience = string.Equals(transaction.Resource, "experience", StringComparison.OrdinalIgnoreCase);

                    for (int dot = 0; dot < span; dot++)
                    {
                        int cost = dot == span - 1 ? transaction.Cost - unitCost * (span - 1) : unitCost;
                        bool retain = false;
                        if (isFreebie && retainedFreebieDots < freebieLimit &&
                            nextPath < 10)
                        {
                            retain = true;
                            retainedFreebieDots++;
                        }
                        else if (isFreebie)
                        {
                            refundedFreebies += cost;
                        }
                        else if (isExperience && nextPath < 10)
                        {
                            retain = true;
                            retainedExperienceDots++;
                        }
                        else if (isExperience)
                        {
                            refundedExperience += cost;
                        }

                        if (retain)
                        {
                            int previous = nextPath;
                            nextPath++;
                            state.PurchaseLedger.Add(new PurchaseTransaction {
                                Order = state.NextPurchaseOrder++, Block = transaction.Block,
                                Category = "humanity_path", ItemId = transaction.ItemId,
                                ItemName = transaction.ItemName, PreviousValue = previous, NewValue = nextPath,
                                Resource = transaction.Resource, Cost = cost,
                                RefundLocked = transaction.RefundLocked,
                                AffectsVirtueDerivedValues = transaction.AffectsVirtueDerivedValues,
                                DependencyGroupId = transaction.DependencyGroupId,
                                Source = "VirtueRebuild"
                            });
                        }
                    }
                }

                state.FreebiePoints += refundedFreebies;
                state.ExperiencePoints += refundedExperience;
            }

            SetHumanityPathRating(nextPath);

            if (preservePathPurchases)
            {
                state.HumanityPathConfirmedRating = nextPath;
                state.HumanityPathPendingConfirmation = false;
                state.HumanityPathPendingPreviousRating = nextPath;
                if (refundedFreebies > 0 || refundedExperience > 0)
                {
                    var message = "Путь/Человечность пересобран по новым Добродетелям.\n" +
                        "Новый рейтинг: " + nextPath + ".";
                    if (refundedFreebies > 0) message += "\nВозвращено свободных пунктов: " + refundedFreebies + ".";
                    if (refundedExperience > 0) message += "\nВозвращено опыта: " + refundedExperience + ".";
                    ThemedDialog.Show(Window.GetWindow(this), message, "Пересборка Пути/Человечности",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private int GetPrimaryVirtueRating(string name, bool usePrimarySnapshot, Dictionary<string, int> initial)
        {
            int value;
            if (usePrimarySnapshot)
                return initial != null && initial.TryGetValue(name, out value)
                    ? value : CharacterCreationRules.GetCreationLimit("virtue_base");
            return GetVirtueRating(name);
        }

        public void SetHumanityPathRating(int value)
        {
            if (_character == null) return;
            _character.HumanityPathRating = Math.Max(0, Math.Min(10, value));
            HumanityPathTrack.SetTrackValue(_character.HumanityPathRating);
            RefreshHumanityPathActionButtons();
        }

        // Вызывается только после фактической покупки уровня за опыт/свободные пункты.
        public void SetHumanityPathRatingFromAdvancement(int value)
        {
            if (_character == null) return;
            int previous = _character.HumanityPathRating;
            if (value > previous)
            {
                var state = _character.CreationState;
                if (state.HumanityPathConfirmedRating < 0 && state.ConfirmedInitialBlocks.Contains("virtues"))
                    state.HumanityPathConfirmedRating = previous;
                if (!state.HumanityPathPendingConfirmation)
                    state.HumanityPathPendingPreviousRating = previous;
                state.HumanityPathPendingConfirmation = true;
            }
            SetHumanityPathRating(value);
        }

        private void RefreshHumanityPathActionButtons()
        {
            if (_character == null || ConfirmHumanityPathButton == null || DegradeHumanityPathButton == null) return;
            var state = _character.CreationState;
            bool virtuesConfirmed = state != null && state.ConfirmedInitialBlocks != null &&
                state.ConfirmedInitialBlocks.Contains("virtues");
            if (virtuesConfirmed && state.HumanityPathConfirmedRating < 0)
                state.HumanityPathConfirmedRating = _character.HumanityPathRating;
            bool pending = state != null && state.HumanityPathPendingConfirmation;
            ConfirmHumanityPathButton.Visibility = virtuesConfirmed && pending ? Visibility.Visible : Visibility.Collapsed;
            bool hasFreebiePoints = state != null && state.FreebiePoints > 0;
            DegradeHumanityPathButton.Visibility = virtuesConfirmed && !pending && !hasFreebiePoints && _character.HumanityPathRating > 0
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ConfirmHumanityPath_Click(object sender, RoutedEventArgs e)
        {
            if (_character == null) return;
            var state = _character.CreationState;
            if (!state.HumanityPathPendingConfirmation || !state.ConfirmedInitialBlocks.Contains("virtues")) return;
            const string warning = "Вы собираетесь подтвердить повышение пунктов в Пути/Человечности, это необратимое действие. Вложенные очки опыта нельзя будет вернуть. Подтверждайте действие, только если ваш персонаж действительно готов к этому шагу и вы уверены в выборе.";
            if (!ShowHumanityPathConfirmation(warning, "Подтверждение распределения", "Ой, стой", "Я понимаю что творю")) return;

            if (state.PurchaseLedger != null)
                foreach (var transaction in state.PurchaseLedger.Where(t => !t.IsCancelled &&
                    string.Equals(t.Category, "humanity_path", StringComparison.OrdinalIgnoreCase) &&
                    t.NewValue > state.HumanityPathPendingPreviousRating && t.NewValue <= _character.HumanityPathRating))
                    transaction.RefundLocked = true;

            state.HumanityPathConfirmedRating = Math.Max(state.HumanityPathConfirmedRating, _character.HumanityPathRating);
            state.HumanityPathPendingConfirmation = false;
            state.HumanityPathPendingPreviousRating = _character.HumanityPathRating;
            UndoService.Clear();
            CharacterSelectionWindow.SaveCharacters();
            RefreshHumanityPathActionButtons();
        }

        private void DegradeHumanityPath_Click(object sender, RoutedEventArgs e)
        {
            if (_character == null) return;
            var state = _character.CreationState;
            if (state.HumanityPathPendingConfirmation || !state.ConfirmedInitialBlocks.Contains("virtues") || state.FreebiePoints > 0) return;
            const string warning = "Зверь внутри вас набирает силу. Утраченную Человечность или рейтинг Пути нельзя вернуть простым отдыхом или кровью. Восстановление потребует долгого духовного труда, раскаяния (или строгого следования этике Пути), одобрения Рассказчика и затрат опыта. На нуле вы навсегда станете бездумным и безвольным чудовищем.";
            if (!ShowHumanityPathConfirmation(warning, "Деградация", "Отмена", "Я деградировал")) return;
            if (_character.HumanityPathRating <= 0) return;
            SetHumanityPathRating(_character.HumanityPathRating - 1);
            state.HumanityPathDegradationCount++;
            // Подтверждённый максимум не уменьшаем: восстановление до него всегда стоит опыт.
            UndoService.Clear();
            CharacterSelectionWindow.SaveCharacters();
            RefreshHumanityPathActionButtons();
        }

        private bool ShowHumanityPathConfirmation(string message, string title, string cancelText, string confirmText)
        {
            var window = HumanityPathEditorWindow.CreateWindow(Window.GetWindow(this), title, 580, 0);
            window.ResizeMode = ResizeMode.NoResize;
            window.MinHeight = 0;
            window.Height = double.NaN;
            window.SizeToContent = SizeToContent.Height;
            var root = new Grid { Margin = new Thickness(22) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var text = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 15,
                Margin = new Thickness(0, 0, 0, 22),
                MaxWidth = 520
            };
            text.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            Grid.SetRow(text, 0);
            root.Children.Add(text);
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var cancel = HumanityPathEditorWindow.MakeButton(cancelText, false);
            var confirm = HumanityPathEditorWindow.MakeButton(confirmText, true);
            bool accepted = false;
            cancel.Click += (s, e) => window.Close();
            confirm.Click += (s, e) => { accepted = true; window.Close(); };
            buttons.Children.Add(cancel);
            buttons.Children.Add(confirm);
            Grid.SetRow(buttons, 1);
            root.Children.Add(buttons);
            window.Content = root;
            window.ShowDialog();
            return accepted;
        }

        private int GetVirtueTotal()
        {
            int total = 0;
            foreach (string name in VirtuesTrackList.Names)
                total += GetVirtueRating(name);
            return total;
        }

        public int GetVirtueExtraDots()
        {
            if (_character == null) return 0;
            int total = 0;
            bool useInitialSnapshot = _character.CreationState.InitialVirtueBlockEverConfirmed;
            foreach (string virtueName in VirtuesTrackList.Names)
            {
                int value;
                if (useInitialSnapshot && _character.CreationState.InitialVirtueRatings.TryGetValue(virtueName, out value)) total += value;
                else if (_character.CreationState.OtherDots.TryGetValue("virtue:" + virtueName, out value)) total += value;
                else total += CharacterCreationRules.GetCreationLimit("virtue_base");
            }
            return Math.Max(0, total - VirtuesTrackList.Names.Count * CharacterCreationRules.GetCreationLimit("virtue_base"));
        }

        private static IEnumerable<TrackControl> FindTracks(DependencyObject parent)
        {
            if (parent == null) yield break;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is TrackControl track) yield return track;
                foreach (var nested in FindTracks(child)) yield return nested;
            }
        }

        public void ApplyTraitPointLimit(int max)
        {
            VirtuesTrackList.Count = max;
        }

        private string GetWillpowerRecoveryInfo()
        {
            if (_character == null) return string.Empty;
            var conditions = new List<string>();
            string nature = _character.NatureSelectedFromSuggestions
                ? ArchetypeService.FindByName(_character.Nature)?.RegainWillpowerCondition
                : _character.NatureCustomWillpowerRecovery;
            string demeanor = _character.DemeanorSelectedFromSuggestions
                ? ArchetypeService.FindByName(_character.Demeanor)?.RegainWillpowerCondition
                : _character.DemeanorCustomWillpowerRecovery;
            if (!string.IsNullOrWhiteSpace(nature)) conditions.Add("Натура («" + _character.Nature + "»): " + nature.Trim());
            if (!string.IsNullOrWhiteSpace(demeanor)) conditions.Add("Маска («" + _character.Demeanor + "»): " + demeanor.Trim());
            return conditions.Count == 0
                ? "Условия восстановления не указаны. Заполните их в описании Натуры и Маски."
                : string.Join("\n\n", conditions);
        }

        private bool IsFreeNeonate
        {
            get { return string.Equals(_character == null ? null : _character.CreationMode, "Свободный Неонат", StringComparison.OrdinalIgnoreCase); }
        }

        private void LoadHumanityPath()
        {
            if (_character == null) return;
            _loadingHumanityPath = true;
            HumanityPathCombo.Visibility = IsFreeNeonate ? Visibility.Collapsed : Visibility.Visible;
            HumanityPathNeonatePicker.Visibility = IsFreeNeonate ? Visibility.Visible : Visibility.Collapsed;
            if (_character.HumanityPathIsCustom)
            {
                _selectedOfficialPath = null;
                HumanityPathCombo.SelectedItem = null;
                HumanityPathInput.Text = _character.CustomHumanityPathName ?? string.Empty;
                UpdateCustomHumanityPathInfo();
                EditHumanityPathButton.Visibility = Visibility.Visible;
            }
            else
            {
                var selected = HumanityPathService.Find(_character.HumanityPathId) ?? HumanityPathService.Find("humanity");
                _selectedOfficialPath = selected;
                HumanityPathCombo.SelectedValue = selected == null ? null : selected.Id;
                HumanityPathInput.Text = selected == null ? string.Empty : selected.Name;
                if (selected != null) UpdateHumanityPathInfo(selected);
                EditHumanityPathButton.Visibility = Visibility.Collapsed;
            }
            HumanityPathSuggestionsContainer.Visibility = Visibility.Collapsed;
            CreateHumanityPathButton.Visibility = Visibility.Collapsed;
            HumanityPathTrack.SetTrackValue(_character.HumanityPathRating);
            _loadingHumanityPath = false;
            ApplyVirtueNames(_character.HumanityPathIsCustom ? _character.CustomHumanityFirstVirtue : (_selectedOfficialPath == null ? "Совесть" : _selectedOfficialPath.FirstVirtue), _character.HumanityPathIsCustom ? _character.CustomHumanitySecondVirtue : (_selectedOfficialPath == null ? "Самоконтроль" : _selectedOfficialPath.SecondVirtue));
        }

        private void HumanityPathCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loadingHumanityPath || _character == null) return;
            var selected = HumanityPathCombo.SelectedItem as HumanityPathDefinition;
            if (selected == null) return;
            ApplyOfficialPath(selected);
        }

        private void ApplyOfficialPath(HumanityPathDefinition path)
        {
            string previousPathId = _character.HumanityPathId;
            bool previousWasCustom = _character.HumanityPathIsCustom;
            bool previousWasHumanity = !previousWasCustom &&
                string.Equals(previousPathId, "humanity", StringComparison.OrdinalIgnoreCase);
            TransferPathVirtues(path.FirstVirtue, path.SecondVirtue);
            _loadingHumanityPath = true;
            _selectedOfficialPath = path;
            _character.HumanityPathIsCustom = false;
            _character.HumanityPathId = path.Id;
            _character.CustomHumanityPathName = null;
            _character.CustomHumanityPathDescription = null;
            _character.CustomHumanityBearingName = null;
            _character.CustomHumanityBearingEffect = null;
            _character.CustomHumanityEdicts = null;
            _character.CustomHumanityHierarchy = new List<HumanityPathSinEntry>();
            HumanityPathName.Text = path.Name;
            HumanityPathInput.Text = path.Name;
            HumanityPathCombo.SelectedValue = path.Id;
            UpdateHumanityPathInfo(path);
            ApplyVirtueNames(path.FirstVirtue, path.SecondVirtue);
            EditHumanityPathButton.Visibility = Visibility.Collapsed;
            HumanityPathSuggestionsContainer.Visibility = Visibility.Collapsed;
            CreateHumanityPathButton.Visibility = Visibility.Collapsed;
            _loadingHumanityPath = false;
            RefreshPathRatingAfterSelection();
            if (previousWasHumanity && !CharacterCreationRules.IsHumanityPath(_character))
                ShowHumanityPathLimitWarning();
        }

        private void UpdateHumanityPathInfo(HumanityPathDefinition path)
        {
            if (path == null) return;
            HumanityPathName.Text = path.Name;
            HumanityPathTrack.InfoDescription = path.Description;
            HumanityPathTrack.InfoLevels = path.Levels ?? new List<TrackLevelDescription>();
            HumanitySinTableButton.IsEnabled = path.Hierarchy != null && path.Hierarchy.Count > 0;
        }

        private void UpdateCustomHumanityPathInfo()
        {
            ApplyVirtueNames(_character.CustomHumanityFirstVirtue, _character.CustomHumanitySecondVirtue);
            HumanityPathName.Text = _character.CustomHumanityPathName ?? string.Empty;
            HumanityPathTrack.InfoDescription = _character.CustomHumanityPathDescription;
            var levels = new List<TrackLevelDescription>();
            foreach (var row in _character.CustomHumanityHierarchy ?? new List<HumanityPathSinEntry>())
                levels.Add(new TrackLevelDescription { Level = row.Level, Rank = "Грех", Description = row.Sin ?? string.Empty });
            HumanityPathTrack.InfoLevels = levels;
            HumanitySinTableButton.IsEnabled = _character.CustomHumanityHierarchy != null && _character.CustomHumanityHierarchy.Count > 0;
        }

        private static string GetHumanityDifficultyModifier(int level)
        {
            // Таблица модификаторов Человечности/Пути: показываем только
            // значение для конкретного уровня, на который наведён курсор.
            if (level >= 10) return "Модификатор стола: −2 к сложности";
            if (level >= 8) return "Модификатор стола: −1 к сложности";
            if (level >= 4) return "Модификатора нет";
            if (level >= 2) return "Модификатор стола: +1 к сложности";
            if (level == 1) return "Модификатор стола: +2 к сложности";
            return null;
        }

        private string BuildPathTooltip(string name, string description, string bearingName, string bearingEffect, string edicts)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(name)) parts.Add(name);
            if (!string.IsNullOrWhiteSpace(description)) parts.Add(description);
            if (!string.IsNullOrWhiteSpace(bearingName)) parts.Add("Столп: " + bearingName + (string.IsNullOrWhiteSpace(bearingEffect) ? string.Empty : " — " + bearingEffect));
            if (!string.IsNullOrWhiteSpace(edicts))
            {
                var markedEdicts = edicts.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => "◆  " + line.Trim());
                parts.Add("Эдикты:\n" + string.Join("\n", markedEdicts));
            }
            return string.Join("\n\n", parts);
        }

        private void HumanityPathInfo_Click(object sender, RoutedEventArgs e)
        {
            if (_character == null) return;
            string title = HumanityPathName.Text;
            string info = _character.HumanityPathIsCustom
                ? BuildPathTooltip(_character.CustomHumanityPathName, _character.CustomHumanityPathDescription, _character.CustomHumanityBearingName, _character.CustomHumanityBearingEffect, _character.CustomHumanityEdicts)
                : BuildPathTooltip(_selectedOfficialPath == null ? title : _selectedOfficialPath.Name, _selectedOfficialPath == null ? null : _selectedOfficialPath.Description, _selectedOfficialPath == null ? null : _selectedOfficialPath.BearingName, _selectedOfficialPath == null ? null : _selectedOfficialPath.BearingEffect, _selectedOfficialPath == null ? null : _selectedOfficialPath.Edicts);
            HumanityPathInfoWindow.Show(Window.GetWindow(this), title, info);
        }


        private void TransferPathVirtues(string newFirst, string newSecond)
        {
            if (_character == null || _character.CreationState == null) return;
            var activePath = _selectedOfficialPath ?? HumanityPathService.Find(_character.HumanityPathId) ?? HumanityPathService.Find("humanity");
            string oldFirst = _character.HumanityPathIsCustom
                ? _character.CustomHumanityFirstVirtue : (activePath == null ? "Совесть" : activePath.FirstVirtue);
            string oldSecond = _character.HumanityPathIsCustom
                ? _character.CustomHumanitySecondVirtue : (activePath == null ? "Самоконтроль" : activePath.SecondVirtue);

            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(oldFirst) && !string.IsNullOrWhiteSpace(newFirst) &&
                !string.Equals(oldFirst, newFirst, StringComparison.OrdinalIgnoreCase)) mapping[oldFirst] = newFirst;
            if (!string.IsNullOrWhiteSpace(oldSecond) && !string.IsNullOrWhiteSpace(newSecond) &&
                !string.Equals(oldSecond, newSecond, StringComparison.OrdinalIgnoreCase)) mapping[oldSecond] = newSecond;
            if (mapping.Count == 0) return;

            var state = _character.CreationState;
            var currentDots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var initialDots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in mapping)
            {
                int value;
                if (state.OtherDots != null && state.OtherDots.TryGetValue("virtue:" + pair.Key, out value)) currentDots[pair.Key] = value;
                if (state.InitialVirtueRatings != null && state.InitialVirtueRatings.TryGetValue(pair.Key, out value)) initialDots[pair.Key] = value;
            }
            int virtueBase = CharacterCreationRules.GetCreationLimit("virtue_base");
            if (state.OtherDots != null)
            {
                foreach (var oldName in mapping.Keys) state.OtherDots.Remove("virtue:" + oldName);
                foreach (var pair in mapping)
                {
                    int value;
                    state.OtherDots["virtue:" + pair.Value] = currentDots.TryGetValue(pair.Key, out value) ? value : virtueBase;
                }
            }
            bool hasInitialVirtueSnapshot = state.InitialVirtueBlockEverConfirmed ||
                (state.InitialVirtueRatings != null && state.InitialVirtueRatings.Count > 0);
            if (hasInitialVirtueSnapshot && state.InitialVirtueRatings != null)
            {
                foreach (var oldName in mapping.Keys) state.InitialVirtueRatings.Remove(oldName);
                foreach (var pair in mapping)
                {
                    int value;
                    state.InitialVirtueRatings[pair.Value] = initialDots.TryGetValue(pair.Key, out value) ? value : virtueBase;
                }
            }

            Func<string, string> rename = name =>
            {
                string target;
                return name != null && mapping.TryGetValue(name, out target) ? target : name;
            };
            if (state.PurchaseLedger != null)
                foreach (var purchase in state.PurchaseLedger.Where(x => !x.IsCancelled && string.Equals(x.Category, "virtue", StringComparison.OrdinalIgnoreCase)))
                { purchase.ItemName = rename(purchase.ItemName); purchase.ItemId = rename(purchase.ItemId); }
            if (state.InitialPurchases != null)
                foreach (var purchase in state.InitialPurchases.Where(x => x.Block == "Добродетели"))
                { purchase.ItemName = rename(purchase.ItemName); purchase.ItemId = rename(purchase.ItemId); }
            if (state.FreebiePurchases != null)
                foreach (var purchase in state.FreebiePurchases.Where(x => x.Block == "Добродетели"))
                { purchase.ItemName = rename(purchase.ItemName); purchase.ItemId = rename(purchase.ItemId); }
            if (state.ExperiencePurchases != null)
                foreach (var purchase in state.ExperiencePurchases.Where(x => string.Equals(x.Category, "virtue", StringComparison.OrdinalIgnoreCase)))
                    purchase.ItemName = rename(purchase.ItemName);
        }

        private void ApplyVirtueNames(string first, string second)
        {
            VirtuesTrackList.Names.Clear();
            VirtuesTrackList.Names.Add(string.IsNullOrWhiteSpace(first) ? "Совесть" : first);
            VirtuesTrackList.Names.Add(string.IsNullOrWhiteSpace(second) ? "Самоконтроль" : second);
            VirtuesTrackList.Names.Add("Смелость");
            Dispatcher.BeginInvoke(new Action(EnsureVirtueTracks), DispatcherPriority.Loaded);
        }

        private void HumanityPathInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateHumanityPathSuggestions();
        }

        private void HumanityPathInput_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            UpdateHumanityPathSuggestions();
        }

        private void HumanityPathInput_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            UpdateHumanityPathSuggestions();
        }

        private void UpdateHumanityPathSuggestions()
        {
            if (_loadingHumanityPath || !IsFreeNeonate) return;
            string query = HumanityPathInput.Text.Trim();
            var matches = string.IsNullOrWhiteSpace(query)
                ? _humanityPaths.ToList()
                : _humanityPaths.Where(x => x.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            HumanityPathSuggestions.ItemsSource = matches;
            HumanityPathSuggestionsContainer.Visibility = matches.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            bool exact = _humanityPaths.Any(x => string.Equals(x.Name, query, StringComparison.OrdinalIgnoreCase));
            CreateHumanityPathButton.Visibility = !string.IsNullOrWhiteSpace(query) && !exact ? Visibility.Visible : Visibility.Collapsed;
        }

        private void HumanityPathSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Выбор применяется по MouseUp, чтобы не закрывать список при навигации клавиатурой
        }

        private void HumanityPathSuggestions_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var selected = HumanityPathSuggestions.SelectedItem as HumanityPathDefinition;
            if (selected != null) ApplyOfficialPath(selected);
        }

        private void HumanityPathInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            ConfirmHumanityPathInput();
        }

        private void CreateHumanityPath_Click(object sender, RoutedEventArgs e)
        {
            ConfirmHumanityPathInput();
        }

        private void ConfirmHumanityPathInput()
        {
            string name = HumanityPathInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;
            var official = HumanityPathService.FindByName(name);
            if (official != null) { ApplyOfficialPath(official); return; }
            HumanityPathSuggestionsContainer.Visibility = Visibility.Collapsed;
            var result = HumanityPathEditorWindow.Edit(Window.GetWindow(this), new HumanityPathEditorResult { Name = name }, false);
            if (result == null)
            {
                HumanityPathInput.Text = _character.HumanityPathIsCustom ? _character.CustomHumanityPathName : (_selectedOfficialPath == null ? "Человечность" : _selectedOfficialPath.Name);
                return;
            }
            SaveCustomHumanityPath(result);
        }

        private void EditHumanityPath_Click(object sender, RoutedEventArgs e)
        {
            if (_character == null || !_character.HumanityPathIsCustom) return;
            var current = new HumanityPathEditorResult
            {
                Name = _character.CustomHumanityPathName, Description = _character.CustomHumanityPathDescription, FirstVirtue = _character.CustomHumanityFirstVirtue, SecondVirtue = _character.CustomHumanitySecondVirtue,
                BearingName = _character.CustomHumanityBearingName, BearingEffect = _character.CustomHumanityBearingEffect,
                Edicts = _character.CustomHumanityEdicts, Hierarchy = HumanityPathEditorWindow.Clone(_character.CustomHumanityHierarchy),
                ApplyFivePointLimit = _character.CustomHumanityPathLimitEnabled
            };
            var result = HumanityPathEditorWindow.Edit(Window.GetWindow(this), current, true);
            if (result == null) return;
            if (result.Deleted)
            {
                string oldId = _character.HumanityPathId;
                string oldName = _character.CustomHumanityPathName;
                string oldDescription = _character.CustomHumanityPathDescription;
                string oldBearingName = _character.CustomHumanityBearingName;
                string oldBearingEffect = _character.CustomHumanityBearingEffect;
                string oldEdicts = _character.CustomHumanityEdicts;
                string oldFirstVirtue = _character.CustomHumanityFirstVirtue;
                string oldSecondVirtue = _character.CustomHumanitySecondVirtue;
                var oldHierarchy = HumanityPathEditorWindow.Clone(_character.CustomHumanityHierarchy);
                bool oldPathLimitEnabled = _character.CustomHumanityPathLimitEnabled;
                var humanity = HumanityPathService.Find("humanity");
                TransferPathVirtues(humanity.FirstVirtue, humanity.SecondVirtue);
                _character.HumanityPathIsCustom = false;
                _character.HumanityPathId = "humanity";
                _character.CustomHumanityPathName = null;
                _character.CustomHumanityPathDescription = null;
                _character.CustomHumanityBearingName = null;
                _character.CustomHumanityBearingEffect = null;
                _character.CustomHumanityEdicts = null;
                _character.CustomHumanityHierarchy = new List<HumanityPathSinEntry>();
                ApplyOfficialPath(humanity);
                UndoService.Store("пользовательский Путь «" + (oldName ?? "Без названия") + "»", () =>
                {
                    _character.HumanityPathIsCustom = true;
                    _character.HumanityPathId = oldId;
                    _character.CustomHumanityPathName = oldName;
                    _character.CustomHumanityPathDescription = oldDescription;
                    _character.CustomHumanityBearingName = oldBearingName;
                    _character.CustomHumanityBearingEffect = oldBearingEffect;
                    _character.CustomHumanityEdicts = oldEdicts;
                    _character.CustomHumanityFirstVirtue = oldFirstVirtue;
                    _character.CustomHumanitySecondVirtue = oldSecondVirtue;
                    _character.CustomHumanityHierarchy = HumanityPathEditorWindow.Clone(oldHierarchy);
                    _character.CustomHumanityPathLimitEnabled = oldPathLimitEnabled;
                    ApplyVirtueNames(oldFirstVirtue, oldSecondVirtue);
                    UpdateCustomHumanityPathInfo();
                    HumanityPathInput.Text = oldName ?? string.Empty;
                    EditHumanityPathButton.Visibility = Visibility.Visible;
                    HumanityPathCombo.SelectedValue = null;
                    CharacterSelectionWindow.SaveCharacters();
                });
                return;
            }
            SaveCustomHumanityPath(result);
        }

        private void SaveCustomHumanityPath(HumanityPathEditorResult result)
        {
            bool previousWasCustom = _character.HumanityPathIsCustom;
            string previousPathId = _character.HumanityPathId;
            bool previousWasHumanity = !previousWasCustom &&
                string.Equals(previousPathId, "humanity", StringComparison.OrdinalIgnoreCase);
            TransferPathVirtues(result.FirstVirtue, result.SecondVirtue);
            _character.HumanityPathIsCustom = true;
            _character.HumanityPathId = "custom";
            _character.CustomHumanityPathName = result.Name;
            _character.CustomHumanityPathDescription = result.Description;
            _character.CustomHumanityBearingName = result.BearingName;
            _character.CustomHumanityBearingEffect = result.BearingEffect;
            _character.CustomHumanityEdicts = result.Edicts;
            _character.CustomHumanityFirstVirtue = result.FirstVirtue;
            _character.CustomHumanitySecondVirtue = result.SecondVirtue;
            _character.CustomHumanityHierarchy = HumanityPathEditorWindow.Clone(result.Hierarchy);
            _character.CustomHumanityPathLimitEnabled = result.ApplyFivePointLimit;
            _loadingHumanityPath = true;
            HumanityPathName.Text = result.Name;
            HumanityPathInput.Text = result.Name;
            HumanityPathCombo.SelectedItem = null;
            UpdateCustomHumanityPathInfo();
            EditHumanityPathButton.Visibility = Visibility.Visible;
            HumanityPathSuggestionsContainer.Visibility = Visibility.Collapsed;
            CreateHumanityPathButton.Visibility = Visibility.Collapsed;
            _loadingHumanityPath = false;
            RefreshPathRatingAfterSelection();
            if (previousWasHumanity) ShowHumanityPathLimitWarning();
        }

        private void HumanitySinTable_Click(object sender, RoutedEventArgs e)
        {
            if (_character == null) return;
            var rows = _character.HumanityPathIsCustom
                ? HumanityPathEditorWindow.Clone(_character.CustomHumanityHierarchy)
                : HumanityPathEditorWindow.Clone(_selectedOfficialPath == null ? null : _selectedOfficialPath.Hierarchy);
            HumanitySinTableWindow.Show(Window.GetWindow(this), rows, HumanityPathName.Text);
        }

        private void HumanityPathTrack_ValueChanged(object sender, EventArgs e)
        {
            if (_loadingHumanityPath || _character == null) return;
            int previous = _character.HumanityPathRating;
            int next = HumanityPathTrack.Value;
            // Неподтверждённые вложения можно убрать. Подтверждённый рейтинг
            // остаётся нижней границей; его снижение доступно только через деградацию.
            if (next < previous)
            {
                var pendingState = _character.CreationState;
                int confirmedFloor = Math.Max(0, pendingState.HumanityPathConfirmedRating);
                if (!pendingState.HumanityPathPendingConfirmation || next < confirmedFloor)
                {
                    HumanityPathTrack.SetTrackValue(previous);
                    return;
                }
                if (AdvancementCheck != null &&
                    !AdvancementCheck("humanity_path", _character.HumanityPathIsCustom ? _character.CustomHumanityPathName : _character.HumanityPathId, previous, next))
                {
                    HumanityPathTrack.SetTrackValue(previous);
                    return;
                }
                _character.HumanityPathRating = next;
                if (next <= confirmedFloor)
                {
                    pendingState.HumanityPathPendingConfirmation = false;
                    pendingState.HumanityPathPendingPreviousRating = confirmedFloor;
                }
                RefreshHumanityPathActionButtons();
                return;
            }
            if (next == previous) return;
            // Сам Track не блокирует уровень выше 5: проверка способа оплаты
            // выполняется в AdvancementCheck. Опытное повышение разрешено;
            // покупка за свободные пункты выше 5 будет отклонена там.
            if (!_character.CreationState.ConfirmedInitialBlocks.Contains("virtues"))
            {
                HumanityPathTrack.SetTrackValue(previous);
                ThemedDialog.Show(Window.GetWindow(this), "Сначала подтвердите распределение Добродетелей.", "Добродетели не подтверждены", MessageBoxButton.OK, MessageBoxImage.Information,
                    "Перейти к Добродетелям", () => VirtuesHeader?.BringIntoView());
                return;
            }
            if (AdvancementCheck != null && !AdvancementCheck("humanity_path", _character.HumanityPathIsCustom ? _character.CustomHumanityPathName : _character.HumanityPathId, previous, next))
            {
                HumanityPathTrack.SetTrackValue(previous);
                return;
            }
            var state = _character.CreationState;
            if (state.HumanityPathConfirmedRating < 0) state.HumanityPathConfirmedRating = previous;
            if (!state.HumanityPathPendingConfirmation) state.HumanityPathPendingPreviousRating = previous;
            state.HumanityPathPendingConfirmation = true;
            _character.HumanityPathRating = next;
            RefreshHumanityPathActionButtons();
        }

        public void RefreshHumanityPathActions() => RefreshHumanityPathActionButtons();

        // Однократная миграция старых персонажей с Путём выше 5.
        // Вызывается при загрузке персонажа после InitializeCharacter и до Loaded.
        public void MigrateLegacyHumanityPathIfNeeded()
        {
            if (_character == null) return;
            var state = _character.CreationState;

            // Человечность не участвует в этой миграции. Режим «Без границ»
            // означает осознанный отказ от legacy-сброса для этого листа.
            if (CharacterCreationRules.IsHumanityPath(_character) ||
                !CharacterCreationRules.IsHumanityPathLimitActive(_character)) return;

            // Новый отдельный флаг хранится непосредственно в CharacterProfile.
            // Старый флаг CreationState переносим для совместимости, но не доверяем
            // ему, если прежняя версия оставила рейтинг >5 без полной XP-оплаты.
            if (_character.HumanityPathLimitMigrationCompleted) return;
            if (state.LegacyHumanityPathMigrationDone &&
                (_character.HumanityPathRating <= 5 || HasExperiencePathPurchaseAboveFive()))
            {
                _character.HumanityPathLimitMigrationCompleted = true;
                CharacterSelectionWindow.SaveCharacters();
                return;
            }

            bool hasLegacyMarkers = state.InitialAllocationEverCompleted ||
                state.InitialPurchaseConfirmed || state.InitialVirtueBlockEverConfirmed ||
                (state.PurchaseLedger != null && state.PurchaseLedger.Count > 0) ||
                (state.ExperiencePurchases != null && state.ExperiencePurchases.Count > 0) ||
                (state.FreebiePurchases != null && state.FreebiePurchases.Count > 0);

            // Если все уровни выше 5 действительно куплены за опыт, это обычная
            // прокачка. Завершаем проверку флагом листа и больше не трогаем рейтинг.
            if (_character.HumanityPathRating > 5 && HasExperiencePathPurchaseAboveFive())
            {
                _character.HumanityPathLimitMigrationCompleted = true;
                state.LegacyHumanityPathMigrationDone = true;
                CharacterSelectionWindow.SaveCharacters();
                return;
            }

            bool legacyHighPath = _character.HumanityPathRating > 5 && hasLegacyMarkers;
            if (!legacyHighPath)
            {
                // Для листов без старого превышения тоже фиксируем, что проверка
                // завершена. Если ограничение отключено, до этого места не доходим.
                if (_character.HumanityPathRating <= 5)
                {
                    _character.HumanityPathLimitMigrationCompleted = true;
                    state.LegacyHumanityPathMigrationDone = true;
                    CharacterSelectionWindow.SaveCharacters();
                }
                return;
            }

            // Исходный минимум Пути, обеспеченный двумя профильными Добродетелями.
            var virtueNames = GetVirtueNames();
            int virtueFloor = 0;
            if (virtueNames.Count >= 2)
            {
                int first, second;
                var initial = state.InitialVirtueRatings;
                if (initial != null && initial.TryGetValue(virtueNames[0], out first) &&
                    initial.TryGetValue(virtueNames[1], out second))
                    virtueFloor = Math.Min(10, first + second);
                else
                    virtueFloor = Math.Min(10, GetVirtueRating(virtueNames[0]) + GetVirtueRating(virtueNames[1]));
            }

            int refundedExperience = 0;
            int refundedFreebies = 0;
            if (state.PurchaseLedger == null) state.PurchaseLedger = new List<PurchaseTransaction>();

            var pathLedger = state.PurchaseLedger.Where(t => !t.IsCancelled &&
                (string.Equals(t.Category, "humanity_path", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(t.Block, "Путь", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(t.Block, "Человечность", StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(t => t.Order).ThenByDescending(t => t.TimestampUtc).ToList();

            // Компенсируем только реально оплаченные уровни выше virtueFloor.
            foreach (var t in pathLedger)
            {
                int levels = Math.Abs(t.NewValue - t.PreviousValue);
                int aboveFloor = Math.Max(0, Math.Max(t.PreviousValue, t.NewValue) -
                    Math.Max(virtueFloor, Math.Min(t.PreviousValue, t.NewValue)));
                if (levels > 0 && aboveFloor > 0 && t.Cost > 0)
                {
                    int refund = (t.Cost / levels) * aboveFloor;
                    if (string.Equals(t.Resource, "experience", StringComparison.OrdinalIgnoreCase))
                        refundedExperience += refund;
                    else if (string.Equals(t.Resource, "freebie", StringComparison.OrdinalIgnoreCase))
                        refundedFreebies += refund;
                }
                t.IsCancelled = true;
            }

            // Поддержка старых JSON без PurchaseLedger: старые журналы используются
            // только для того ресурса, которого нет в основном журнале.
            bool ledgerHasExperience = pathLedger.Any(t => t.Cost > 0 &&
                string.Equals(t.Resource, "experience", StringComparison.OrdinalIgnoreCase));
            bool ledgerHasFreebies = pathLedger.Any(t => t.Cost > 0 &&
                string.Equals(t.Resource, "freebie", StringComparison.OrdinalIgnoreCase));

            if (!ledgerHasFreebies && state.FreebiePurchases != null)
            {
                foreach (var p in state.FreebiePurchases.Where(p =>
                    (string.Equals(p.Block, "Путь", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(p.Block, "Человечность", StringComparison.OrdinalIgnoreCase)) &&
                    string.Equals(p.Source, "freebie", StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    int levels = Math.Abs(p.NewValue - p.PreviousValue);
                    int above = Math.Max(0, Math.Max(p.PreviousValue, p.NewValue) -
                        Math.Max(virtueFloor, Math.Min(p.PreviousValue, p.NewValue)));
                    if (levels > 0 && above > 0 && p.Cost > 0)
                        refundedFreebies += (p.Cost / levels) * above;
                    state.FreebiePurchases.Remove(p);
                }
            }

            if (!ledgerHasExperience && state.ExperiencePurchases != null)
            {
                foreach (var p in state.ExperiencePurchases.Where(p =>
                    string.Equals(p.Category, "humanity_path", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Category, "path", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Category, "путь", StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    int levels = Math.Abs(p.NewValue - p.PreviousValue);
                    int above = Math.Max(0, Math.Max(p.PreviousValue, p.NewValue) -
                        Math.Max(virtueFloor, Math.Min(p.PreviousValue, p.NewValue)));
                    if (levels > 0 && above > 0 && p.Cost > 0)
                        refundedExperience += (p.Cost / levels) * above;
                    state.ExperiencePurchases.Remove(p);
                }
            }

            int virtueBase = CharacterCreationRules.GetCreationLimit("virtue_base");

            // Сила воли также производна от Смелости. При сбросе Добродетелей
            // постоянная Сила воли возвращается к базовому значению, поэтому
            // компенсируем только реально оплаченные уровни выше базы.
            bool ledgerHasWillpowerExperience = state.PurchaseLedger.Any(t => !t.IsCancelled && t.Cost > 0 &&
                (string.Equals(t.Category, "willpower", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(t.Block, "Сила воли", StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(t.Resource, "experience", StringComparison.OrdinalIgnoreCase));
            bool ledgerHasWillpowerFreebies = state.PurchaseLedger.Any(t => !t.IsCancelled && t.Cost > 0 &&
                (string.Equals(t.Category, "willpower", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(t.Block, "Сила воли", StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(t.Resource, "freebie", StringComparison.OrdinalIgnoreCase));

            var willpowerLedger = state.PurchaseLedger.Where(t => !t.IsCancelled &&
                (string.Equals(t.Category, "willpower", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(t.Block, "Сила воли", StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(t => t.Order).ThenByDescending(t => t.TimestampUtc).ToList();
            foreach (var t in willpowerLedger)
            {
                int levels = Math.Abs(t.NewValue - t.PreviousValue);
                int aboveBase = Math.Max(0, Math.Max(t.PreviousValue, t.NewValue) -
                    Math.Max(virtueBase, Math.Min(t.PreviousValue, t.NewValue)));
                if (levels > 0 && aboveBase > 0 && t.Cost > 0)
                {
                    int refund = (t.Cost / levels) * aboveBase;
                    if (string.Equals(t.Resource, "experience", StringComparison.OrdinalIgnoreCase))
                        refundedExperience += refund;
                    else if (string.Equals(t.Resource, "freebie", StringComparison.OrdinalIgnoreCase))
                        refundedFreebies += refund;
                }
                t.IsCancelled = true;
            }

            if (!ledgerHasWillpowerFreebies && state.FreebiePurchases != null)
            {
                foreach (var p in state.FreebiePurchases.Where(p =>
                    string.Equals(p.Block, "Сила воли", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.Source, "freebie", StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    int levels = Math.Abs(p.NewValue - p.PreviousValue);
                    int above = Math.Max(0, Math.Max(p.PreviousValue, p.NewValue) -
                        Math.Max(virtueBase, Math.Min(p.PreviousValue, p.NewValue)));
                    if (levels > 0 && above > 0 && p.Cost > 0)
                        refundedFreebies += (p.Cost / levels) * above;
                    state.FreebiePurchases.Remove(p);
                }
            }

            if (!ledgerHasWillpowerExperience && state.ExperiencePurchases != null)
            {
                foreach (var p in state.ExperiencePurchases.Where(p =>
                    string.Equals(p.Category, "willpower", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Category, "Сила воли", StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    int levels = Math.Abs(p.NewValue - p.PreviousValue);
                    int above = Math.Max(0, Math.Max(p.PreviousValue, p.NewValue) -
                        Math.Max(virtueBase, Math.Min(p.PreviousValue, p.NewValue)));
                    if (levels > 0 && above > 0 && p.Cost > 0)
                        refundedExperience += (p.Cost / levels) * above;
                    state.ExperiencePurchases.Remove(p);
                }
            }

            // Удаляем старые записи о покупках, но не возвращаем стоимость
            // Добродетелей и уровней Пути до минимума: их уже компенсировали ранее.
            if (state.FreebiePurchases != null)
                state.FreebiePurchases.RemoveAll(p =>
                    string.Equals(p.Block, "Добродетели", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Block, "Путь", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Block, "Человечность", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Block, "Сила воли", StringComparison.OrdinalIgnoreCase));
            if (state.ExperiencePurchases != null)
                state.ExperiencePurchases.RemoveAll(p =>
                    string.Equals(p.Category, "virtue", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Category, "добродетель", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Category, "humanity_path", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Category, "path", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Category, "путь", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Category, "willpower", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Category, "Сила воли", StringComparison.OrdinalIgnoreCase));
            if (state.InitialPurchases != null)
                state.InitialPurchases.RemoveAll(p =>
                    string.Equals(p.Block, "Добродетели", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Block, "Сила воли", StringComparison.OrdinalIgnoreCase));

            state.ExperiencePoints += refundedExperience;
            state.FreebiePoints += refundedFreebies;

            if (state.InitialVirtueRatings == null)
                state.InitialVirtueRatings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            else
                state.InitialVirtueRatings.Clear();

            // Сбрасываем Путь, затем Добродетели.
            _character.HumanityPathRating = Math.Min(10, virtueBase * 2);
            HumanityPathTrack.SetTrackValue(_character.HumanityPathRating);
            foreach (string virtue in virtueNames)
            {
                state.OtherDots["virtue:" + virtue] = virtueBase;
                state.InitialVirtueRatings[virtue] = virtueBase;
                foreach (var track in FindTracks(VirtuesTrackList))
                    if (string.Equals(track.Label, virtue, StringComparison.OrdinalIgnoreCase))
                        track.SetTrackValue(virtueBase);
            }
            SetWillpowerPermanent(virtueBase);

            state.UnconfirmInitialBlock("virtues");
            state.InitialVirtueBlockEverConfirmed = false;
            state.HumanityPathConfirmedRating = -1;
            state.HumanityPathPendingConfirmation = false;
            state.HumanityPathPendingPreviousRating = _character.HumanityPathRating;
            state.HumanityPathDegradationCount = 0;
            state.LegacyHumanityPathMigrationDone = true;
            _character.HumanityPathLimitMigrationCompleted = true;
            state.LegacyHumanityPathMigrationNoticePending = true;
            state.LegacyHumanityPathRefundedExperience = refundedExperience;
            state.LegacyHumanityPathRefundedFreebies = refundedFreebies;

            RecalculatePrimaryVirtueDerivedValues();
            RefreshInitialBlockControls();
            RefreshHumanityPathActionButtons();
            UndoService.Clear();
            CharacterSelectionWindow.SaveCharacters();
        }

        public void ShowLegacyHumanityPathMigrationNoticeIfNeeded()
        {
            if (_character == null || !_character.CreationState.LegacyHumanityPathMigrationNoticePending) return;
            var state = _character.CreationState;
            state.LegacyHumanityPathMigrationNoticePending = false;
            string message = "Обнаружен персонаж старого формата с Путём выше 5. Путь и Добродетели разблокированы и сброшены до базовых значений. Возвращено ресурсов: " +
                state.LegacyHumanityPathRefundedExperience + " опыта и " +
                state.LegacyHumanityPathRefundedFreebies + " свободных пунктов.";
            ThemedDialog.Show(Window.GetWindow(this), message, "Обновление старого персонажа",
                MessageBoxButton.OK, MessageBoxImage.Information);
            CharacterSelectionWindow.SaveCharacters();
        }

        public void OpenBioFactPicker()
        {
            AddBioFact_Click(this, new RoutedEventArgs());
        }

        public void ScrollToVirtues()
        {
            VirtuesHeader?.BringIntoView();
        }

        public void ScrollToHumanityPath()
        {
            HumanityPathTrack?.BringIntoView();
        }

        private void BioFactsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var inner = sender as ScrollViewer;
            if (inner == null) return;
            bool canScrollFurther = e.Delta > 0 ? inner.VerticalOffset > 0 : inner.VerticalOffset < inner.ScrollableHeight;
            if (canScrollFurther) return;
            var outer = FindOuterScrollViewer(inner);
            if (outer == null) return;
            int lines = Math.Max(1, Math.Abs(e.Delta) / 120) * 3;
            for (int i = 0; i < lines; i++)
            {
                if (e.Delta > 0) outer.LineUp();
                else outer.LineDown();
            }
            e.Handled = true;
        }

        private static ScrollViewer FindOuterScrollViewer(DependencyObject element)
        {
            DependencyObject current = element;
            while (current != null)
            {
                current = current is Visual || current is System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(current)
                    : LogicalTreeHelper.GetParent(current);
                if (current is ScrollViewer viewer) return viewer;
            }
            return null;
        }

        private void AddBioFact_Click(object sender, RoutedEventArgs e)
        {
            if (_character == null) return;
            bool creationFinished = _character.CreationState.InitialPurchaseConfirmed;
            bool backgroundsConfirmed = creationFinished || _character.CreationState.ConfirmedInitialBlocks.Contains("backgrounds");
            int backgroundFreebieCost = CharacterCreationRules.GetFreebieCost("background");
            if (creationFinished && (!backgroundsConfirmed || _character.CreationState.FreebiePoints < backgroundFreebieCost))
            {
                ThemedDialog.Show(Window.GetWindow(this), "Для добавления факта биографии нужен минимум " + backgroundFreebieCost + " свободных пункт(а/ов). Сейчас доступно: " + _character.CreationState.FreebiePoints + ".", "Недостаточно свободных пунктов", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshInitialBlockControls();
                return;
            }
            int backgroundUsed = _character.CreationState.InitialBackgroundBlockEverConfirmed
                ? (_character.CreationState.InitialBackgroundDots == null ? 0 : _character.CreationState.InitialBackgroundDots.Values.Sum())
                : _character.BiographyFacts.Sum(x => Math.Max(0, x.Level));
            int availableBackgroundDots = Math.Max(0, CharacterCreationRules.GetCreationLimit("backgrounds") - backgroundUsed);
            if (!backgroundsConfirmed && availableBackgroundDots <= 0) return;
            int maximumPurchasableLevel = creationFinished
                ? Math.Min(5, _character.CreationState.FreebiePoints / Math.Max(1, backgroundFreebieCost))
                : 5;
            var picker = new BioFactPickerWindow(_character, maximumPurchasableLevel) { Owner = Window.GetWindow(this) };
            if (picker.ShowDialog() != true || picker.Result == null) return;
            foreach (var existing in _character.BiographyFacts)
            {
                if (string.Equals(existing.Name, picker.Result.Name, StringComparison.OrdinalIgnoreCase))
                {
                    ThemedDialog.Show(Window.GetWindow(this), "Этот факт биографии уже добавлен.", "Факт биографии", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }
            if (!backgroundsConfirmed)
            {
                int remaining = Math.Max(0, CharacterCreationRules.GetCreationLimit("backgrounds") - backgroundUsed);
                picker.Result.Level = Math.Min(picker.Result.Level, remaining);
                if (picker.Result.Level == 0) return;
                if (_character.CreationState.InitialBackgroundBlockEverConfirmed && _character.CreationState.InitialBackgroundDots != null)
                    _character.CreationState.InitialBackgroundDots[picker.Result.Name] = picker.Result.Level;
            }
            else
            {
                if (picker.Result.Level <= 0)
                {
                    ThemedDialog.Show(Window.GetWindow(this), "Для нового факта биографии нужно выбрать хотя бы 1 точку.", "Не указан уровень факта", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (AdvancementCheck != null && !AdvancementCheck("background", picker.Result.Name, 0, picker.Result.Level))
                    return;
            }
            _character.BiographyFacts.Add(picker.Result);
            (Window.GetWindow(this) as MainWindow)?.RefreshGenerationFromCharacter();
            RenderBioFacts();
        }

        public void RefreshBiographyFacts()
        {
            RenderBioFacts();
        }

        private void RenderBioFacts()
        {
            BioFactsPanel.Children.Clear();
            if (_character == null || _character.BiographyFacts == null) return;
            foreach (var fact in _character.BiographyFacts)
            {
                var card = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(5, 3, 7, 3), Margin = new Thickness(0, 2, 0, 3) };
                card.SetResourceReference(Border.BackgroundProperty, "CardHeaderBrush");
                card.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var removeIcon = new TextBlock { Text = "\uE74D", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16, Foreground = new SolidColorBrush(Color.FromRgb(190, 35, 35)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var remove = new Button { Content = removeIcon, Width = 30, Height = 30, Margin = new Thickness(0, 0, 7, 0), Padding = new Thickness(0), Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, ToolTip = "Удалить факт" };
                remove.Click += (s, e) =>
                {
                    string message = fact.IsCustom ? "Удалить пользовательский факт «" + fact.Name + "»? Его основное описание и описания уровней также будут удалены." : "Удалить факт «" + fact.Name + "» из биографии?";
                    var answer = ThemedDialog.Show(Window.GetWindow(this), message, "Удаление факта биографии", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (answer != MessageBoxResult.Yes) return;
                    string creationStateSnapshot = UndoService.CaptureCreationState(_character);
                    bool hasRecordedPurchase = _character.CreationState.PurchaseLedger != null && _character.CreationState.PurchaseLedger.Any(t => !t.IsCancelled && string.Equals(t.ItemName, fact.Name, StringComparison.OrdinalIgnoreCase));
                    if (fact.Level > 0 && hasRecordedPurchase && AdvancementCheck != null && !AdvancementCheck("background", fact.Name, fact.Level, 0)) return;
                    int restoreIndex = _character.BiographyFacts.IndexOf(fact);
                    bool hadSnapshot = _character.CreationState.InitialBackgroundDots != null && _character.CreationState.InitialBackgroundDots.ContainsKey(fact.Name);
                    int snapshotValue = 0;
                    if (hadSnapshot) snapshotValue = _character.CreationState.InitialBackgroundDots[fact.Name];
                    if (_character.CreationState.InitialBackgroundDots != null) _character.CreationState.InitialBackgroundDots.Remove(fact.Name);
                    _character.BiographyFacts.Remove(fact);
                    UndoService.Store("факт биографии «" + fact.Name + "»", () =>
                    {
                        if (!_character.BiographyFacts.Contains(fact)) _character.BiographyFacts.Insert(Math.Max(0, Math.Min(restoreIndex, _character.BiographyFacts.Count)), fact);
                        if (hadSnapshot) _character.CreationState.InitialBackgroundDots[fact.Name] = snapshotValue;
                        UndoService.RestoreCreationState(_character, creationStateSnapshot);
                        (Window.GetWindow(this) as MainWindow)?.RefreshGenerationFromCharacter();
                        RenderBioFacts();
                        CharacterSelectionWindow.SaveCharacters();
                    });
                    (Window.GetWindow(this) as MainWindow)?.RefreshGenerationFromCharacter();
                    RenderBioFacts();
                };
                Grid.SetColumn(remove, 0); row.Children.Add(remove);
                var definition = BioFactService.Find(fact.Name);
                int backgroundMax = 5;
                var track = new TrackControl
                {
                    Label = fact.Name,
                    Count = backgroundMax,
                    LabelWidth = 170,
                    Cascade = true,
                    DisableInfo = true,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                track.SetTrackValue(fact.Level);
                track.ValueChanged += (s, e) =>
                {
                    int previous = fact.Level;
                    bool backgroundBlockConfirmed = _character.CreationState.ConfirmedInitialBlocks.Contains("backgrounds");
                    bool backgroundBlockWasConfirmed = _character.CreationState.InitialBackgroundBlockEverConfirmed;
                    bool hasRecordedPurchase = _character.CreationState.PurchaseLedger != null && _character.CreationState.PurchaseLedger.Any(t => !t.IsCancelled && string.Equals(t.ItemName, fact.Name, StringComparison.OrdinalIgnoreCase));
                    if (track.Value != previous && (backgroundBlockConfirmed || (backgroundBlockWasConfirmed && track.Value < previous && hasRecordedPurchase)) &&
                        AdvancementCheck != null && !AdvancementCheck("background", fact.Name, previous, track.Value))
                    { track.SetTrackValue(previous); return; }
                    int next = track.Value;
                    if (!backgroundBlockConfirmed && next > previous)
                    {
                        if (backgroundBlockWasConfirmed)
                        {
                            int initialCurrent; _character.CreationState.InitialBackgroundDots.TryGetValue(fact.Name, out initialCurrent);
                            int initialOther = _character.CreationState.InitialBackgroundDots.Where(x => !string.Equals(x.Key, fact.Name, StringComparison.OrdinalIgnoreCase)).Sum(x => Math.Max(0, x.Value));
                            int remaining = Math.Max(0, CharacterCreationRules.GetCreationLimit("backgrounds") - initialOther - initialCurrent);
                            int paidDots = _character.CreationState.PurchaseLedger.Where(t => !t.IsCancelled && string.Equals(t.ItemName, fact.Name, StringComparison.OrdinalIgnoreCase) && t.Resource != "initial_pool").Sum(t => Math.Max(0, t.NewValue - t.PreviousValue));
                            next = Math.Min(next, paidDots + initialCurrent + remaining);
                        }
                        else
                        {
                            int remaining = Math.Max(0, CharacterCreationRules.GetCreationLimit("backgrounds") - _character.BiographyFacts.Where(x => !ReferenceEquals(x, fact)).Sum(x => Math.Max(0, x.Level)) - previous);
                            next = Math.Min(next, previous + remaining);
                        }
                        if (next != track.Value) track.SetTrackValue(next);
                    }
                    fact.Level = next;
                    if (!backgroundBlockConfirmed && backgroundBlockWasConfirmed)
                    {
                        int paidDots = _character.CreationState.PurchaseLedger.Where(t => !t.IsCancelled && string.Equals(t.ItemName, fact.Name, StringComparison.OrdinalIgnoreCase) && t.Resource != "initial_pool").Sum(t => Math.Max(0, t.NewValue - t.PreviousValue));
                        _character.CreationState.InitialBackgroundDots[fact.Name] = Math.Max(0, next - paidDots);
                    }
                    RenderBioFacts();
                    (Window.GetWindow(this) as MainWindow)?.RefreshGenerationFromCharacter();
                };
                Grid.SetColumn(track, 1); row.Children.Add(track); card.Child = row;
                card.Background = Brushes.Transparent; 
                card.Cursor = Cursors.Hand;
                card.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    if (e.ClickCount != 2) return;

                    
                    DependencyObject source = e.OriginalSource as DependencyObject;
                    while (source != null && !ReferenceEquals(source, card))
                    {
                        if (source is Button || source is CheckBox) return;
                        source = source is Visual || source is System.Windows.Media.Media3D.Visual3D
                            ? VisualTreeHelper.GetParent(source)
                            : LogicalTreeHelper.GetParent(source);
                    }

                    new BioFactDescriptionWindow(Window.GetWindow(this), fact, definition).ShowDialog();
                    e.Handled = true;
                };
                BioFactsPanel.Children.Add(card);
            }
            RefreshInitialBlockControls();
            InitialPurchaseProgressChanged?.Invoke();
        }

        private void RefreshInitialBlockControls()
        {
            RefreshHumanityPathActionButtons();
            if (_character == null || VirtuesRemainingLabel == null) return;
            int virtuePool = CharacterCreationRules.GetCreationLimit("virtues");
            int virtueUsed = GetVirtueExtraDots();
            int backgroundPool = CharacterCreationRules.GetCreationLimit("backgrounds");
            int backgroundUsed = _character.CreationState.InitialBackgroundDots != null && _character.CreationState.InitialBackgroundDots.Count > 0
                ? _character.CreationState.InitialBackgroundDots.Values.Sum()
                : (_character.BiographyFacts == null ? 0 : _character.BiographyFacts.Sum(x => Math.Max(0, x.Level)));
            VirtuesRemainingLabel.Text = "Осталось точек: " + Math.Max(0, virtuePool - virtueUsed);
            BackgroundRemainingLabel.Text = "Осталось точек: " + Math.Max(0, backgroundPool - backgroundUsed);
            bool virtuesDone = _character.CreationState.ConfirmedInitialBlocks.Contains("virtues");
            bool backgroundsDone = _character.CreationState.ConfirmedInitialBlocks.Contains("backgrounds");
            VirtuesRemainingLabel.Visibility = virtuesDone ? Visibility.Collapsed : Visibility.Visible;
            ConfirmVirtuesButton.Visibility = virtuesDone ? Visibility.Collapsed : Visibility.Visible;
            BackgroundRemainingLabel.Visibility = backgroundsDone ? Visibility.Collapsed : Visibility.Visible;
            ConfirmBackgroundsButton.Visibility = backgroundsDone ? Visibility.Collapsed : Visibility.Visible;
            ApplyVirtuePoolLimits();
            UpdateAddBioFactButton(backgroundsDone, backgroundPool - backgroundUsed);
        }

        private void UpdateAddBioFactButton(bool? backgroundsDoneOverride = null, int? remainingBackgroundDotsOverride = null)
        {
            if (_character == null || AddBioFactButton == null) return;
            bool backgroundsDone = backgroundsDoneOverride ?? _character.CreationState.ConfirmedInitialBlocks.Contains("backgrounds");
            int remaining = remainingBackgroundDotsOverride ?? Math.Max(0,
                CharacterCreationRules.GetCreationLimit("backgrounds") -
                (_character.CreationState.InitialBackgroundDots != null && _character.CreationState.InitialBackgroundDots.Count > 0
                    ? _character.CreationState.InitialBackgroundDots.Values.Sum()
                    : (_character.BiographyFacts == null ? 0 : _character.BiographyFacts.Sum(x => Math.Max(0, x.Level)))));
            int freebiePoints = _character.CreationState.FreebiePoints;
            int backgroundCost;
            try { backgroundCost = CharacterCreationRules.GetFreebieCost("background"); }
            catch { backgroundCost = int.MaxValue; }
            bool initialPoolAvailable = !backgroundsDone && remaining > 0;
            bool freebiePurchaseAvailable = backgroundsDone && freebiePoints > 0;
            AddBioFactButton.Visibility = !backgroundsDone || freebiePoints > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            AddBioFactButton.IsEnabled = initialPoolAvailable ||
                (backgroundsDone && freebiePoints >= backgroundCost);
        }

        private void ApplyVirtuePoolLimits()
        {
            if (_character == null || VirtuesTrackList == null) return;
            int remaining = Math.Max(0, CharacterCreationRules.GetCreationLimit("virtues") - GetVirtueExtraDots());
            ApplyVirtueLimitsRecursive(VirtuesTrackList, remaining);
        }

        private void ApplyVirtueLimitsRecursive(DependencyObject parent, int remaining)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                var track = child as TrackControl;
                if (track != null)
                {
                    track.MaxValue = track.Count;
                    track.IsEnabled = true;
                }
                else ApplyVirtueLimitsRecursive(child, remaining);
            }
        }

        private void ConfirmVirtues_Click(object sender, RoutedEventArgs e)
        {
            if (_character == null) return;
            int virtuePoolToSpend = CharacterCreationRules.GetCreationLimit("virtues");
            int virtueDotsSpent = GetVirtueExtraDots();
            bool allVirtuesSelected = GetVirtueNames().All(name => GetVirtueRating(name) > 0);
            if (!allVirtuesSelected || virtueDotsSpent < virtuePoolToSpend)
            {
                ThemedDialog.Show(Window.GetWindow(this),
                    "Выбери все Добродетели и распредели весь пул точек. Осталось: " + Math.Max(0, virtuePoolToSpend - virtueDotsSpent) + ".",
                    "Не все точки распределены", MessageBoxButton.OK, MessageBoxImage.Information,
                    "Перейти к Добродетелям", () => VirtuesHeader?.BringIntoView());
                return;
            }
            if (_character.CreationState.InitialVirtueRatings == null)
                _character.CreationState.InitialVirtueRatings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // При повторном подтверждении (в частности, после миграционного сброса)
            // старый снимок базовых Добродетелей нельзя сохранять: фиксируем именно
            // новые значения, которые игрок распределил перед этим подтверждением.
            _character.CreationState.InitialVirtueRatings.Clear();
            foreach (var virtueName in GetVirtueNames())
                _character.CreationState.InitialVirtueRatings[virtueName] = GetVirtueRating(virtueName);
            int courage;
            if (!_character.CreationState.InitialVirtueRatings.TryGetValue("Смелость", out courage)) courage = GetVirtueRating("Смелость");
            SetWillpowerPermanent(courage);
            if (_character.CreationState.PurchaseLedger == null)
                _character.CreationState.PurchaseLedger = new List<PurchaseTransaction>();
            _character.CreationState.PurchaseLedger.RemoveAll(t => t.Resource == "initial_pool" && t.Block == "Добродетели");
            foreach (var virtueName in GetVirtueNames())
            {
                int rating = GetVirtueRating(virtueName);
                int initialRating;
                if (!_character.CreationState.InitialVirtueRatings.TryGetValue(virtueName, out initialRating)) initialRating = CharacterCreationRules.GetCreationLimit("virtue_base");
                int baseValue = CharacterCreationRules.GetCreationLimit("virtue_base");
                for (int level = baseValue; level < initialRating; level++)
                    _character.CreationState.PurchaseLedger.Add(new PurchaseTransaction
                    {
                        Order = _character.CreationState.NextPurchaseOrder++, Block = "Добродетели", Category = "virtue",
                        ItemId = virtueName, ItemName = virtueName, PreviousValue = level, NewValue = level + 1,
                        Resource = "initial_pool", Cost = 0, AffectsVirtueDerivedValues = true, Source = "Initial"
                    });
            }
            // Пересчитываем Путь/Человечность по только что зафиксированным
            // первичным значениям двух профильных Добродетелей.
            RecalculatePrimaryVirtueDerivedValues(forceConfirmed: true, preservePathPurchases: true);
            if (_character.CreationState.InitialPurchases == null) _character.CreationState.InitialPurchases = new List<CreationPurchase>();
            _character.CreationState.InitialPurchases.RemoveAll(p => p.Block == "Добродетели" || p.Block == "Сила воли");
            foreach (var virtueName in GetVirtueNames())
            {
                int rating = GetVirtueRating(virtueName);
                int initialRating;
                if (!_character.CreationState.InitialVirtueRatings.TryGetValue(virtueName, out initialRating)) initialRating = CharacterCreationRules.GetCreationLimit("virtue_base");
                _character.CreationState.InitialPurchases.Add(new CreationPurchase { Block = "Добродетели", ItemId = virtueName, ItemName = virtueName, PreviousValue = CharacterCreationRules.GetCreationLimit("virtue_base"), NewValue = initialRating, Cost = 0, Source = "initial" });
            }
            _character.CreationState.InitialPurchases.Add(new CreationPurchase { Block = "Сила воли", ItemId = "willpower_permanent", ItemName = "Сила воли", PreviousValue = 0, NewValue = courage, Cost = 0, Source = "initial" });
            _character.CreationState.InitialVirtueBlockEverConfirmed = true;
            _character.CreationState.ConfirmInitialBlock("virtues");
            if (_character.CreationState.HumanityPathConfirmedRating < 0)
                _character.CreationState.HumanityPathConfirmedRating = _character.HumanityPathRating;
            RefreshInitialBlockControls(); RefreshHumanityPathActionButtons(); InitialPurchaseProgressChanged?.Invoke();
            (Window.GetWindow(this) as MainWindow)?.RefreshGenerationFromCharacter();
        }
        private void ConfirmBackgrounds_Click(object sender, RoutedEventArgs e)
        {
            if (_character == null) return;
            int backgroundPoolToSpend = CharacterCreationRules.GetCreationLimit("backgrounds");
            int backgroundDotsSpent = _character.BiographyFacts == null ? 0 : _character.BiographyFacts.Sum(x => Math.Max(0, x.Level));
            bool allFactsSelected = _character.BiographyFacts != null &&
                _character.BiographyFacts.Count > 0 &&
                _character.BiographyFacts.All(fact => fact.Level > 0);
            if (!allFactsSelected || backgroundDotsSpent < backgroundPoolToSpend)
            {
                ThemedDialog.Show(Window.GetWindow(this),
                    "Добавь факты биографии, выбери для каждого хотя бы 1 точку и распредели весь пул. Осталось: " + Math.Max(0, backgroundPoolToSpend - backgroundDotsSpent) + ".",
                    "Не все точки распределены", MessageBoxButton.OK, MessageBoxImage.Information,
                    "Добавить факт", () => OpenBioFactPicker());
                return;
            }
            if (_character.CreationState.InitialPurchases == null) _character.CreationState.InitialPurchases = new List<CreationPurchase>();
            _character.CreationState.InitialPurchases.RemoveAll(p => p.Block == "Факты биографии");
            _character.CreationState.PurchaseLedger.RemoveAll(t => t.Resource == "initial_pool" && t.Block == "Фоны");
            if (_character.CreationState.InitialBackgroundDots == null) _character.CreationState.InitialBackgroundDots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            bool hadInitialBackgroundSnapshot = _character.CreationState.InitialBackgroundBlockEverConfirmed;
            if (!hadInitialBackgroundSnapshot) _character.CreationState.InitialBackgroundDots.Clear();
            foreach (var fact in _character.BiographyFacts)
            {
                if (!hadInitialBackgroundSnapshot && !_character.CreationState.InitialBackgroundDots.ContainsKey(fact.Name))
                    _character.CreationState.InitialBackgroundDots[fact.Name] = fact.Level;
                int initialLevel;
                if (!_character.CreationState.InitialBackgroundDots.TryGetValue(fact.Name, out initialLevel)) initialLevel = 0;
                _character.CreationState.InitialPurchases.Add(new CreationPurchase { Block = "Факты биографии", ItemId = fact.Name, ItemName = fact.Name, PreviousValue = 0, NewValue = initialLevel, Cost = 0, Source = "initial" });
                for (int level = 0; level < initialLevel; level++)
                    _character.CreationState.PurchaseLedger.Add(new PurchaseTransaction { Order = _character.CreationState.NextPurchaseOrder++, Block = "Фоны", Category = "background", ItemId = fact.Name, ItemName = fact.Name, PreviousValue = level, NewValue = level + 1, Resource = "initial_pool", Cost = 0, Source = "Initial" });
            }
            _character.CreationState.InitialBackgroundBlockEverConfirmed = true;
            _character.CreationState.ConfirmInitialBlock("backgrounds");
            RenderBioFacts(); InitialPurchaseProgressChanged?.Invoke();
        }

        private void AggravatedDamageToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_character == null || AggravatedDamageToggle == null) return;
            _character.AllowAggravatedDamage = AggravatedDamageToggle.IsChecked == true;
        }

        private void SaveHealthStates()
        {
            if (_character == null) return;
            _character.HealthDamageStates = new System.Collections.ObjectModel.ObservableCollection<int>(HealthTrack.GetStates());
            UpdateHealthHeader();
        }

        private void UpdateHealthHeader()
        {
            if (HealthHeader == null || HealthTrack == null || BloodTrack == null) return;
            var states = HealthTrack.GetStates();
            bool incapacitated = states.Count >= 7 && states[6] != 0;
            if (incapacitated && BloodTrack.Value == 0)
                HealthHeader.Text = "Торпор";
            else
                HealthHeader.Text = "Здоровье";
        }

        public void ApplyGenerationRules(CharacterProfile character)
        {
            if (character == null) return;
            int currentBlood = BloodTrack.Value;
            BloodTrack.Count = character.BloodPoolMax > 0 ? character.BloodPoolMax : 10;
            BloodTrack.SetTrackValue(Math.Max(0, Math.Min(BloodTrack.Count, currentBlood)));
            character.CurrentBlood = BloodTrack.Value;
            BloodPerTurnText.Text = "Максимум: " + BloodTrack.Count + " пунктов  •  Трата за ход: " + character.BloodPerTurn + " " +
                (character.BloodPerTurn == 1 ? "пункт" : character.BloodPerTurn >= 2 && character.BloodPerTurn <= 4 ? "пункта" : "пунктов");
        }

        private void TrackControl_Loaded(object sender, RoutedEventArgs e) { }
    }
}
