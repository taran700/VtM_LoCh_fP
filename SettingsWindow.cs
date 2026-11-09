using System.Windows;
using System.Windows.Controls;

namespace VtM_LoCh_fP
{
    internal sealed class SettingsWindow : Window
    {
        private readonly ComboBox _theme, _points, _healing, _ruleOfSeven;
        private readonly ComboBox _storytellerPrices;
        private readonly ComboBox _neonatePurchaseMode;

        public SettingsWindow()
        {
            Title = "Настройки";
            Width = 460;
            Height = 540;
            MinHeight = 540;
            MaxHeight = 540;
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

            AddSetting(panel, "Увеличение пула от Поколения",
                _points = new ComboBox
                {
                    Items = { "Включено", "Отключено" },
                    SelectedItem = AppSettings.GenerationPointIncrease ? "Включено" : "Отключено",
                    Height = 36
                },
                "Если включено, максимальное число точек атрибутов, способностей и других пулов зависит от поколения персонажа.");

            AddSetting(panel, "Лечение ранений",
                _healing = new ComboBox
                {
                    Items = { "Сир, я сделаю сам", "Работать Гуль!" },
                    SelectedItem = AppSettings.WoundHealingMode,
                    Height = 36
                });

            AddSetting(panel, "Правило Семи в Недостатках",
                _ruleOfSeven = new ComboBox
                {
                    Items = { "Мастер разрешил брать больше!", "Мы чтим книгу!" },
                    SelectedItem = AppSettings.RuleOfSeven ? "Мы чтим книгу!" : "Мастер разрешил брать больше!",
                    Height = 36
                },
                "Правило запрещает брать на себя недостатки сумма стоимости которых более 7");

            panel.Children.Add(new TextBlock
            {
                Text = "Цены Рассказчика (Неонат)",
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["MutedBrush"],
                Margin = new Thickness(0, 4, 0, 5)
            });
            AddSetting(panel, "Режим цен Рассказчика",
                _storytellerPrices = new ComboBox
                {
                    Items = { "Обычные цены", "Цены Рассказчика" },
                    SelectedIndex = AppSettings.StorytellerExperiencePrices ? 1 : 0,
                    Height = 36,
                    ToolTip = "Если в Data/data.json задана таблица base_rules.storyteller_experience_costs, закуп за опыт использует её. Если таблицы нет, применяются базовые цены из experience_costs."
                });
            AddSetting(panel, "Автоматическая закупка (только Свободный Неонат)",
                _neonatePurchaseMode = new ComboBox
                {
                    Items = { "Свободные пункты → опыт", "Только свободные пункты", "Только опыт", "Без автоматической закупки" },
                    SelectedItem = AppSettings.NeonatePurchaseMode,
                    Height = 36
                },
                "При смешанном режиме сначала списываются свободные пункты, остаток стоимости оплачивается опытом. В режиме Птенца опыт обязателен независимо от этой настройки.");
            panel.Children.Add(new TextBlock
            {
                Text = "Эта настройка применяется только в режиме «Свободный Неонат». В смешанном режиме сначала расходуются свободные пункты, затем оставшаяся стоимость оплачивается опытом. В режиме Птенца прокачка всегда требует опыта.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["MutedBrush"],
                Margin = new Thickness(0, 0, 0, 14)
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

        private static void AddSetting(Panel panel, string label, ComboBox combo, string tooltip = null)
        {
            panel.Children.Add(new TextBlock
            {
                Text = label,
                ToolTip = tooltip,
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["MutedBrush"],
                Margin = new Thickness(0, 0, 0, 5)
            });
            combo.Margin = new Thickness(0, 0, 0, 14);
            panel.Children.Add(combo);
        }

        private static CheckBox AddToggle(Panel panel, string label, bool value)
        {
            var check = new CheckBox
            {
                Content = label,
                IsChecked = value,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextBrush"]
            };
            panel.Children.Add(check);
            return check;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.DarkTheme = _theme.SelectedIndex == 1;
            AppSettings.GenerationPointIncrease = (_points.SelectedItem as string) != "Отключено";
            AppSettings.WoundHealingMode = _healing.SelectedItem as string ?? "Работать Гуль!";
            AppSettings.RuleOfSeven = (_ruleOfSeven.SelectedItem as string) == "Мы чтим книгу!";
            AppSettings.StorytellerExperiencePrices = _storytellerPrices.SelectedIndex == 1;
            AppSettings.NeonatePurchaseMode = _neonatePurchaseMode.SelectedItem as string ?? "Свободные пункты → опыт";
            AppSettings.FreebiePurchasesEnabled = AppSettings.NeonatePurchaseMode == "Свободные пункты → опыт" || AppSettings.NeonatePurchaseMode == "Только свободные пункты";
            AppSettings.ExperiencePurchasesEnabled = AppSettings.NeonatePurchaseMode == "Свободные пункты → опыт" || AppSettings.NeonatePurchaseMode == "Только опыт";
            AppSettings.Save();
            DialogResult = true;
        }
    }
}
