using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using VtM_LoCh_fP.Controls;

namespace VtM_LoCh_fP.Views
{
    /// <summary>Страница преимуществ персонажа.</summary>
    public partial class PageTwoView : UserControl
    {
        private CharacterProfile _character;

        public PageTwoView()
        {
            InitializeComponent();
        }

        public void InitializeCharacter(CharacterProfile character)
        {
            _character = character;
            if (_character.BiographyFacts == null)
                _character.BiographyFacts = new System.Collections.ObjectModel.ObservableCollection<BioFactEntry>();
            RenderBioFacts();
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
                    MessageBox.Show(Window.GetWindow(this), "Этот факт биографии уже добавлен.", "Факт биографии", MessageBoxButton.OK, MessageBoxImage.Information);
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
                var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 1, 0, 1) };
                var remove = new Button
                {
                    Content = "×",
                    Width = 28,
                    Height = 27,
                    Margin = new Thickness(6, 0, 0, 0),
                    Padding = new Thickness(0),
                    ToolTip = "Удалить факт"
                };
                remove.Click += (s, e) =>
                {
                    string message = fact.IsCustom
                        ? "Удалить пользовательский факт «" + fact.Name + "»? Вместе с ним будут безвозвратно удалены его основное описание и все описания уровней."
                        : "Удалить факт «" + fact.Name + "» из биографии?";
                    var answer = MessageBox.Show(Window.GetWindow(this), message, "Удаление факта биографии", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (answer != MessageBoxResult.Yes) return;
                    _character.BiographyFacts.Remove(fact);
                    RenderBioFacts();
                };
                DockPanel.SetDock(remove, Dock.Right);
                row.Children.Add(remove);

                var definition = BioFactService.Find(fact.Name);
                var levelInfo = new System.Collections.Generic.List<TrackLevelDescription>();
                if (fact.LevelDescriptions != null)
                    levelInfo.AddRange(fact.LevelDescriptions.Select(x => new TrackLevelDescription { Level = x.Level, Description = x.Description }));
                if (fact.HolisticDescriptions != null)
                    foreach (var item in fact.HolisticDescriptions)
                        if (!levelInfo.Any(x => x.Level == item.Level)) levelInfo.Add(new TrackLevelDescription { Level = item.Level, Description = item.Description });
                if (definition != null && definition.Levels != null)
                    foreach (var item in definition.Levels)
                        if (!levelInfo.Any(x => x.Level == item.Level)) levelInfo.Add(item);

                var track = new TrackControl
                {
                    Label = fact.Name,
                    Count = 5,
                    LabelWidth = 170,
                    Cascade = true,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    InfoDescription = !string.IsNullOrWhiteSpace(fact.CustomDescription) ? fact.CustomDescription : definition?.Description,
                    InfoLevels = levelInfo
                };
                track.SetTrackValue(fact.Level);
                track.ValueChanged += (s, e) => fact.Level = track.Value;
                row.Children.Add(track);
                BioFactsPanel.Children.Add(row);
            }
        }

        private void TrackControl_Loaded(object sender, RoutedEventArgs e)
        {
        }
    }
}
