using System.Windows;
using System.Windows.Controls;

namespace VtM_LoCh_fP
{
    internal sealed class SettingsWindow : Window
    {
        private readonly ComboBox _theme, _disciplines, _points;
        public SettingsWindow()
        {
            Title = "Настройки"; Width = 440; Height = 360; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var panel = new StackPanel { Margin = new Thickness(22) };
            AddSetting(panel, "Тема персонажа", _theme = new ComboBox { Items = { "Человек", "Сородич" }, SelectedIndex = AppSettings.DarkTheme ? 1 : 0, Height = 30 });
            AddSetting(panel, "Дисциплины", _disciplines = new ComboBox { Items = { "Готовые", "Самобытные" }, SelectedItem = AppSettings.DisciplineMode, Height = 30 });
            AddSetting(panel, "Точки", _points = new ComboBox { Items = { "Базовые", "Усиленные" }, SelectedItem = AppSettings.PointMode, Height = 30 });
            panel.Children.Add(new TextBlock { Text = "Расчёты и правила пока не реализованы — ЗАГЛУШКА", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,8,0,18), Opacity = 0.75 });
            var save = new Button { Content = "Сохранить", Padding = new Thickness(16,8,16,8), HorizontalAlignment = HorizontalAlignment.Right }; save.Click += Save_Click; panel.Children.Add(save); Content = panel;
        }
        private static void AddSetting(Panel panel, string label, ComboBox combo)
        { panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0,0,0,5) }); combo.Margin = new Thickness(0,0,0,14); panel.Children.Add(combo); }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.DarkTheme = _theme.SelectedIndex == 1;
            AppSettings.DisciplineMode = _disciplines.SelectedItem as string ?? "Готовые";
            AppSettings.PointMode = _points.SelectedItem as string ?? "Базовые";
            AppSettings.Save();
            if (AppSettings.DarkTheme) ThemeManager.ApplyDark(); else ThemeManager.ApplyLight();
            DialogResult = true;
        }
    }
}
