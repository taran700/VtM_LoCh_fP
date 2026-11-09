using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using VtM_LoCh_fP.Controls;

namespace VtM_LoCh_fP.Views
{
    public partial class PageTwoView : UserControl
    {
        private CharacterProfile _character;
        private bool _loadingHumanityPath;
        private List<HumanityPathDefinition> _humanityPaths;
        private HumanityPathDefinition _selectedOfficialPath;
        public event Action InitialPurchaseProgressChanged;
        public event Func<string, string, int, int, bool> AdvancementCheck;
        public event Func<string, string, int, int, bool> VirtueAdvancementCheck;

        public PageTwoView()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                EnsureVirtueTracks();
                EnsureWillpowerTrack();
                InitializeHumanityFromVirtues();
            };
            _humanityPaths = HumanityPathService.GetAll();
            HumanityPathCombo.ItemsSource = _humanityPaths;
            HumanityPathSuggestions.ItemsSource = _humanityPaths;
            HumanityPathTrack.ValueChanged += HumanityPathTrack_ValueChanged;
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
                UpdateHealthHeader();
                return true;
            };
            HealthTrack.HealingMode = () => AppSettings.WoundHealingMode;
            HealthTrack.AllowAggravatedDamage = () => AggravatedDamageToggle != null && AggravatedDamageToggle.IsChecked == true;
            HealthTrack.StateChanged = SaveHealthStates;
            BloodTrack.ValueChanged += (s, e) => UpdateHealthHeader();
            WillpowerPermanentTrack.ValueChanged += (s, e) =>
            {
                if (_character == null) return;
                int oldValue; _character.CreationState.OtherDots.TryGetValue("willpower_permanent", out oldValue);
                int nextValue = WillpowerPermanentTrack.Value;
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
            _character.CreationState.PropertyChanged -= CreationState_PropertyChanged;
            _character.CreationState.PropertyChanged += CreationState_PropertyChanged;
            AggravatedDamageToggle.IsChecked = _character.AllowAggravatedDamage;
            HealthTrack.LoadStates(_character.HealthDamageStates);
            UpdateHealthHeader();
            RenderBioFacts();
            LoadHumanityPath();
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
                    ? _character.CreationState.PurchaseLedger.Where(t => string.Equals(t.ItemName, track.Label, StringComparison.OrdinalIgnoreCase) && t.Resource != "initial_pool")
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
            if (_character == null || _character.CreationState.ConfirmedInitialBlocks.Contains("virtues")) return;
            int conscience = GetVirtueRating("Совесть");
            int selfControl = GetVirtueRating("Самоконтроль");
            int initialHumanity = Math.Min(10, conscience + selfControl);
            _character.HumanityPathRating = initialHumanity;
            if (HumanityPathTrack != null) HumanityPathTrack.SetTrackValue(initialHumanity);
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

        public void RecalculatePrimaryVirtueDerivedValues()
        {
            if (_character == null) return;
            var initial = _character.CreationState.InitialVirtueRatings;
            if (initial == null || initial.Count == 0) return;
            int courage;
            if (initial.TryGetValue("Смелость", out courage))
            {
                int previousWillpower = GetWillpowerPermanent();
                const string willpowerName = "Постоянная сила воли";
                if (previousWillpower > courage && AdvancementCheck != null)
                    AdvancementCheck("willpower", willpowerName, previousWillpower, courage);
                else if (previousWillpower < courage && AdvancementCheck != null)
                    AdvancementCheck("willpower", willpowerName, courage, previousWillpower);
                SetWillpowerPermanent(courage);
            }
            var names = GetVirtueNames();
            int first, second;
            if (names.Count >= 2 && initial.TryGetValue(names[0], out first) && initial.TryGetValue(names[1], out second))
            {
                int nextPath = Math.Min(10, first + second);
                int previousPath = _character.HumanityPathRating;
                string pathName = _character.HumanityPathIsCustom ? _character.CustomHumanityPathName : _character.HumanityPathId;
                if (previousPath > nextPath && AdvancementCheck != null)
                    AdvancementCheck("humanity_path", pathName, previousPath, nextPath);
                else if (previousPath < nextPath && AdvancementCheck != null)
                    AdvancementCheck("humanity_path", pathName, nextPath, previousPath);
                SetHumanityPathRating(nextPath);
            }
        }

        public void SetHumanityPathRating(int value)
        {
            if (_character == null) return;
            _character.HumanityPathRating = value;
            HumanityPathTrack.SetTrackValue(value);
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
            HumanityPathSuggestionsPopup.IsOpen = false;
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
            HumanityPathSuggestionsPopup.IsOpen = false;
            CreateHumanityPathButton.Visibility = Visibility.Collapsed;
            _loadingHumanityPath = false;
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
            if (_loadingHumanityPath || !IsFreeNeonate) return;
            string query = HumanityPathInput.Text.Trim();
            var matches = string.IsNullOrWhiteSpace(query) ? new List<HumanityPathDefinition>() : _humanityPaths.Where(x => x.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            HumanityPathSuggestions.ItemsSource = matches;
            HumanityPathSuggestionsPopup.IsOpen = matches.Count > 0;
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
            HumanityPathSuggestionsPopup.IsOpen = false;
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
                Edicts = _character.CustomHumanityEdicts, Hierarchy = HumanityPathEditorWindow.Clone(_character.CustomHumanityHierarchy)
            };
            var result = HumanityPathEditorWindow.Edit(Window.GetWindow(this), current, true);
            if (result == null) return;
            if (result.Deleted)
            {
                var humanity = HumanityPathService.Find("humanity");
                _character.HumanityPathIsCustom = false;
                _character.HumanityPathId = "humanity";
                _character.CustomHumanityPathName = null;
                _character.CustomHumanityPathDescription = null;
                _character.CustomHumanityBearingName = null;
                _character.CustomHumanityBearingEffect = null;
                _character.CustomHumanityEdicts = null;
                _character.CustomHumanityHierarchy = new List<HumanityPathSinEntry>();
                ApplyOfficialPath(humanity);
                return;
            }
            SaveCustomHumanityPath(result);
        }

        private void SaveCustomHumanityPath(HumanityPathEditorResult result)
        {
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
            _loadingHumanityPath = true;
            HumanityPathName.Text = result.Name;
            HumanityPathInput.Text = result.Name;
            HumanityPathCombo.SelectedItem = null;
            UpdateCustomHumanityPathInfo();
            EditHumanityPathButton.Visibility = Visibility.Visible;
            HumanityPathSuggestionsPopup.IsOpen = false;
            CreateHumanityPathButton.Visibility = Visibility.Collapsed;
            _loadingHumanityPath = false;
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
            int virtueFloor = GetHumanityVirtueFloor();
            if (next < previous && next <= virtueFloor)
            {
                var answer = ThemedDialog.Show(Window.GetWindow(this),
                    "Это значение достигает или опускается ниже уровня Человечности/Пути, обеспеченного Добродетелями (" + virtueFloor + ").\nПродолжить снижение?",
                    "Предупреждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (answer != MessageBoxResult.Yes) { HumanityPathTrack.SetTrackValue(previous); return; }
            }
            if (next != previous && _character.CreationState.ConfirmedInitialBlocks.Contains("virtues") &&
                AdvancementCheck != null && !AdvancementCheck("humanity_path", _character.HumanityPathIsCustom ? _character.CustomHumanityPathName : _character.HumanityPathId, previous, next))
            { HumanityPathTrack.SetTrackValue(previous); return; }
            _character.HumanityPathRating = next;
        }

        private void AddBioFact_Click(object sender, RoutedEventArgs e)
        {
            if (_character == null) return;
            bool backgroundsConfirmed = _character.CreationState.ConfirmedInitialBlocks.Contains("backgrounds");
            bool creationFinished = _character.CreationState.InitialPurchaseConfirmed;
            if (creationFinished && (!backgroundsConfirmed || _character.CreationState.FreebiePoints <= 0))
            {
                ThemedDialog.Show(Window.GetWindow(this), "Новый факт биографии можно добавить после создания персонажа за оставшиеся свободные пункты.", "Недостаточно свободных пунктов", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            int backgroundUsed = _character.CreationState.InitialBackgroundBlockEverConfirmed
                ? (_character.CreationState.InitialBackgroundDots == null ? 0 : _character.CreationState.InitialBackgroundDots.Values.Sum())
                : _character.BiographyFacts.Sum(x => Math.Max(0, x.Level));
            int availableBackgroundDots = Math.Max(0, CharacterCreationRules.GetCreationLimit("backgrounds") - backgroundUsed);
            if (!backgroundsConfirmed && availableBackgroundDots <= 0) return;
            var picker = new BioFactPickerWindow(_character, 5) { Owner = Window.GetWindow(this) };
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
                remove.IsEnabled = !_character.CreationState.InitialPurchaseConfirmed;
                remove.Click += (s, e) =>
                {
                    if (_character.CreationState.InitialPurchaseConfirmed) return;
                    string message = fact.IsCustom ? "Удалить пользовательский факт «" + fact.Name + "»? Вместе с ним будут безвозвратно удалены его основное описание и все описания уровней." : "Удалить факт «" + fact.Name + "» из биографии?";
                    var answer = ThemedDialog.Show(Window.GetWindow(this), message, "Удаление факта биографии", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (answer != MessageBoxResult.Yes) return;
                    bool hasRecordedPurchase = _character.CreationState.PurchaseLedger != null && _character.CreationState.PurchaseLedger.Any(t => string.Equals(t.ItemName, fact.Name, StringComparison.OrdinalIgnoreCase));
                    if (fact.Level > 0 && hasRecordedPurchase && AdvancementCheck != null && !AdvancementCheck("background", fact.Name, fact.Level, 0)) return;
                    if (_character.CreationState.InitialBackgroundDots != null) _character.CreationState.InitialBackgroundDots.Remove(fact.Name);
                    _character.BiographyFacts.Remove(fact);
                    (Window.GetWindow(this) as MainWindow)?.RefreshGenerationFromCharacter();
                    RenderBioFacts();
                };
                Grid.SetColumn(remove, 0); row.Children.Add(remove);
                var definition = BioFactService.Find(fact.Name);
                var levelInfo = new List<TrackLevelDescription>();
                if (fact.LevelDescriptions != null) levelInfo.AddRange(fact.LevelDescriptions.Select(x => new TrackLevelDescription { Level = x.Level, Description = x.Description }));
                if (fact.HolisticDescriptions != null) foreach (var item in fact.HolisticDescriptions) if (!levelInfo.Any(x => x.Level == item.Level)) levelInfo.Add(new TrackLevelDescription { Level = item.Level, Description = item.Description });
                if (definition != null && definition.Levels != null) foreach (var item in definition.Levels) if (!levelInfo.Any(x => x.Level == item.Level)) levelInfo.Add(item);
                int backgroundMax = 5;
                var track = new TrackControl { Label = fact.Name, Count = backgroundMax, LabelWidth = 170, Cascade = true, HorizontalAlignment = HorizontalAlignment.Left, InfoDescription = !string.IsNullOrWhiteSpace(fact.CustomDescription) ? fact.CustomDescription : definition == null ? null : definition.Description, InfoLevels = levelInfo };
                track.SetTrackValue(fact.Level);
                track.IsEnabled = !_character.CreationState.InitialPurchaseConfirmed;
                track.ValueChanged += (s, e) =>
                {
                    if (_character.CreationState.InitialPurchaseConfirmed) { track.SetTrackValue(fact.Level); return; }
                    int previous = fact.Level;
                    bool backgroundBlockConfirmed = _character.CreationState.ConfirmedInitialBlocks.Contains("backgrounds");
                    bool backgroundBlockWasConfirmed = _character.CreationState.InitialBackgroundBlockEverConfirmed;
                    bool hasRecordedPurchase = _character.CreationState.PurchaseLedger != null && _character.CreationState.PurchaseLedger.Any(t => string.Equals(t.ItemName, fact.Name, StringComparison.OrdinalIgnoreCase));
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
                            int paidDots = _character.CreationState.PurchaseLedger.Where(t => string.Equals(t.ItemName, fact.Name, StringComparison.OrdinalIgnoreCase) && t.Resource != "initial_pool").Sum(t => Math.Max(0, t.NewValue - t.PreviousValue));
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
                        int paidDots = _character.CreationState.PurchaseLedger.Where(t => string.Equals(t.ItemName, fact.Name, StringComparison.OrdinalIgnoreCase) && t.Resource != "initial_pool").Sum(t => Math.Max(0, t.NewValue - t.PreviousValue));
                        _character.CreationState.InitialBackgroundDots[fact.Name] = Math.Max(0, next - paidDots);
                    }
                    RenderBioFacts();
                    (Window.GetWindow(this) as MainWindow)?.RefreshGenerationFromCharacter();
                };
                Grid.SetColumn(track, 1); row.Children.Add(track); card.Child = row; BioFactsPanel.Children.Add(card);
            }
            RefreshInitialBlockControls();
            InitialPurchaseProgressChanged?.Invoke();
        }

        private void RefreshInitialBlockControls()
        {
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
            if (AddBioFactButton != null)
                AddBioFactButton.IsEnabled = backgroundsDone
                    ? (!_character.CreationState.InitialPurchaseConfirmed || _character.CreationState.FreebiePoints > 0)
                    : backgroundPool - backgroundUsed > 0;
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
                    "Не все точки распределены", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_character.CreationState.InitialVirtueRatings == null)
                _character.CreationState.InitialVirtueRatings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            bool hadInitialVirtueSnapshot = _character.CreationState.InitialVirtueRatings.Count > 0;
            if (!hadInitialVirtueSnapshot) _character.CreationState.InitialVirtueRatings.Clear();
            if (!hadInitialVirtueSnapshot)
                foreach (var virtueName in GetVirtueNames()) _character.CreationState.InitialVirtueRatings[virtueName] = GetVirtueRating(virtueName);
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
                        Resource = "initial_pool", Cost = 0, AffectsVirtueDerivedValues = true
                    });
            }
            RecalculatePrimaryVirtueDerivedValues();
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
            RefreshInitialBlockControls(); InitialPurchaseProgressChanged?.Invoke();
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
                    "Не все точки распределены", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    _character.CreationState.PurchaseLedger.Add(new PurchaseTransaction { Order = _character.CreationState.NextPurchaseOrder++, Block = "Фоны", Category = "background", ItemId = fact.Name, ItemName = fact.Name, PreviousValue = level, NewValue = level + 1, Resource = "initial_pool", Cost = 0 });
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
            BloodTrack.SetTrackValue(currentBlood);
            BloodPerTurnText.Text = "Максимум: " + BloodTrack.Count + " пунктов  •  Трата за ход: " + character.BloodPerTurn + " " +
                (character.BloodPerTurn == 1 ? "пункт" : character.BloodPerTurn >= 2 && character.BloodPerTurn <= 4 ? "пункта" : "пунктов");
        }

        private void TrackControl_Loaded(object sender, RoutedEventArgs e) { }
    }
}
