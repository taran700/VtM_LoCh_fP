using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace VtM_LoCh_fP
{
    internal sealed class CharacterSelectionWindow : Window
    {
        private static readonly ObservableCollection<CharacterProfile> Characters = new ObservableCollection<CharacterProfile>();
        private readonly ListBox _list;

        public CharacterSelectionWindow()
        {
            Title = "VtM V20 — Персонажи"; Width = 560; Height = 430; MinWidth = 460; MinHeight = 340;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ThemeManager.AttachWindow(this);
            var root = new Grid { Margin = new Thickness(20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var title = new TextBlock { Text = "Персонажи", FontSize = 24, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,14) };
            title.SetResourceReference(TextBlock.ForegroundProperty, "HeadingBrush");
            root.Children.Add(title);
            _list = new ListBox { ItemsSource = Characters, Margin = new Thickness(0,0,0,14), Padding = new Thickness(6) }; Grid.SetRow(_list, 1); root.Children.Add(_list);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var create = new Button { Content = "Создать персонажа", Padding = new Thickness(14,8,14,8), Margin = new Thickness(0,0,8,0) }; create.Click += Create_Click;
            var open = new Button { Content = "Открыть", Padding = new Thickness(14,8,14,8) }; open.Click += Open_Click;
            buttons.Children.Add(create); buttons.Children.Add(open); Grid.SetRow(buttons, 2); root.Children.Add(buttons); Content = root;
            _list.MouseDoubleClick += (s,e) => OpenSelected();
            Closed += (s,e) => { if (Application.Current.MainWindow == this) Application.Current.Shutdown(); };
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CharacterCreateWindow { Owner = this };
            if (dialog.ShowDialog() != true) return;
            var character = new CharacterProfile { Name = dialog.CharacterName, CreationMode = dialog.CreationMode };
            Characters.Add(character); _list.SelectedItem = character; OpenSelected();
        }

        private void Open_Click(object sender, RoutedEventArgs e) => OpenSelected();
        private void OpenSelected()
        {
            if (!(_list.SelectedItem is CharacterProfile character)) return;
            Hide();
            var sheet = new MainWindow(character);
            Application.Current.MainWindow = sheet;
            sheet.Closed += (s,e) =>
            {
                if (Application.Current.MainWindow == sheet && IsVisible == false)
                {
                    Application.Current.MainWindow = this;
                    Show();
                }
            };
            sheet.Show();
        }
    }
}
