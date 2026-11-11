using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VtM_LoCh_fP
{
    internal sealed class HumanityPathEditorResult
    {
        public bool Deleted { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string BearingName { get; set; }
        public string BearingEffect { get; set; }
        public string Edicts { get; set; }
        public string FirstVirtue { get; set; }
        public string SecondVirtue { get; set; }
        public List<HumanityPathSinEntry> Hierarchy { get; set; }
    }

    internal static class HumanityPathEditorWindow
    {
        public static HumanityPathEditorResult Edit(Window owner, HumanityPathEditorResult current, bool allowDelete)
        {
            var window = CreateWindow(owner, current == null ? "Новый Путь" : "Редактирование Пути", 720, 780);
            var root = new DockPanel { Margin = new Thickness(18) };
            var footer = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(buttons, 1); footer.Children.Add(buttons);
            var content = new StackPanel();
            var scroll = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            root.Children.Add(scroll);

            var name = new TextBox
            {
                Text = current == null ? string.Empty : current.Name,
                FontSize = 21,
                FontWeight = FontWeights.SemiBold,
                MinHeight = 42,
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 0, 12),
                TextWrapping = TextWrapping.NoWrap,
                AcceptsReturn = false,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                TextAlignment = TextAlignment.Left
            };
            name.SetResourceReference(Control.BackgroundProperty, "InputBrush");
            name.SetResourceReference(Control.ForegroundProperty, "TextBrush");
            name.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
            name.IsReadOnly = current != null;
            if (current != null) name.ToolTip = "Название зафиксировано после создания Пути.";
            content.Children.Add(name);
            TextBox description = AddField(content, "Описание", current == null ? string.Empty : current.Description, true, 85);
            TextBox bearingName = AddField(content, "Столп — название", current == null ? string.Empty : current.BearingName, false, 34);
            TextBox bearingEffect = AddField(content, "Столп — правила и эффект", current == null ? string.Empty : current.BearingEffect, true, 65);
            TextBox edicts = AddField(content, "Эдикты", current == null ? string.Empty : current.Edicts, true, 100);
            content.Children.Add(new TextBlock { Text = "Добродетели Пути", FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 5) });
            var virtueGrid = new Grid(); virtueGrid.ColumnDefinitions.Add(new ColumnDefinition()); virtueGrid.ColumnDefinitions.Add(new ColumnDefinition());
            var firstVirtue = new ComboBox { FontSize = 15, MinHeight = 36, Margin = new Thickness(0,0,6,0), ItemsSource = new[] { "Совесть", "Решимость" }, SelectedItem = current == null ? "Совесть" : (current.FirstVirtue ?? "Совесть") };
            var secondVirtue = new ComboBox { FontSize = 15, MinHeight = 36, Margin = new Thickness(6,0,0,0), ItemsSource = new[] { "Самоконтроль", "Инстинкты" }, SelectedItem = current == null ? "Самоконтроль" : (current.SecondVirtue ?? "Самоконтроль") };
            virtueGrid.Children.Add(firstVirtue); Grid.SetColumn(secondVirtue,1); virtueGrid.Children.Add(secondVirtue); content.Children.Add(virtueGrid);

            var tableTitle = new TextBlock { Text = "Таблица грехов", FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 5) };
            content.Children.Add(tableTitle);
            List<HumanityPathSinEntry> hierarchy = current == null || current.Hierarchy == null ? new List<HumanityPathSinEntry>() : Clone(current.Hierarchy);
            var tableStatus = new TextBlock
            {
                Text = hierarchy.Count > 0 ? "Заполнено строк: " + hierarchy.Count + " из 10" : "Не добавлено",
                Foreground = (Brush)Application.Current.FindResource("MutedBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 5, 0, 8)
            };
            content.Children.Add(tableStatus);
            var tableButton = MakeButton(hierarchy.Count > 0 ? "Редактировать таблицу грехов" : "＋ Добавить информацию", hierarchy.Count == 0);
            tableButton.HorizontalAlignment = HorizontalAlignment.Center;
            tableButton.MinWidth = 190;
            content.Children.Add(tableButton);
            tableButton.Click += (s, e) =>
            {
                var result = HumanitySinTableWindow.Edit(window, hierarchy);
                if (result != null)
                {
                    hierarchy = result;
                    tableStatus.Text = hierarchy.Count > 0 ? "Заполнено строк: " + hierarchy.Count + " из 10" : "Не добавлено";
                    tableButton.Content = hierarchy.Count > 0 ? "Редактировать таблицу грехов" : "＋ Добавить информацию";
                }
            };

            var cancel = MakeButton("Пропустить", false);
            cancel.ToolTip = "Сохранить введённые данные без обязательного заполнения остальных разделов";
            cancel.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(name.Text))
                {
                    ThemedDialog.Show(window, "Сначала укажи название Пути.", "Название Пути", MessageBoxButton.OK, MessageBoxImage.Warning);
                    name.Focus(); return;
                }
                name.IsReadOnly = true;
                window.DialogResult = true;
            };
            buttons.Children.Add(cancel);
            if (allowDelete)
            {
                var delete = MakeButton("Удалить Путь", false);
                delete.Foreground = new SolidColorBrush(Color.FromRgb(190, 35, 35));
                delete.Margin = new Thickness(10, 0, 0, 0);
                delete.Click += (s, e) =>
                {
                    var answer = ThemedDialog.Show(window, "Удалить пользовательский Путь «" + (current == null ? name.Text : current.Name) + "»? Его описание, Столп, Эдикты и таблица грехов будут удалены.", "Удаление Пути", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (answer == MessageBoxResult.Yes) { window.Tag = "DELETE"; window.DialogResult = true; }
                };
                buttons.Children.Add(delete);
            }
            var save = MakeButton("Подтвердить", true);
            save.Margin = new Thickness(10, 0, 0, 0);
            save.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(name.Text))
                {
                    ThemedDialog.Show(window, "Укажи название Пути.", "Не заполнено название", MessageBoxButton.OK, MessageBoxImage.Warning);
                    name.Focus(); return;
                }
                window.DialogResult = true;
            };
            buttons.Children.Add(save);
            window.Content = root;
            if (window.ShowDialog() != true) return null;
            if (string.Equals(Convert.ToString(window.Tag), "DELETE", StringComparison.Ordinal)) return new HumanityPathEditorResult { Deleted = true };
            return new HumanityPathEditorResult
            {
                Name = name.Text.Trim(), Description = description.Text.Trim(), BearingName = bearingName.Text.Trim(),
                BearingEffect = bearingEffect.Text.Trim(), Edicts = edicts.Text.Trim(), FirstVirtue = Convert.ToString(firstVirtue.SelectedItem), SecondVirtue = Convert.ToString(secondVirtue.SelectedItem), Hierarchy = hierarchy
            };
        }

        private static TextBox AddField(Panel panel, string title, string value, bool multiline, double height)
        {
            panel.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 9, 0, 5) });
            var box = new TextBox { Text = value ?? string.Empty, FontSize = 15, MaxLength = 3000, MinHeight = height, Padding = new Thickness(8, 6, 8, 6), TextWrapping = TextWrapping.Wrap, AcceptsReturn = multiline, VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled, HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Top, TextAlignment = TextAlignment.Left };
            box.SetResourceReference(Control.BackgroundProperty, "InputBrush");
            box.SetResourceReference(Control.ForegroundProperty, "TextBrush");
            box.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
            panel.Children.Add(box);
            return box;
        }

        internal static Window CreateWindow(Window owner, string title, double width, double height)
        {
            var window = new Window { Title = title, Width = width, Height = height, MinWidth = 560, MinHeight = 540, WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner, Owner = owner, ResizeMode = ResizeMode.CanResize, ShowInTaskbar = false };
            ThemeManager.AttachWindow(window);
            return window;
        }

        internal static Button MakeButton(string text, bool primary)
        {
            var button = new Button { Content = text, MinWidth = 105, MinHeight = 34, Padding = new Thickness(12, 6, 12, 6), Cursor = System.Windows.Input.Cursors.Hand };
            if (primary) { button.Background = new SolidColorBrush(Color.FromRgb(185, 28, 28)); button.SetResourceReference(Control.ForegroundProperty, "OnAccentBrush"); button.BorderBrush = new SolidColorBrush(Color.FromRgb(185, 28, 28)); }
            else { button.SetResourceReference(Control.BackgroundProperty, "InputBrush"); button.SetResourceReference(Control.ForegroundProperty, "TextBrush"); button.SetResourceReference(Control.BorderBrushProperty, "BorderBrush"); }
            return button;
        }

        internal static List<HumanityPathSinEntry> Clone(IEnumerable<HumanityPathSinEntry> source)
        {
            return source == null ? new List<HumanityPathSinEntry>() : source.Select(x => new HumanityPathSinEntry { Level = x.Level, Sin = x.Sin, Rationale = x.Rationale }).ToList();
        }
    }

    internal static class HumanityPathInfoWindow
    {
        public static void Show(Window owner, string title, string information)
        {
            var window = HumanityPathEditorWindow.CreateWindow(owner, "Информация о Пути", 560, 520);
            window.MinWidth = 420;
            window.MinHeight = 320;
            var root = new DockPanel { Margin = new Thickness(18) };
            var close = HumanityPathEditorWindow.MakeButton("Закрыть", true);
            close.FontSize = 14;
            close.Width = 110;
            close.HorizontalAlignment = HorizontalAlignment.Right;
            close.Margin = new Thickness(0, 12, 0, 0);
            close.Click += (s, e) => window.Close();
            DockPanel.SetDock(close, Dock.Bottom);
            root.Children.Add(close);
            var heading = new TextBlock { Text = title ?? "Путь", FontSize = 21, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
            DockPanel.SetDock(heading, Dock.Top);
            root.Children.Add(heading);
            var body = new TextBlock { Text = information ?? "Информация не добавлена.", FontSize = 15, LineHeight = 22, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            window.Content = root;
            window.ShowDialog();
        }
    }

    internal static class HumanitySinTableWindow
    {
        public static void Show(Window owner, IEnumerable<HumanityPathSinEntry> source, string pathName)
        {
            var rows = HumanityPathEditorWindow.Clone(source).OrderByDescending(x => x.Level).ToList();
            var window = HumanityPathEditorWindow.CreateWindow(owner, "Таблица грехов — " + pathName, 900, 700);
            window.MinWidth = 680;
            var root = new DockPanel { Margin = new Thickness(16) };
            var close = HumanityPathEditorWindow.MakeButton("Закрыть", true);
            close.FontSize = 14;
            close.Width = 110;
            close.HorizontalAlignment = HorizontalAlignment.Right;
            close.Margin = new Thickness(0, 10, 0, 0);
            close.Click += (s, e) => window.Close();
            DockPanel.SetDock(close, Dock.Bottom);
            root.Children.Add(close);

            var table = new Grid();
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int i = 0; i < 10; i++) table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddReadCell(table, "Уровень", 0, 0, true, false);
            AddReadCell(table, "Грех", 0, 1, true, false);
            for (int level = 10; level >= 1; level--)
            {
                int rowIndex = 11 - level;
                var item = rows.FirstOrDefault(x => x.Level == level);
                bool alternate = rowIndex % 2 == 0;
                AddReadCell(table, level.ToString(), rowIndex, 0, false, alternate, true);
                AddReadCell(table, item == null ? string.Empty : item.Sin, rowIndex, 1, false, alternate);
            }
            root.Children.Add(new ScrollViewer { Content = table, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            window.Content = root;
            window.ShowDialog();
        }

        private static void AddReadCell(Grid table, string value, int row, int column, bool header, bool alternate, bool centered = false)
        {
            var text = new TextBlock
            {
                Text = value ?? string.Empty,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(7, 6, 7, 6),
                FontSize = header ? 15 : 14,
                FontWeight = header || column == 0 ? FontWeights.SemiBold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = centered ? HorizontalAlignment.Center : HorizontalAlignment.Stretch,
                TextAlignment = centered ? TextAlignment.Center : TextAlignment.Left
            };
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(105, 128, 128, 128)),
                BorderThickness = new Thickness(0.6),
                Background = header ? new SolidColorBrush(Color.FromArgb(48, 128, 128, 128)) : (alternate ? new SolidColorBrush(Color.FromArgb(34, 128, 128, 128)) : Brushes.Transparent),
                Child = text
            };
            Grid.SetRow(border, row); Grid.SetColumn(border, column); table.Children.Add(border);
        }

        public static List<HumanityPathSinEntry> Edit(Window owner, IEnumerable<HumanityPathSinEntry> current)
        {
            var window = HumanityPathEditorWindow.CreateWindow(owner, "Таблица грехов", 850, 760);
            window.MinWidth = 700;
            var root = new DockPanel { Margin = new Thickness(16) };
            var top = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            top.Children.Add(new TextBlock { Text = "Взять за основу", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
            var templates = HumanityPathService.GetAll();
            var combo = new ComboBox { ItemsSource = templates, DisplayMemberPath = "Name", MinHeight = 32, Padding = new Thickness(6, 3, 6, 3) };
            top.Children.Add(combo);
            DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
            var rows = new List<TextBox>();
            var table = new StackPanel();
            var header = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.Children.Add(new TextBlock { Text = "Уровень", FontWeight = FontWeights.SemiBold });
            var h1 = new TextBlock { Text = "Грех", FontWeight = FontWeights.SemiBold }; Grid.SetColumn(h1, 1); header.Children.Add(h1);
            table.Children.Add(header);
            for (int level = 10; level >= 1; level--)
            {
                var grid = new Grid { Margin = new Thickness(0), Background = level % 2 == 0 ? new SolidColorBrush(Color.FromArgb(42, 128, 128, 128)) : new SolidColorBrush(Color.FromArgb(12, 128, 128, 128)) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var label = new TextBlock { Text = level.ToString(), FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(3, 8, 0, 0) };
                grid.Children.Add(label);
                var sin = MakeTextBox(58); sin.FontSize = 14; sin.HorizontalContentAlignment = HorizontalAlignment.Left; sin.TextAlignment = TextAlignment.Left; sin.Margin = new Thickness(4,3,4,3); Grid.SetColumn(sin, 1); grid.Children.Add(sin);
                rows.Add(sin);
                var rowBorder = new Border { BorderBrush = new SolidColorBrush(Color.FromArgb(115, 128, 128, 128)), BorderThickness = new Thickness(0.7), Child = grid };
                table.Children.Add(rowBorder);
            }
            var existing = HumanityPathEditorWindow.Clone(current);
            foreach (var row in existing)
            {
                int index = 10 - row.Level;
                if (index >= 0 && index < rows.Count) rows[index].Text = row.Sin ?? string.Empty;
            }
            combo.SelectionChanged += (s, e) =>
            {
                var selected = combo.SelectedItem as HumanityPathDefinition;
                if (selected == null) return;
                for (int i = 0; i < rows.Count; i++) rows[i].Text = string.Empty;
                foreach (var row in selected.Hierarchy)
                {
                    int index = 10 - row.Level;
                    if (index >= 0 && index < rows.Count) rows[index].Text = row.Sin ?? string.Empty;
                }
            };
            var scroll = new ScrollViewer { Content = table, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            root.Children.Add(scroll);
            var skip = HumanityPathEditorWindow.MakeButton("Пропустить", false);
            skip.Click += (s, e) =>
            {
                var partial = new List<HumanityPathSinEntry>();
                for (int i = 0; i < rows.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(rows[i].Text))
                        partial.Add(new HumanityPathSinEntry { Level = 10 - i, Sin = rows[i].Text.Trim() });
                }
                window.Tag = partial;
                window.DialogResult = true;
            };
            footer.Children.Add(skip);
            var save = HumanityPathEditorWindow.MakeButton("Подтвердить", true); save.Margin = new Thickness(10, 0, 0, 0);
            save.Click += (s, e) =>
            {
                var result = new List<HumanityPathSinEntry>();
                for (int i = 0; i < rows.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(rows[i].Text))
                        result.Add(new HumanityPathSinEntry { Level = 10 - i, Sin = rows[i].Text.Trim() });
                }
                window.Tag = result; window.DialogResult = true;
            };
            footer.Children.Add(save);
            window.Content = root;
            return window.ShowDialog() == true ? window.Tag as List<HumanityPathSinEntry> : null;
        }

        private static TextBox MakeTextBox(double height)
        {
            var box = new TextBox { FontSize = 14, MinHeight = height, Padding = new Thickness(7, 5, 7, 5), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxLength = 3000, HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Top, TextAlignment = TextAlignment.Left };
            box.SetResourceReference(Control.BackgroundProperty, "InputBrush"); box.SetResourceReference(Control.ForegroundProperty, "TextBrush"); box.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
            return box;
        }
    }
}
