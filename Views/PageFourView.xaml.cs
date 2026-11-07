using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VtM_LoCh_fP.Controls;

namespace VtM_LoCh_fP.Views
{
    /// <summary>Страница достоинств, недостатков и биографии персонажа.</summary>
    public partial class PageFourView : UserControl
    {
        private CharacterProfile _character;
        private bool _loading;

        public PageFourView()
        {
            InitializeComponent();
        }

        public void InitializeCharacter(CharacterProfile character)
        {
            _character = character;
            if (_character.MeritsFlaws == null)
                _character.MeritsFlaws = new System.Collections.ObjectModel.ObservableCollection<CharacterMeritFlaw>();
            if (_character.MeritFlawBasePoints < 0)
                _character.MeritFlawBasePoints = 15;

            _loading = true;
            BasePointsInput.Visibility = IsFreeNeonate ? Visibility.Visible : Visibility.Collapsed;
            BasePointsInput.Text = _character.MeritFlawBasePoints.ToString();
            SetField("BirthDate", _character.BiographyBirthDate);
            SetField("DeathDate", _character.BiographyDeathDate);
            SetField("TrueAge", _character.BiographyTrueAge);
            SetField("ApparentAge", _character.BiographyApparentAge);
            SetField("Height", _character.BiographyHeight);
            SetField("Weight", _character.BiographyWeight);
            BackstoryField.InputText = _character.BiographyBackstory ?? string.Empty;
            AppearanceField.InputText = _character.BiographyAppearance ?? string.Empty;
            _loading = false;
            RenderMeritsFlaws();
        }

        private bool IsFreeNeonate
        {
            get { return string.Equals(_character == null ? null : _character.CreationMode, "Свободный Неонат", StringComparison.OrdinalIgnoreCase); }
        }

        private void AddFlaw_Click(object sender, RoutedEventArgs e)
        {
            if (_character == null) return;
            new MeritFlawPickerWindow(Window.GetWindow(this), _character, "flaw").ShowDialog();
            RenderMeritsFlaws();
        }

        private void AddMerit_Click(object sender, RoutedEventArgs e)
        {
            if (_character == null) return;
            new MeritFlawPickerWindow(Window.GetWindow(this), _character, "merit").ShowDialog();
            RenderMeritsFlaws();
        }

        private void RenderMeritsFlaws()
        {
            if (_character == null) return;
            RenderList(FlawsPanel, _character.MeritsFlaws.Where(x => x.Type == "flaw").ToList());
            RenderList(MeritsPanel, _character.MeritsFlaws.Where(x => x.Type == "merit").ToList());
            int flaws = _character.MeritsFlaws.Where(x => x.Type == "flaw").Sum(x => x.Cost);
            int merits = _character.MeritsFlaws.Where(x => x.Type == "merit").Sum(x => x.Cost);
            FreePointsText.Text = "Свободные очки: " + (_character.MeritFlawBasePoints + flaws - merits) + "   ·   Недостатки: +" + flaws + "   ·   Достоинства: -" + merits;
        }

        private void RenderList(Panel panel, System.Collections.Generic.IList<CharacterMeritFlaw> entries)
        {
            panel.Children.Clear();
            foreach (var entry in entries)
            {
                var card = new Border { Padding = new Thickness(10, 7, 8, 7), Margin = new Thickness(0, 0, 0, 6), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5) };
                card.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
                card.SetResourceReference(Border.BackgroundProperty, "CardBgBrush");
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var name = new TextBlock { Text = entry.Name, FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
                row.Children.Add(name);
                var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var cost = new TextBlock { Text = (entry.Type == "flaw" ? "+" : "-") + entry.Cost, FontSize = 15, FontWeight = FontWeights.Bold, Foreground = BrushResource("AccentBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
                right.Children.Add(cost);
                var remove = new Button { Content = "×", Width = 28, Height = 28, Padding = new Thickness(0), FontSize = 19, Cursor = System.Windows.Input.Cursors.Hand, ToolTip = "Убрать характеристику" };
                remove.SetResourceReference(Control.BackgroundProperty, "InputBrush");
                remove.SetResourceReference(Control.ForegroundProperty, "TextBrush");
                remove.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
                
                right.Children.Add(remove);
                Grid.SetColumn(right, 1); row.Children.Add(right);
                card.Child = row;
                card.Cursor = System.Windows.Input.Cursors.Hand;
                card.MouseLeftButtonUp += (s, e) =>
                {
                    DependencyObject source = e.OriginalSource as DependencyObject;
                    while (source != null)
                    {
                        if (source is Button) return;
                        source = VisualTreeHelper.GetParent(source);
                    }
                    ShowOwnedMeritFlaw(entry);
                    e.Handled = true;
                };
                remove.Click += (s, e) => ShowOwnedMeritFlaw(entry);
                panel.Children.Add(card);
            }
            if (entries.Count == 0)
                panel.Children.Add(new TextBlock { Text = "Пока ничего не выбрано.", Foreground = BrushResource("MutedBrush"), Margin = new Thickness(2, 3, 2, 5) });
        }

        private void ShowOwnedMeritFlaw(CharacterMeritFlaw entry)
        {
            var window = new MeritFlawDescriptionWindow(Window.GetWindow(this), entry, () =>
            {
                _character.MeritsFlaws.Remove(entry);
                RenderMeritsFlaws();
            });
            window.ShowDialog();
        }

        private void BasePointsInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || _character == null || !IsFreeNeonate) return;
            int value;
            if (!int.TryParse(BasePointsInput.Text, out value) || value < 0 || value > 99) return;
            _character.MeritFlawBasePoints = value;
            RenderMeritsFlaws();
        }

        private void BiographyField_InputTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || _character == null) return;
            var field = sender as FieldControl;
            if (field == null) return;
            string value = field.InputText;
            switch (Convert.ToString(field.Tag))
            {
                case "BirthDate": _character.BiographyBirthDate = value; break;
                case "DeathDate": _character.BiographyDeathDate = value; break;
                case "TrueAge": _character.BiographyTrueAge = value; break;
                case "ApparentAge": _character.BiographyApparentAge = value; break;
                case "Height": _character.BiographyHeight = value; break;
                case "Weight": _character.BiographyWeight = value; break;
                case "Backstory": _character.BiographyBackstory = value; break;
                case "Appearance": _character.BiographyAppearance = value; break;
            }
        }

        private void SetField(string tag, string value)
        {
            FieldControl field = null;
            switch (tag)
            {
                case "BirthDate": field = BirthDateField; break;
                case "DeathDate": field = DeathDateField; break;
                case "TrueAge": field = TrueAgeField; break;
                case "ApparentAge": field = ApparentAgeField; break;
                case "Height": field = HeightField; break;
                case "Weight": field = WeightField; break;
            }
            if (field != null) field.InputText = value ?? string.Empty;
        }

        private static Brush BrushResource(string key)
        {
            return Application.Current.Resources[key] as Brush ?? Brushes.Gray;
        }

        private void BackstoryField_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}
