using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VtM_LoCh_fP
{
    public static class ThemedDialog
    {
        public static MessageBoxResult Show(Window owner, string message, string title,
            MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None)
        {
            var window = new Window
            {
                Title = title,
                Owner = owner,
                Width = 440,
                SizeToContent = SizeToContent.Height,
                MinHeight = 180,
                MaxHeight = 420,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.ToolWindow,
                Background = Brushes.Transparent,
                FontFamily = new FontFamily("Segoe UI")
            };
            window.SetResourceReference(Window.BackgroundProperty, "BgBrush");
            window.SetResourceReference(Window.ForegroundProperty, "TextBrush");

            var root = new Border { Padding = new Thickness(22, 18, 22, 16), CornerRadius = new CornerRadius(7) };
            root.SetResourceReference(Border.BackgroundProperty, "CardBgBrush");
            root.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            root.BorderThickness = new Thickness(1);
            var layout = new DockPanel();
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            DockPanel.SetDock(footer, Dock.Bottom);
            layout.Children.Add(footer);
            var body = new StackPanel { Orientation = Orientation.Horizontal };
            var glyph = image == MessageBoxImage.Warning ? "\uE7BA" : image == MessageBoxImage.Error ? "\uE783" : image == MessageBoxImage.Information ? "\uE946" : "\uE8D4";
            var icon = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 25, Margin = new Thickness(0, 1, 14, 0), VerticalAlignment = VerticalAlignment.Top };
            icon.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            body.Children.Add(icon);
            var text = new TextBlock { Text = message ?? string.Empty, TextWrapping = TextWrapping.Wrap, FontSize = 14, MaxWidth = 340, VerticalAlignment = VerticalAlignment.Center };
            text.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            body.Children.Add(text);
            layout.Children.Add(body);
            root.Child = layout;
            window.Content = root;

            MessageBoxResult result = MessageBoxResult.None;
            Action<string, MessageBoxResult, bool> addButton = (caption, value, primary) =>
            {
                var button = new Button { Content = caption, MinWidth = 88, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(14, 7, 14, 7), IsDefault = primary };
                if (primary)
                {
                    button.Background = new SolidColorBrush(Color.FromRgb(185, 28, 28));
                    button.Foreground = Brushes.White;
                    button.BorderBrush = new SolidColorBrush(Color.FromRgb(185, 28, 28));
                }
                button.Click += (s, e) => { result = value; window.DialogResult = true; };
                footer.Children.Add(button);
            };
            if (buttons == MessageBoxButton.YesNo || buttons == MessageBoxButton.YesNoCancel)
            {
                addButton("Нет", MessageBoxResult.No, false);
                addButton("Да", MessageBoxResult.Yes, true);
                if (buttons == MessageBoxButton.YesNoCancel) addButton("Отмена", MessageBoxResult.Cancel, false);
            }
            else if (buttons == MessageBoxButton.OKCancel)
            {
                addButton("Отмена", MessageBoxResult.Cancel, false);
                addButton("ОК", MessageBoxResult.OK, true);
            }
            else addButton("Понятно", MessageBoxResult.OK, true);

            window.ShowDialog();
            return result;
        }

        public static MessageBoxResult Show(string message, string title, MessageBoxButton buttons, MessageBoxImage image)
        {
            return Show(Application.Current == null ? null : Application.Current.MainWindow, message, title, buttons, image);
        }

        public static MessageBoxResult Show(string message)
        {
            return Show(Application.Current == null ? null : Application.Current.MainWindow, message, "Сообщение", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
