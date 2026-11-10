using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using VtM_LoCh_fP.Controls;

namespace VtM_LoCh_fP
{
    internal sealed class BioFactPickerWindow : Window
    {
        private readonly bool _freeMode;
        private readonly ComboBox _facts;
        private readonly TextBox _factInput;
        private readonly ListBox _suggestions;
        private readonly TrackControl _levelTrack;
        private readonly TextBlock _description;
        private readonly TextBlock _levelTitle;
        private readonly TextBlock _levelDescription;
        private readonly Button _baseDescriptionButton;
        private readonly Button _levelDescriptionButton;
        private readonly Button _holisticDescriptionButton;
        private readonly List<BioFactDefinition> _definitions;
        private BioFactDefinition _selectedDefinition;
        private string _customDescription = string.Empty;
        private readonly List<BioFactLevelDescription> _customLevels = new List<BioFactLevelDescription>();
        private readonly List<BioFactLevelDescription> _holisticLevels = new List<BioFactLevelDescription>();
        private bool _updatingInput;
        private string _lastInputName = string.Empty;

        public BioFactEntry Result { get; private set; }

        public BioFactPickerWindow(CharacterProfile character, int maximumLevel = 5)
        {
            _freeMode = string.Equals(character.CreationMode, "Свободный Неонат", StringComparison.OrdinalIgnoreCase);
            _definitions = BioFactService.GetAll();
            if (!_freeMode && !string.IsNullOrWhiteSpace(character.ClanName) &&
                character.ClanName.IndexOf("каити", StringComparison.OrdinalIgnoreCase) >= 0)
                _definitions = _definitions.Where(x => !x.CaitiffRestricted).ToList();

            Title = "Добавить факт биографии";
            Width = 680; Height = 650; MinWidth = 570; MinHeight = 520;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (Brush)Application.Current.FindResource("BgBrush");
            Foreground = (Brush)Application.Current.FindResource("TextBrush");
            ThemeManager.AttachWindow(this);

            var root = new Grid { Margin = new Thickness(22) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.Children.Add(new TextBlock { Text = "ФАКТ БИОГРАФИИ", FontSize = 21, FontWeight = FontWeights.Bold, Foreground = (Brush)Application.Current.FindResource("HeadingBrush"), Margin = new Thickness(0, 0, 0, 14) });

            var selectionPanel = new StackPanel();
            Grid.SetRow(selectionPanel, 1);
            selectionPanel.Children.Add(new TextBlock { Text = _freeMode ? "Введите название или выберите подсказку" : "Выберите факт из списка", Margin = new Thickness(0, 0, 0, 6) });
            if (_freeMode)
            {
                _factInput = new TextBox { Height = 34, FontSize = 14, Padding = new Thickness(7, 4, 7, 4), Margin = new Thickness(0, 0, 0, 3), HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Top, TextAlignment = TextAlignment.Left };
                _factInput.TextChanged += FactInput_TextChanged;
                _suggestions = new ListBox { MaxHeight = 135, Visibility = Visibility.Collapsed, DisplayMemberPath = "Name", Margin = new Thickness(0, 0, 0, 8) };
                var suggestionStyle = new Style(typeof(ListBoxItem));
                suggestionStyle.Setters.Add(new Setter(ToolTipService.ToolTipProperty, new System.Windows.Data.Binding("Description")));
                _suggestions.ItemContainerStyle = suggestionStyle;
                _suggestions.SelectionChanged += Suggestion_SelectionChanged;
                selectionPanel.Children.Add(_factInput);
                selectionPanel.Children.Add(_suggestions);
            }
            else
            {
                _facts = new ComboBox { Height = 34, IsEditable = false, IsTextSearchEnabled = true, ItemsSource = _definitions, DisplayMemberPath = "Name", Margin = new Thickness(0, 0, 0, 10) };
                ComboBoxWheelSupport.Enable(_facts);
                var itemStyle = new Style(typeof(ComboBoxItem));
                itemStyle.Setters.Add(new Setter(ToolTipService.ToolTipProperty, new System.Windows.Data.Binding("Description")));
                _facts.ItemContainerStyle = itemStyle;
                _facts.SelectionChanged += (s, e) => { _selectedDefinition = _facts.SelectedItem as BioFactDefinition; UpdatePreview(); };
                selectionPanel.Children.Add(_facts);
            }

            var levelRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 0, 6) };
            _levelTrack = new TrackControl { Label = "Уровень", Count = Math.Max(0, Math.Min(5, maximumLevel)), LabelWidth = 90, Width = 255, HorizontalAlignment = HorizontalAlignment.Left, Cascade = true };
            _levelTrack.ValueChanged += (s, e) => UpdatePreview();
            levelRow.Children.Add(_levelTrack);
            selectionPanel.Children.Add(levelRow);

            var descriptionButtons = new WrapPanel { Margin = new Thickness(0, 2, 0, 8) };
            _baseDescriptionButton = MakeButton("Добавить описание", (s, e) => EditBaseDescription());
            _levelDescriptionButton = MakeButton("Добавить описание уровня", (s, e) => EditLevelDescription());
            _holisticDescriptionButton = MakeButton("Добавить целостное описание", (s, e) => EditHolisticDescription());
            descriptionButtons.Children.Add(_baseDescriptionButton);
            descriptionButtons.Children.Add(_levelDescriptionButton);
            descriptionButtons.Children.Add(_holisticDescriptionButton);
            selectionPanel.Children.Add(descriptionButtons);
            root.Children.Add(selectionPanel);

            var descriptionPanel = new StackPanel();
            Grid.SetRow(descriptionPanel, 2);
            _description = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 14, Margin = new Thickness(0, 4, 0, 14) };
            _levelTitle = new TextBlock { FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = (Brush)Application.Current.FindResource("HeadingBrush"), Margin = new Thickness(0, 0, 0, 6) };
            _levelDescription = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 14 };
            var descriptionCard = new Border { Padding = new Thickness(14), Background = (Brush)Application.Current.FindResource("CardBgBrush"), BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Child = new StackPanel { Children = { _description, _levelTitle, _levelDescription } } };
            descriptionPanel.Children.Add(descriptionCard);
            root.Children.Add(descriptionPanel);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
            Grid.SetRow(buttons, 3);
            var confirm = new Button { Content = "Подтвердить", MinWidth = 120, Padding = new Thickness(14, 8, 14, 8), IsDefault = true };
            confirm.Click += Confirm_Click;
            var cancel = new Button { Content = "Отмена", MinWidth = 100, Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
            buttons.Children.Add(confirm); buttons.Children.Add(cancel); root.Children.Add(buttons);
            Content = root;

            _baseDescriptionButton.Visibility = _freeMode ? Visibility.Visible : Visibility.Collapsed;
            _levelDescriptionButton.Visibility = _freeMode ? Visibility.Visible : Visibility.Collapsed;
            _holisticDescriptionButton.Visibility = _freeMode ? Visibility.Visible : Visibility.Collapsed;
            if (!_freeMode && _definitions.Count > 0) _facts.SelectedIndex = 0;
            UpdatePreview();
        }

        private static Button MakeButton(string text, RoutedEventHandler click)
        {
            var button = new Button { Content = text, Padding = new Thickness(9, 5, 9, 5), Margin = new Thickness(0, 0, 7, 5) };
            button.Click += click;
            return button;
        }

        private string CurrentName => _freeMode ? (_factInput.Text ?? string.Empty).Trim() : (_selectedDefinition?.Name ?? string.Empty);
        private bool IsCustomFact => _freeMode && (_selectedDefinition == null || !string.Equals(CurrentName, _selectedDefinition.Name, StringComparison.OrdinalIgnoreCase));

        private void FactInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_updatingInput) return;
            string query = (_factInput.Text ?? string.Empty).Trim();
            if (!string.Equals(query, _lastInputName, StringComparison.OrdinalIgnoreCase))
            {
                _customDescription = string.Empty;
                _customLevels.Clear();
                _holisticLevels.Clear();
            }
            _lastInputName = query;
            _selectedDefinition = null;
            var matches = string.IsNullOrWhiteSpace(query) ? new List<BioFactDefinition>() : _definitions.Where(x => x.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).Take(12).ToList();
            _suggestions.ItemsSource = matches;
            _suggestions.Visibility = matches.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdatePreview();
        }

        private void Suggestion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = _suggestions.SelectedItem as BioFactDefinition;
            if (selected == null) return;
            if (!string.Equals(_lastInputName, selected.Name, StringComparison.OrdinalIgnoreCase))
            {
                _customDescription = string.Empty;
                _customLevels.Clear();
                _holisticLevels.Clear();
            }
            _updatingInput = true;
            _factInput.Text = selected.Name;
            _factInput.CaretIndex = _factInput.Text.Length;
            _updatingInput = false;
            _lastInputName = selected.Name;
            _selectedDefinition = selected;
            _suggestions.Visibility = Visibility.Collapsed;
            _suggestions.SelectedItem = null;
            UpdatePreview();
        }

        private void EditBaseDescription()
        {
            if (string.IsNullOrWhiteSpace(CurrentName))
            {
                ThemedDialog.Show(this, "Сначала введите название факта.", "Факт биографии", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var value = BioFactDescriptionEditorWindow.EditSingle(this, "Описание факта — " + CurrentName, _customDescription);
            if (value != null) { _customDescription = value; UpdatePreview(); }
        }

        private void EditLevelDescription()
        {
            if (string.IsNullOrWhiteSpace(CurrentName) || _levelTrack.Value < 1)
            {
                ThemedDialog.Show(this, "Сначала укажите название факта и выберите его уровень.", "Факт биографии", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            int level = _levelTrack.Value;
            var old = _customLevels.FirstOrDefault(x => x.Level == level)?.Description;
            var value = BioFactDescriptionEditorWindow.EditSingle(this, "Описание уровня " + level, old);
            if (value == null) return;
            _customLevels.RemoveAll(x => x.Level == level);
            if (!string.IsNullOrWhiteSpace(value)) _customLevels.Add(new BioFactLevelDescription { Level = level, Description = value });
            UpdatePreview();
        }

        private void EditHolisticDescription()
        {
            if (string.IsNullOrWhiteSpace(CurrentName))
            {
                ThemedDialog.Show(this, "Сначала введите название факта.", "Факт биографии", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var edited = BioFactDescriptionEditorWindow.EditHolistic(this, _holisticLevels);
            if (edited == null) return;
            _holisticLevels.Clear(); _holisticLevels.AddRange(edited);
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            var selected = _selectedDefinition;
            _baseDescriptionButton.Visibility = _freeMode && IsCustomFact && !string.IsNullOrWhiteSpace(CurrentName) ? Visibility.Visible : Visibility.Collapsed;
            _description.Text = selected != null ? selected.Description : (_freeMode && CurrentName.Length > 0 ? (_customDescription.Length > 0 ? _customDescription : "Пользовательский факт. Добавьте собственное описание.") : "Выберите факт, чтобы увидеть его описание.");

            int currentLevel = _levelTrack.Value;
            _levelDescriptionButton.Content = _customLevels.Any(x => x.Level == currentLevel) ? "Изменить описание уровня" : "Добавить описание уровня";
            var level = _customLevels.FirstOrDefault(x => x.Level == currentLevel);
            var holistic = _holisticLevels.FirstOrDefault(x => x.Level == currentLevel);
            var databaseLevel = selected?.Levels?.FirstOrDefault(x => x.Level == currentLevel);
            if (currentLevel > 0)
            {
                _levelTitle.Text = "Уровень " + currentLevel;
                _levelDescription.Text = level?.Description ?? holistic?.Description ?? databaseLevel?.Description ?? "Для этого уровня описание пока не задано.";
            }
            else
            {
                _levelTitle.Text = "Уровень не выбран";
                _levelDescription.Text = "Отметьте точки, чтобы увидеть описание выбранного уровня.";
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            string name = CurrentName;
            if (string.IsNullOrWhiteSpace(name))
            {
                ThemedDialog.Show(this, "Выберите или введите факт биографии.", "Факт биографии", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!_freeMode && _selectedDefinition == null)
            {
                ThemedDialog.Show(this, "В режиме Птенца можно выбрать только факт из базы данных.", "Факт биографии", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            int selectedLevel = _levelTrack.Value;
            if (selectedLevel < 1 || selectedLevel > _levelTrack.Count)
            {
                ThemedDialog.Show(this, "Доступных точек для нового факта биографии больше нет.", "Факт биографии", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Result = new BioFactEntry
            {
                Name = name,
                Level = selectedLevel,
                IsCustom = IsCustomFact,
                CustomDescription = IsCustomFact ? _customDescription : string.Empty,
                LevelDescriptions = _customLevels.Select(x => new BioFactLevelDescription { Level = x.Level, Description = x.Description }).ToList(),
                HolisticDescriptions = _holisticLevels.Select(x => new BioFactLevelDescription { Level = x.Level, Description = x.Description }).ToList()
            };
            DialogResult = true;
        }
    }

    internal static class ComboBoxWheelSupport
    {
        public static void Enable(ComboBox combo)
        {
            if (combo == null) return;
            combo.DropDownOpened += (sender, args) =>
            {
                combo.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var popup = combo.Template.FindName("PART_Popup", combo) as Popup;
                    var scrollViewer = popup == null ? null : FindScrollViewer(popup.Child);
                    if (scrollViewer == null) return;
                    scrollViewer.PreviewMouseWheel -= ScrollViewer_PreviewMouseWheel;
                    scrollViewer.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            };
        }

        private static void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var viewer = sender as ScrollViewer;
            if (viewer == null || e.Delta == 0) return;
            viewer.ScrollToVerticalOffset(viewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private static ScrollViewer FindScrollViewer(DependencyObject parent)
        {
            if (parent == null) return null;
            var direct = parent as ScrollViewer;
            if (direct != null) return direct;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var found = FindScrollViewer(VisualTreeHelper.GetChild(parent, i));
                if (found != null) return found;
            }
            return null;
        }
    }
}
