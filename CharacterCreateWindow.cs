using System.Windows;
using System.Windows.Controls;

namespace VtM_LoCh_fP
{
    internal sealed class CharacterCreateWindow : Window
    {
        private readonly TextBox _name;
        private readonly ComboBox _mode;
        public string CharacterName => _name.Text.Trim();
        public string CreationMode => _mode.SelectedItem as string;

        public CharacterCreateWindow()
        {
            Title = "Создание персонажа"; Width = 430; Height = 270; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ThemeManager.AttachWindow(this);
            var panel = new StackPanel { Margin = new Thickness(22) };
            panel.Children.Add(new TextBlock { Text = "Имя персонажа", Margin = new Thickness(0,0,0,6) });
            _name = new TextBox { Height = 30, Margin = new Thickness(0,0,0,16) }; panel.Children.Add(_name);
            panel.Children.Add(new TextBlock { Text = "Режим создания", Margin = new Thickness(0,0,0,6) });
            _mode = new ComboBox { Height = 30, Items = { "Свободный Неонат", "Птенец на поводке Сира" }, SelectedIndex = 0, Margin = new Thickness(0,0,0,20) }; panel.Children.Add(_mode);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var ok = new Button { Content = "Создать", Padding = new Thickness(16,7,16,7), IsDefault = true }; ok.Click += (s,e) => { if (string.IsNullOrWhiteSpace(CharacterName)) { MessageBox.Show("Введите имя персонажа."); return; } DialogResult = true; };
            var cancel = new Button { Content = "Отмена", Padding = new Thickness(16,7,16,7), Margin = new Thickness(8,0,0,0), IsCancel = true }; buttons.Children.Add(ok); buttons.Children.Add(cancel); panel.Children.Add(buttons); Content = panel;
        }
    }
}
