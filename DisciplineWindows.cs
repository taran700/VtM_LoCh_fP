
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VtM_LoCh_fP
{
    internal static class DisciplineWindows
    {
        public static DisciplineDefinition Choose(
            Window owner,
            IEnumerable<DisciplineDefinition> source,
            IEnumerable<string> clanIds,
            bool allowCustom,
            Action<DisciplineDefinition> onCustom)
        {
            DisciplineDefinition chosen = null;

            var window = HumanityPathEditorWindow.CreateWindow(
                owner, "Каталог дисциплин", 780, 740);

            var root = new DockPanel { Margin = new Thickness(16) };

            var bottom = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };

            var close = HumanityPathEditorWindow.MakeButton("Закрыть", false);
            close.Click += (s, e) => window.Close();
            bottom.Children.Add(close);

            if (allowCustom)
            {
                var add = HumanityPathEditorWindow.MakeButton(
                    "Добавить свою дисциплину", true);

                add.Margin = new Thickness(8, 0, 0, 0);

                add.Click += (s, e) =>
                {
                    var d = EditCustom(owner, null);

                    if (d != null)
                    {
                        onCustom?.Invoke(d);
                        chosen = d;
                        window.DialogResult = true;
                    }
                };

                bottom.Children.Add(add);
            }

            DockPanel.SetDock(bottom, Dock.Bottom);
            root.Children.Add(bottom);

            var list = new StackPanel();
            var clan = new HashSet<string>(
                clanIds ?? Enumerable.Empty<string>());

            foreach (var d in source.OrderBy(x => x.Name))
            {
                bool isClan = clan.Contains(d.Id);

                var button = new Button
                {
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Padding = new Thickness(12, 9, 12, 9),
                    Margin = new Thickness(0, 0, 0, 6),
                    BorderThickness = new Thickness(1),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                button.SetResourceReference(
                    Control.BackgroundProperty,
                    isClan ? "SelectionBrush" : "InputBrush");

                button.SetResourceReference(
                    Control.ForegroundProperty,
                    "TextBrush");

                button.SetResourceReference(
                    Control.BorderBrushProperty,
                    "BorderBrush");

                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                if (isClan)
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var title = new TextBlock
                {
                    Text = d.Name,
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                };

                Grid.SetRow(title, 0);
                grid.Children.Add(title);

                if (isClan)
                {
                    var label = new TextBlock
                    {
                        Text = "Клановая дисциплина",
                        FontSize = 12,
                        Foreground = BrushResource("AccentBrush"),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0, 3, 0, 0)
                    };

                    Grid.SetRow(label, 1);
                    grid.Children.Add(label);
                }

                button.Content = grid;

                button.Click += (s, e) =>
                {
                    DisciplineDefinition result;

                    if (ShowDetails(owner, d, isClan, out result))
                    {
                        chosen = result;
                        window.DialogResult = true;
                    }
                };

                list.Children.Add(button);
            }

            root.Children.Add(new ScrollViewer
            {
                Content = list,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            });

            window.Content = root;
            window.ShowDialog();

            return chosen;
        }

        private static bool ShowDetails(
            Window owner,
            DisciplineDefinition original,
            bool clan,
            out DisciplineDefinition chosen)
        {
            chosen = null;

            var w = HumanityPathEditorWindow.CreateWindow(
                owner, original.Name, 760, 800);

            var root = new DockPanel { Margin = new Thickness(18) };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };

            var close = HumanityPathEditorWindow.MakeButton("Закрыть", false);
            close.Click += (s, e) => w.DialogResult = false;
            buttons.Children.Add(close);

            var pathPicker = (ComboBox)null;

            var take = HumanityPathEditorWindow.MakeButton(
                "Взять дисциплину", true);

            take.Margin = new Thickness(8, 0, 0, 0);

            take.Click += (s, e) =>
            {
                if (original.Paths.Count > 0 &&
                    pathPicker.SelectedItem == null)
                {
                    return;
                }

                w.DialogResult = true;
            };

            buttons.Children.Add(take);

            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);

            var content = new StackPanel();

            content.Children.Add(new TextBlock
            {
                Text = original.Name,
                FontSize = 25,
                FontWeight = FontWeights.Bold,
                Foreground = BrushResource("HeadingBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            });

            if (clan)
            {
                content.Children.Add(new TextBlock
                {
                    Text = "КЛАНОВАЯ ДИСЦИПЛИНА",
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = BrushResource("AccentBrush"),
                    Margin = new Thickness(0, 0, 0, 8)
                });
            }

            var description = new TextBlock
            {
                Text = original.Description,
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14),
                LineHeight = 22
            };

            content.Children.Add(description);

            StackPanel powersPanel = new StackPanel();

            if (original.Paths.Count > 0)
            {
                content.Children.Add(new TextBlock
                {
                    Text = "Путь",
                    FontSize = 15,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 5, 0, 5)
                });

                pathPicker = new ComboBox
                {
                    ItemsSource = original.Paths,
                    DisplayMemberPath = "Name",
                    SelectedValuePath = "Id",
                    MinHeight = 36,
                    FontSize = 15
                };

                pathPicker.SetResourceReference(
                    Control.BackgroundProperty, "InputBrush");

                pathPicker.SetResourceReference(
                    Control.ForegroundProperty, "TextBrush");

                pathPicker.SetResourceReference(
                    Control.BorderBrushProperty, "BorderBrush");

                pathPicker.SelectedIndex = 0;
                content.Children.Add(pathPicker);
            }

            content.Children.Add(powersPanel);

            Action renderPowers = () =>
            {
                powersPanel.Children.Clear();

                DisciplineDefinition view =
                    original.Paths.Count > 0
                        ? DisciplineService.ForSelection(
                            original, Convert.ToString(pathPicker.SelectedValue))
                        : original;

                if (view != null &&
                    !string.IsNullOrWhiteSpace(view.PathName))
                {
                    powersPanel.Children.Add(new TextBlock
                    {
                        Text = view.PathName,
                        FontSize = 19,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = BrushResource("HeadingBrush"),
                        Margin = new Thickness(0, 10, 0, 5)
                    });

                    var path = original.Paths.FirstOrDefault(
                        x => x.Id == view.PathId);

                    if (path != null &&
                        !string.IsNullOrWhiteSpace(path.Description))
                    {
                        powersPanel.Children.Add(new TextBlock
                        {
                            Text = path.Description,
                            FontSize = 15,
                            TextWrapping = TextWrapping.Wrap,
                            LineHeight = 21,
                            Margin = new Thickness(0, 0, 0, 12)
                        });
                    }
                }

                foreach (var p in (view?.Powers ?? original.Powers)
                    .OrderBy(x => x.Level))
                {
                    var card = new Border
                    {
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(13),
                        Margin = new Thickness(0, 0, 0, 10),
                        CornerRadius = new CornerRadius(5)
                    };

                    card.SetResourceReference(
                        Border.BorderBrushProperty, "BorderBrush");

                    card.SetResourceReference(
                        Border.BackgroundProperty, "CardBgBrush");

                    var sp = new StackPanel();

                    sp.Children.Add(new TextBlock
                    {
                        Text = "Уровень " + p.Level + " — " + p.Name,
                        FontSize = 18,
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 0, 0, 8),
                        TextWrapping = TextWrapping.Wrap
                    });

                    AddParameter(sp, "ПУЛ", p.DicePool);
                    AddParameter(sp, "СЛОЖНОСТЬ", p.Difficulty);
                    AddParameter(sp, "СТОИМОСТЬ", p.Cost);
                    AddParameter(sp, "ДЛИТЕЛЬНОСТЬ", p.Duration);

                    AddSection(sp, "ОПИСАНИЕ", p.SystemText);
                    AddSection(sp, "ОСОБЕННОСТИ", p.Features);

                    card.Child = sp;
                    powersPanel.Children.Add(card);
                }
            };

            if (pathPicker != null)
                pathPicker.SelectionChanged += (s, e) => renderPowers();

            renderPowers();

            root.Children.Add(new ScrollViewer
            {
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            });

            w.Content = root;

            bool accepted = w.ShowDialog() == true;

            if (accepted)
            {
                chosen = DisciplineService.ForSelection(
                    original,
                    Convert.ToString(pathPicker?.SelectedValue));
            }

            return accepted;
        }

        public static DisciplineDefinition EditCustom(
            Window owner,
            DisciplineDefinition current)
        {
            var w = HumanityPathEditorWindow.CreateWindow(
                owner,
                current == null
                    ? "Своя дисциплина"
                    : "Редактирование дисциплины",
                780,
                820);

            var root = new DockPanel { Margin = new Thickness(16) };

            var formTop = new StackPanel();
            DockPanel.SetDock(formTop, Dock.Top);
            root.Children.Add(formTop);

            var name = Field(
                formTop, "Название дисциплины",
                current?.Name, false, 42);

            var desc = Field(
                formTop, "Описание дисциплины",
                current?.Description, true, 85);

            var countPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 6)
            };

            countPanel.Children.Add(new TextBlock
            {
                Text = "Количество уровней (1–9):",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                FontSize = 15
            });

            var count = new TextBox
            {
                Width = 90,
                Text = (
                    current != null && current.LevelCount > 0
                        ? current.LevelCount
                        : (current?.Powers.Count ?? 5)
                ).ToString(),
                FontSize = 15,
                MinHeight = 32,
                Padding = new Thickness(6)
            };

            count.SetResourceReference(
                Control.BackgroundProperty, "InputBrush");

            count.SetResourceReference(
                Control.ForegroundProperty, "TextBrush");

            count.SetResourceReference(
                Control.BorderBrushProperty, "BorderBrush");

            countPanel.Children.Add(count);
            formTop.Children.Add(countPanel);

            var editors = new List<PowerEditors>();

            var stack = new StackPanel();

            var scroll = new ScrollViewer
            {
                Content = stack,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            Func<int> levelCount = () =>
            {
                double value;

                if (!double.TryParse(
                        count.Text,
                        NumberStyles.Float,
                        CultureInfo.CurrentCulture,
                        out value) &&
                    !double.TryParse(
                        count.Text,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out value))
                {
                    value = 5;
                }

                if (double.IsNaN(value) || double.IsInfinity(value))
                    value = 5;

                int n = (int)Math.Round(
                    value,
                    MidpointRounding.AwayFromZero);

                return Math.Max(1, Math.Min(9, n));
            };

            Action rebuild = () =>
            {
                var saved = new Dictionary<int, DisciplinePower>();

                for (int i = 0; i < editors.Count; i++)
                    saved[i + 1] = editors[i].ToPower(i + 1);

                stack.Children.Clear();
                editors.Clear();

                int n = levelCount();

                for (int i = 1; i <= n; i++)
                {
                    var old = saved.ContainsKey(i)
                        ? saved[i]
                        : current?.Powers.FirstOrDefault(x => x.Level == i);

                    // Вместо GroupBox используем Border.
                    // Это исключает стандартную системную белую рамку WPF.
                    var group = new Border
                    {
                        Margin = new Thickness(0, 5, 0, 8),
                        Padding = new Thickness(8),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4)
                    };

                    group.SetResourceReference(
                        Border.BorderBrushProperty,
                        "AccentBrush");

                    group.SetResourceReference(
                        Border.BackgroundProperty,
                        "CardBgBrush");

                    var groupContent = new StackPanel();

                    var header = new TextBlock
                    {
                        Text = "Уровень " + i,
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 15,
                        Margin = new Thickness(0, 0, 0, 8)
                    };

                    header.SetResourceReference(
                        TextBlock.ForegroundProperty,
                        "TextBrush");

                    groupContent.Children.Add(header);

                    var g = new StackPanel();
                    var pe = new PowerEditors();

                    pe.Name = Field(
                        g, "Название умения", old?.Name, false, 36);

                    pe.Pool = Field(
                        g, "Пул", old?.DicePool, false, 34);

                    pe.Difficulty = Field(
                        g, "Сложность", old?.Difficulty, false, 34);

                    pe.Cost = Field(
                        g, "Стоимость", old?.Cost, false, 34);

                    pe.Duration = Field(
                        g, "Длительность", old?.Duration, false, 34);

                    pe.Description = Field(
                        g, "Полное описание / системный текст",
                        old?.SystemText, true, 90);

                    pe.Features = Field(
                        g, "Особенности / ключевые правила",
                        old?.Features, true, 65);

                    editors.Add(pe);

                    groupContent.Children.Add(g);
                    group.Child = groupContent;

                    stack.Children.Add(group);
                }
            };

            count.TextChanged += (s, e) => rebuild();

            rebuild();

            // Панель кнопок добавляется до ScrollViewer.
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);

            var cancel = HumanityPathEditorWindow.MakeButton(
                "Отмена", false);

            cancel.Click += (s, e) => w.DialogResult = false;

            buttons.Children.Add(cancel);

            var save = HumanityPathEditorWindow.MakeButton(
                "Подтвердить", true);

            save.Margin = new Thickness(8, 0, 0, 0);

            save.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(name.Text))
                {
                    ThemedDialog.Show(
                        w,
                        "Укажи название дисциплины.",
                        "Не заполнено название",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                count.Text = levelCount().ToString();
                w.DialogResult = true;
            };

            buttons.Children.Add(save);

            DockPanel.SetDock(scroll, Dock.Top);
            root.Children.Add(scroll);

            w.Content = root;

            if (w.ShowDialog() != true)
                return null;

            var result = new DisciplineDefinition
            {
                Id = current?.Id ??
                     ("custom_" + Guid.NewGuid().ToString("N")),

                Name = name.Text.Trim(),
                Description = Optional(desc.Text),
                IsCustom = true,
                LevelCount = levelCount()
            };

            for (int i = 0; i < editors.Count; i++)
            {
                var p = editors[i].ToPower(i + 1);

                if (!string.IsNullOrWhiteSpace(p.Name) ||
                    !string.IsNullOrWhiteSpace(p.SystemText) ||
                    !string.IsNullOrWhiteSpace(p.DicePool) ||
                    !string.IsNullOrWhiteSpace(p.Difficulty) ||
                    !string.IsNullOrWhiteSpace(p.Cost) ||
                    !string.IsNullOrWhiteSpace(p.Duration) ||
                    !string.IsNullOrWhiteSpace(p.Features))
                {
                    result.Powers.Add(p);
                }
            }

            return result;
        }

        private class PowerEditors
        {
            public TextBox Name;
            public TextBox Pool;
            public TextBox Difficulty;
            public TextBox Cost;
            public TextBox Duration;
            public TextBox Description;
            public TextBox Features;

            public DisciplinePower ToPower(int level)
            {
                return new DisciplinePower
                {
                    Level = level,
                    Id = "custom_power_" + level,
                    Name = Optional(Name.Text),
                    DicePool = Optional(Pool.Text),
                    Difficulty = Optional(Difficulty.Text),
                    Cost = Optional(Cost.Text),
                    Duration = Optional(Duration.Text),
                    SystemText = Optional(Description.Text),
                    Features = Optional(Features.Text)
                };
            }
        }

        private static string Optional(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        private static void AddParameter(
            Panel p,
            string label,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Trim() == "—" ||
                value.Trim() == "-")
            {
                return;
            }

            var row = new DockPanel
            {
                Margin = new Thickness(0, 2, 0, 2)
            };

            row.Children.Add(new TextBlock
            {
                Text = label,
                Width = 115,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold
            });

            row.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap
            });

            p.Children.Add(row);
        }

        private static void AddSection(
            Panel p,
            string title,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Trim() == "—" ||
                value.Trim() == "-")
            {
                return;
            }

            p.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 10, 0, 4)
            });

            p.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 15,
                LineHeight = 22,
                TextWrapping = TextWrapping.Wrap
            });
        }

        private static TextBox Field(
            Panel p,
            string label,
            string value,
            bool multi,
            double height)
        {
            p.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 7, 0, 3)
            });

            var t = new TextBox
            {
                Text = value ?? "",
                FontSize = 15,
                MinHeight = height,
                AcceptsReturn = multi,
                TextWrapping = TextWrapping.Wrap,
                VerticalContentAlignment = VerticalAlignment.Top,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                TextAlignment = TextAlignment.Left,
                Padding = new Thickness(7),
                VerticalScrollBarVisibility = multi
                    ? ScrollBarVisibility.Auto
                    : ScrollBarVisibility.Disabled
            };

            t.SetResourceReference(
                Control.BackgroundProperty, "InputBrush");

            t.SetResourceReference(
                Control.ForegroundProperty, "TextBrush");

            t.SetResourceReference(
                Control.BorderBrushProperty, "BorderBrush");

            p.Children.Add(t);

            return t;
        }

        private static Brush BrushResource(string key)
        {
            var b = Application.Current.TryFindResource(key) as Brush;
            return b ?? Brushes.Firebrick;
        }
    }
}
