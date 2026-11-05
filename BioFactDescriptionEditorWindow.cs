using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VtM_LoCh_fP
{
    internal static class BioFactDescriptionEditorWindow
    {
        public static string EditSingle(Window owner, string title, string current)
        {
            var window = CreateWindow(owner, title, 470, 315);
            var root = new StackPanel { Margin = new Thickness(16) };
            root.Children.Add(new TextBlock { Text = "Собственное описание (не более 500 символов)", Margin = new Thickness(0, 0, 0, 8), FontWeight = FontWeights.SemiBold });
            var editor = new TextBox { Text = current ?? string.Empty, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxLength = 500, MinHeight = 150 };
            var count = new TextBlock { Text = editor.Text.Length + "/500", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 5, 0, 10) };
            editor.TextChanged += (s, e) => count.Text = editor.Text.Length + "/500";
            root.Children.Add(editor); root.Children.Add(count);
            AddButtons(window, root, () => window.DialogResult = true);
            window.Content = root;
            return window.ShowDialog() == true ? editor.Text.Trim() : null;
        }

        public static List<BioFactLevelDescription> EditHolistic(Window owner, IEnumerable<BioFactLevelDescription> current)
        {
            var existing = current == null ? new List<BioFactLevelDescription>() : current.ToList();
            var window = CreateWindow(owner, "Целостное описание факта", 620, 690);
            window.MinHeight = 560;
            var root = new DockPanel { Margin = new Thickness(16) };
            var intro = new TextBlock { Text = "Заполните описания для нужных уровней. Каждый слот относится только к своему уровню.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
            DockPanel.SetDock(intro, Dock.Top); root.Children.Add(intro);
            var editors = new List<TextBox>();
            var stack = new StackPanel();
            for (int level = 1; level <= 5; level++)
            {
                var item = existing.FirstOrDefault(x => x.Level == level);
                stack.Children.Add(new TextBlock { Text = "Уровень " + level, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 7, 0, 4) });
                var editor = new TextBox { Text = item?.Description ?? string.Empty, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 62, MaxLength = 500 };
                editors.Add(editor); stack.Children.Add(editor);
            }
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
            var scroll = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            root.Children.Add(scroll);
            var save = new Button { Content = "Сохранить", MinWidth = 110, Padding = new Thickness(12, 7, 12, 7), IsDefault = true };
            var cancel = new Button { Content = "Отмена", MinWidth = 90, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
            save.Click += (s, e) =>
            {
                int filled = editors.Count(x => !string.IsNullOrWhiteSpace(x.Text));
                if (filled < 5)
                {
                    var answer = ThemedDialog.Show(window, "Заполнены не все пять уровней (" + filled + " из 5). Сохранить только заполненные описания?", "Неполное описание", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (answer != MessageBoxResult.Yes) return;
                }
                window.DialogResult = true;
            };
            buttons.Children.Add(save); buttons.Children.Add(cancel); window.Content = root;
            if (window.ShowDialog() != true) return null;
            var result = new List<BioFactLevelDescription>();
            for (int i = 0; i < editors.Count; i++)
                if (!string.IsNullOrWhiteSpace(editors[i].Text)) result.Add(new BioFactLevelDescription { Level = i + 1, Description = editors[i].Text.Trim() });
            return result;
        }

        private static Window CreateWindow(Window owner, string title, double width, double height)
        {
            var window = new Window { Title = title, Width = width, Height = height, MinWidth = 420, MinHeight = 280, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = owner, ResizeMode = ResizeMode.CanResize, Background = (Brush)Application.Current.FindResource("BgBrush"), Foreground = (Brush)Application.Current.FindResource("TextBrush") };
            ThemeManager.AttachWindow(window);
            return window;
        }

        private static void AddButtons(Window window, Panel root, Action saveAction)
        {
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var save = new Button { Content = "Сохранить", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var cancel = new Button { Content = "Отмена", Padding = new Thickness(12, 6, 12, 6), IsCancel = true };
            save.Click += (s, e) => saveAction(); buttons.Children.Add(save); buttons.Children.Add(cancel); root.Children.Add(buttons);
        }
    }
}
