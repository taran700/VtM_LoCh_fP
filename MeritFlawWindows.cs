using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VtM_LoCh_fP
{
    internal sealed class MeritFlawPickerWindow : Window
    {
        private readonly CharacterProfile _character;
        private readonly string _type;
        private readonly StackPanel _list;
        private readonly TextBlock _pointsText;
        private readonly TextBlock _emptyText;
        private readonly List<MeritFlawDefinition> _definitions;

        public MeritFlawPickerWindow(Window owner, CharacterProfile character, string type)
        {
            _character = character;
            _type = type;
            _definitions = MeritFlawService.GetAll().Where(x => x.Type == type).OrderBy(x => x.Name).ToList();
            Title = type == "flaw" ? "Каталог недостатков" : "Каталог достоинств";
            Width = 780; Height = 740; MinWidth = 560; MinHeight = 540;
            WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
            Owner = owner;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = false;
            ThemeManager.AttachWindow(this);

            var root = new DockPanel { Margin = new Thickness(16) };
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var close = HumanityPathEditorWindow.MakeButton("Закрыть", false);
            close.Click += (s, e) => Close();
            footer.Children.Add(close);
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);

            var header = new DockPanel { LastChildFill = true, Margin = new Thickness(2, 0, 2, 12) };
            _pointsText = new TextBlock { FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            header.Children.Add(_pointsText);
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var content = new Grid();
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _emptyText = new TextBlock { Text = "Все доступные варианты уже выбраны.", Foreground = BrushResource("MutedBrush"), Margin = new Thickness(4, 8, 4, 8), Visibility = Visibility.Collapsed };
            content.Children.Add(_emptyText);
            _list = new StackPanel();
            var listScroll = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetRow(listScroll, 1);
            content.Children.Add(listScroll);
            root.Children.Add(content);
            Content = root;
            RefreshList();
        }

        private void RefreshList()
        {
            _list.Children.Clear();
            var owned = _character.MeritsFlaws ?? new System.Collections.ObjectModel.ObservableCollection<CharacterMeritFlaw>();
            var selectedIds = new HashSet<string>(owned.Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
            var available = _definitions.Where(x => !selectedIds.Contains(x.Id)).ToList();
            _emptyText.Visibility = available.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            int flaws = owned.Where(x => x.Type == "flaw").Sum(x => x.Cost);
            int merits = owned.Where(x => x.Type == "merit").Sum(x => x.Cost);
            int free = _character.MeritFlawBasePoints + flaws - merits;
            _pointsText.Text = "Свободные очки: " + free + "   ·   Недостатки: " + flaws + "   ·   Достоинства: " + merits;

            foreach (var item in available)
            {
                var button = new Button
                {
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Padding = new Thickness(12, 9, 12, 9),
                    Margin = new Thickness(0, 0, 0, 6),
                    BorderThickness = new Thickness(1),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                button.SetResourceReference(Control.BackgroundProperty, "InputBrush");
                button.SetResourceReference(Control.ForegroundProperty, "TextBrush");
                button.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var name = new TextBlock { Text = item.Name, FontSize = 17, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
                var costs = new TextBlock { Text = FormatCosts(item), FontSize = 16, FontWeight = FontWeights.Bold, Foreground = BrushResource(item.Type == "flaw" ? "AccentBrush" : "HeadingBrush"), Margin = new Thickness(14, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center };
                row.Children.Add(name);
                Grid.SetColumn(costs, 1); row.Children.Add(costs);
                button.Content = row;
                button.ToolTip = item.Description;
                button.Click += (s, e) => ShowDetails(item);
                _list.Children.Add(button);
            }
        }

        private void ShowDetails(MeritFlawDefinition item)
        {
            var detail = new MeritFlawDescriptionWindow(this, item, _character);
            if (detail.ShowDialog() == true)
            {
                int cost = detail.SelectedCost;
                if (_character.MeritsFlaws == null)
                    _character.MeritsFlaws = new System.Collections.ObjectModel.ObservableCollection<CharacterMeritFlaw>();
                _character.MeritsFlaws.Add(new CharacterMeritFlaw { Id = item.Id, Name = item.Name, Type = item.Type, Cost = cost, Description = item.Description });
                RefreshList();
            }
        }

        private static string FormatCosts(MeritFlawDefinition item)
        {
            string sign = item.Type == "flaw" ? "+" : "-";
            var values = item.Costs.OrderBy(x => x).Select(x => sign + x).ToList();
            return values.Count == 1 ? values[0] : string.Join(" / ", values);
        }

        internal static Brush BrushResource(string key)
        {
            return Application.Current.Resources[key] as Brush ?? Brushes.Gray;
        }
    }

    internal sealed class MeritFlawDescriptionWindow : Window
    {
        private readonly CharacterProfile _character;
        private readonly MeritFlawDefinition _definition;
        private readonly ComboBox _costPicker;
        public int SelectedCost { get; private set; }

        public MeritFlawDescriptionWindow(Window owner, MeritFlawDefinition definition, CharacterProfile character)
        {
            _character = character;
            _definition = definition;
            Title = definition.Name;
            Width = 760; Height = 620; MinWidth = 560; MinHeight = 440;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Owner = owner;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = false;
            ThemeManager.AttachWindow(this);

            var root = new DockPanel { Margin = new Thickness(18) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var close = HumanityPathEditorWindow.MakeButton("Закрыть", false);
            close.Click += (s, e) => DialogResult = false;
            buttons.Children.Add(close);
            var buy = HumanityPathEditorWindow.MakeButton("Купить", true);
            buy.Margin = new Thickness(8, 0, 0, 0);
            buy.Click += Buy_Click;
            buttons.Children.Add(buy);
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);

            var body = new StackPanel();
            body.Children.Add(new TextBlock { Text = definition.Name, FontSize = 25, FontWeight = FontWeights.Bold, Foreground = MeritFlawPickerWindow.BrushResource("HeadingBrush"), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
            body.Children.Add(new TextBlock { Text = definition.Type == "flaw" ? "НЕДОСТАТОК" : "ДОСТОИНСТВО", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = MeritFlawPickerWindow.BrushResource("MutedBrush"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 14) });
            body.Children.Add(new TextBlock { Text = definition.Description, FontSize = 15, LineHeight = 22, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 0, 2, 16) });

            if (definition.Costs.Count > 1)
            {
                body.Children.Add(new TextBlock { Text = "Стоимость", Style = (Style)Application.Current.FindResource("FieldLabelStyle") });
                _costPicker = new ComboBox { MinHeight = 34, ItemsSource = definition.Costs.OrderBy(x => x).ToList(), SelectedIndex = 0, Margin = new Thickness(1, 0, 4, 10) };
                body.Children.Add(_costPicker);
            }
            else _costPicker = null;

            var info = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = MeritFlawPickerWindow.BrushResource("MutedBrush"), Margin = new Thickness(2, 4, 2, 0) };
            body.Children.Add(info);
            var owned = character.MeritsFlaws ?? new System.Collections.ObjectModel.ObservableCollection<CharacterMeritFlaw>();
            int flaws = owned.Where(x => x.Type == "flaw").Sum(x => x.Cost);
            int merits = owned.Where(x => x.Type == "merit").Sum(x => x.Cost);
            int free = character.MeritFlawBasePoints + flaws - merits;
            info.Text = "Свободные очки сейчас: " + free + ". После покупки: " + (definition.Type == "flaw" ? "очков станет больше." : "очков станет меньше.");
            root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Content = root;
        }

        private void Buy_Click(object sender, RoutedEventArgs e)
        {
            int cost = _costPicker == null ? _definition.Costs[0] : Convert.ToInt32(_costPicker.SelectedItem);
            var owned = _character.MeritsFlaws ?? new System.Collections.ObjectModel.ObservableCollection<CharacterMeritFlaw>();
            if (owned.Any(x => string.Equals(x.Id, _definition.Id, StringComparison.OrdinalIgnoreCase)))
            {
                ThemedDialog.Show(this, "Эта характеристика уже выбрана.", "Покупка", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            int flaws = owned.Where(x => x.Type == "flaw").Sum(x => x.Cost);
            int merits = owned.Where(x => x.Type == "merit").Sum(x => x.Cost);
            int free = _character.MeritFlawBasePoints + flaws - merits;
            if (_definition.Type == "merit" && cost > free)
            {
                ThemedDialog.Show(this, "Недостаточно свободных очков для покупки этого достоинства.", "Недостаточно очков", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_definition.Type == "flaw" && AppSettings.RuleOfSeven && flaws + cost > 7)
            {
                ThemedDialog.Show(this, "Правило Семи запрещает брать недостатки суммарной стоимостью более 7 очков.", "Правило Семи", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var conflicts = owned.Where(x => _definition.IncompatibleWith.Contains(x.Id, StringComparer.OrdinalIgnoreCase)).Select(x => x.Name).ToList();
            if (conflicts.Count > 0)
            {
                ThemedDialog.Show(this, "Эта характеристика несовместима с уже выбранной: " + string.Join(", ", conflicts) + ".", "Несовместимые характеристики", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var reverseConflict = MeritFlawService.GetAll().Where(x => owned.Any(y => y.Id == x.Id) && x.IncompatibleWith.Contains(_definition.Id, StringComparer.OrdinalIgnoreCase)).Select(x => x.Name).ToList();
            if (reverseConflict.Count > 0)
            {
                ThemedDialog.Show(this, "Уже выбранная характеристика несовместима с этой: " + string.Join(", ", reverseConflict) + ".", "Несовместимые характеристики", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            SelectedCost = cost;
            DialogResult = true;
        }
    }
}
