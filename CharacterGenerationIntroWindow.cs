using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VtM_LoCh_fP
{
    internal sealed class CharacterGenerationIntroWindow : Window
    {
        public CharacterGenerationIntroWindow()
        {
            Title = "Случайная генерация персонажа";
            Width = 590;
            Height = 430;
            MinWidth = 540;
            MinHeight = 390;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            ThemeManager.AttachWindow(this);

            var root = new DockPanel { Margin = new Thickness(24) };
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            var cancel = HumanityPathEditorWindow.MakeButton("Отмена", false);
            cancel.Click += (s, e) => { DialogResult = false; };
            var generate = HumanityPathEditorWindow.MakeButton("Сгенерировать", true);
            generate.MinWidth = 150;
            generate.Margin = new Thickness(10, 0, 0, 0);
            generate.Click += (s, e) => { DialogResult = true; };
            footer.Children.Add(cancel);
            footer.Children.Add(generate);
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);

            var content = new StackPanel();
            var title = new TextBlock { Text = "Создание случайного персонажа", FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 14), TextWrapping = TextWrapping.Wrap };
            title.SetResourceReference(TextBlock.ForegroundProperty, "HeadingBrush");
            content.Children.Add(title);
            var description = new TextBlock
            {
                Text = "Генератор самостоятельно заполнит лист персонажа, используя игровые данные V20 и заранее подготовленные профили предпочтений. Результат будет создан в режиме «Свободный Неонат». Вся генерация строится по принципу: как назовёшь корабль, так он и поплывёт…",
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 23,
                Margin = new Thickness(0, 0, 0, 14)
            };
            description.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            content.Children.Add(description);
            var details = new TextBlock
            {
                Text = "Будут случайно определены Имя, Клан, Натура, Маска, Амплуа, Сир, Характеристики, Способности, Факты Биографии, Дисциплины и Биографические данные. Распределение характеристик и способностей использует стандартные стартовые пулы V20 и закуп за 15 свободных пунктов.",
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 21
            };
            details.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            content.Children.Add(details);
            root.Children.Add(content);
            Content = root;
        }
    }
}
