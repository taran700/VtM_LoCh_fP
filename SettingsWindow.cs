using System.Windows;
using System.Windows.Controls;

namespace VtM_LoCh_fP
{
    internal sealed class SettingsWindow : Window
    {
        private readonly ComboBox _theme, _points, _healing;

        public SettingsWindow()
        {
            Title = "Настройки";
            Width = 460;
            Height = 430;
            MinHeight = 430;
            MaxHeight = 430;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ThemeManager.AttachWindow(this);

            var root = new DockPanel { Margin = new Thickness(22) };
            var footer = new Border { Padding = new Thickness(0, 14, 0, 0), BorderThickness = new Thickness(0, 1, 0, 0) };
            footer.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            var save = new Button
            {
                Content = "Сохранить",
                MinWidth = 120,
                MinHeight = 38,
                Padding = new Thickness(18, 8, 18, 8),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            save.Click += Save_Click;
            footer.Child = save;
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            var panel = new StackPanel();
            scroll.Content = panel;
            root.Children.Add(scroll);

            AddSetting(panel, "Тема персонажа",
                _theme = new ComboBox
                {
                    Items = { "Человек", "Сородич" },
                    SelectedIndex = AppSettings.DarkTheme ? 1 : 0,
                    Height = 36
                });

            AddSetting(panel, "Точки",
                _points = new ComboBox
                {
                    Items = { "Базовые", "Усиленные" },
                    SelectedItem = AppSettings.PointMode,
                    Height = 36
                });

            AddSetting(panel, "Лечение ранений",
                _healing = new ComboBox
                {
                    Items = { "Сир, я сделаю сам", "Работать Гуль!" },
                    SelectedItem = AppSettings.WoundHealingMode,
                    Height = 36
                });

            panel.Children.Add(new TextBlock
            {
                Text = "«Сир, я сделаю сам» — отметка раны снимается без автоматической траты крови. «Работать Гуль!» — лечение списывает кровь по правилам V20.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["MutedBrush"],
                Margin = new Thickness(0, 8, 0, 18),
                Opacity = 1
            });

            Content = root;
        }

        private static void AddSetting(Panel panel, string label, ComboBox combo)
        {
            panel.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["MutedBrush"],
                Margin = new Thickness(0, 0, 0, 5)
            });
            combo.Margin = new Thickness(0, 0, 0, 14);
            panel.Children.Add(combo);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.DarkTheme = _theme.SelectedIndex == 1;
            AppSettings.PointMode = _points.SelectedItem as string ?? "Базовые";
            AppSettings.WoundHealingMode = _healing.SelectedItem as string ?? "Работать Гуль!";
            AppSettings.Save();
            DialogResult = true;
        }
    }
}
