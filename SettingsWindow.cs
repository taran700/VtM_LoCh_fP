using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace VtM_LoCh_fP
{
    internal sealed class SettingsWindow : Window
    {
        private readonly ComboBox _theme, _points, _healing, _ruleOfSeven;
        private readonly ComboBox _neonatePurchaseMode, _humanityPathLimit;
        private readonly bool _isFledgling;
        private readonly CharacterProfile _character;

        public SettingsWindow(CharacterProfile character)
        {
            _character = character;
            _isFledgling = CharacterCreationRules.IsFledgling(character);
            Title = "Вампстройки";
            Icon = new BitmapImage(new System.Uri("pack://application:,,,/Resources/Vampiric.ico", System.UriKind.Absolute));
            Width = 460;
            // Обычный режим показывает все настройки без прокрутки; в режиме Птенца скрытые параметры не занимают место, поэтому окно компактнее
            Height = _isFledgling ? 420 : 620;
            MinHeight = _isFledgling ? 420 : 620;
            MaxHeight = _isFledgling ? 420 : 620;
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
                },"«Сир, я сделаю сам» — отметка раны снимается без автоматической траты крови. «Работать Гуль!» — лечение списывает кровь по правилам V20.");



            AddSetting(panel, "Правило Семи в Недостатках",
                _ruleOfSeven = new ComboBox
                {
                    Items = { "Мастер разрешил брать больше!", "Мы чтим книгу!" },
                    SelectedItem = AppSettings.RuleOfSeven ? "Мы чтим книгу!" : "Мастер разрешил брать больше!",
                    Height = 36
                },
                "Правило запрещает брать на себя недостатки сумма стоимости которых более 7");

           
            
            var purchaseSettings = new StackPanel();
            AddSetting(purchaseSettings, "Автоматическая закупка",
                _neonatePurchaseMode = new ComboBox
                {
                    Items = { "Свободные пункты затем опыт", "Только свободные пункты", "Только опыт", "Без автоматической закупки" },
                    SelectedItem = AppSettings.NeonatePurchaseMode,
                    Height = 36
                },
                "При смешанном режиме сначала списываются свободные пункты, затем опыт.");
            purchaseSettings.Children.Add(new TextBlock
            {
                Text = "В режиме Птенца автоматическая закупка всегда выполняется сначала за свободные пункты, затем за опыт.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["MutedBrush"],
                Margin = new Thickness(0, 0, 0, 14)
            });
            panel.Children.Add(purchaseSettings);

            var pathLimitSettings = new StackPanel();
            AddSetting(pathLimitSettings, "Ограничение рейтинга Пути",
                _humanityPathLimit = new ComboBox
                {
                    Items = { "Ограничение есть", "Без границ" },
                    SelectedItem = character == null || character.HumanityPathLimitEnabled ? "Ограничение есть" : "Без границ",
                    Height = 36
                },
                "Для любого Пути, кроме Человечности, свободными пунктами выше 5 покупать нельзя. За опыт повышение выше 5 разрешено.");
            panel.Children.Add(pathLimitSettings);

            if (_isFledgling)
            {
                // Для Птенца обе настройки скрыты: правила принудительно включены
                purchaseSettings.Visibility = Visibility.Collapsed;
                pathLimitSettings.Visibility = Visibility.Collapsed;
                _neonatePurchaseMode.SelectedItem = "Свободные пункты затем опыт";
                _humanityPathLimit.SelectedItem = "Ограничение есть";
            }

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


        private void Save_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.DarkTheme = _theme.SelectedIndex == 1;
            AppSettings.GenerationPointIncrease = (_points.SelectedItem as string) != "Отключено";
            AppSettings.WoundHealingMode = _healing.SelectedItem as string ?? "Работать Гуль!";
            AppSettings.RuleOfSeven = (_ruleOfSeven.SelectedItem as string) == "Мы чтим книгу!";
            if (!_isFledgling)
            {
                AppSettings.NeonatePurchaseMode = _neonatePurchaseMode.SelectedItem as string ?? "Свободные пункты затем опыт";
                AppSettings.FreebiePurchasesEnabled = AppSettings.NeonatePurchaseMode == "Свободные пункты затем опыт" || AppSettings.NeonatePurchaseMode == "Только свободные пункты";
                AppSettings.ExperiencePurchasesEnabled = AppSettings.NeonatePurchaseMode == "Свободные пункты затем опыт" || AppSettings.NeonatePurchaseMode == "Только опыт";
            }
            if (!_isFledgling && _character != null)
                _character.HumanityPathLimitEnabled = (_humanityPathLimit.SelectedItem as string) != "Без границ";
            AppSettings.Save();
            if (_character != null) CharacterSelectionWindow.SaveCharacters();
            DialogResult = true;
        }
    }
}
