using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using System.Windows.Controls.Primitives;
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
                var card = new Border
                {
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(5, 3, 7, 3),
                    Margin = new Thickness(0, 2, 0, 3)
                };
                card.SetResourceReference(Border.BackgroundProperty, "CardHeaderBrush");
                card.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var removeIcon = new TextBlock
                {
                    Text = "\uE74D",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 16,
                    Foreground = new SolidColorBrush(Color.FromRgb(190, 35, 35)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var remove = new Button
                {
                    Content = removeIcon,
                    Width = 30,
                    Height = 30,
                    Margin = new Thickness(0, 0, 7, 0),
                    Padding = new Thickness(0),
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = "Удалить факт"
                };
                remove.Click += (s, e) =>
                {
                    string message = fact.IsCustom
                        ? "Удалить пользовательский факт «" + fact.Name + "»? Вместе с ним будут безвозвратно удалены его основное описание и все описания уровней."
                        : "Удалить факт «" + fact.Name + "» из биографии?";
                    var answer = ThemedDialog.Show(Window.GetWindow(this), message, "Удаление факта биографии", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (answer != MessageBoxResult.Yes) return;
                    _character.BiographyFacts.Remove(fact);
                    RenderBioFacts();
                };
                Grid.SetColumn(remove, 0);
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
                Grid.SetColumn(track, 1);
                row.Children.Add(track);
                card.Child = row;
                BioFactsPanel.Children.Add(card);
            }
        }

        private void TrackControl_Loaded(object sender, RoutedEventArgs e)
        {
        }
    }
}
