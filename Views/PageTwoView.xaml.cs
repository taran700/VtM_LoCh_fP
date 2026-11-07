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

        public PageTwoView()
        {
            InitializeComponent();
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
            AggravatedDamageToggle.IsChecked = _character.AllowAggravatedDamage;
            HealthTrack.LoadStates(_character.HealthDamageStates);
            UpdateHealthHeader();
            RenderBioFacts();
            LoadHumanityPath();
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
            // Выбор применяется по MouseUp, чтобы не закрывать список при навигации клавиатурой.
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
            if (!_loadingHumanityPath && _character != null)
                _character.HumanityPathRating = HumanityPathTrack.Value;
        }

        private void AddBioFact_Click(object sender, RoutedEventArgs e)
        {
            if (_character == null) return;
            var picker = new BioFactPickerWindow(_character) { Owner = Window.GetWindow(this) };
            if (picker.ShowDialog() != true || picker.Result == null) return;
            foreach (var existing in _character.BiographyFacts)
            {
                if (string.Equals(existing.Name, picker.Result.Name, StringComparison.OrdinalIgnoreCase))
                {
                    ThemedDialog.Show(Window.GetWindow(this), "Этот факт биографии уже добавлен.", "Факт биографии", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }
            _character.BiographyFacts.Add(picker.Result);
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
                    string message = fact.IsCustom ? "Удалить пользовательский факт «" + fact.Name + "»? Вместе с ним будут безвозвратно удалены его основное описание и все описания уровней." : "Удалить факт «" + fact.Name + "» из биографии?";
                    var answer = ThemedDialog.Show(Window.GetWindow(this), message, "Удаление факта биографии", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (answer != MessageBoxResult.Yes) return;
                    _character.BiographyFacts.Remove(fact); RenderBioFacts();
                };
                Grid.SetColumn(remove, 0); row.Children.Add(remove);
                var definition = BioFactService.Find(fact.Name);
                var levelInfo = new List<TrackLevelDescription>();
                if (fact.LevelDescriptions != null) levelInfo.AddRange(fact.LevelDescriptions.Select(x => new TrackLevelDescription { Level = x.Level, Description = x.Description }));
                if (fact.HolisticDescriptions != null) foreach (var item in fact.HolisticDescriptions) if (!levelInfo.Any(x => x.Level == item.Level)) levelInfo.Add(new TrackLevelDescription { Level = item.Level, Description = item.Description });
                if (definition != null && definition.Levels != null) foreach (var item in definition.Levels) if (!levelInfo.Any(x => x.Level == item.Level)) levelInfo.Add(item);
                var track = new TrackControl { Label = fact.Name, Count = 5, LabelWidth = 170, Cascade = true, HorizontalAlignment = HorizontalAlignment.Left, InfoDescription = !string.IsNullOrWhiteSpace(fact.CustomDescription) ? fact.CustomDescription : definition == null ? null : definition.Description, InfoLevels = levelInfo };
                track.SetTrackValue(fact.Level);
                track.ValueChanged += (s, e) => fact.Level = track.Value;
                Grid.SetColumn(track, 1); row.Children.Add(track); card.Child = row; BioFactsPanel.Children.Add(card);
            }
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
