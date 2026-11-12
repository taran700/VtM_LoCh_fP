using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace VtM_LoCh_fP
{
    public static class ThemedDialog
    {
        public static MessageBoxResult Show(Window owner, string message, string title,
            MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None)
        {
            return Show(owner, message, title, buttons, image, null, null);
        }

        public static MessageBoxResult Show(Window owner, string message, string title,
            MessageBoxButton buttons, MessageBoxImage image, string actionText, Action action)
        {
            if (string.IsNullOrWhiteSpace(actionText) && action == null && owner is MainWindow mainWindow)
                mainWindow.TryGetDialogNavigation(message, out actionText, out action);

            var window = new Window
            {
                Title = title,
                Owner = owner,
                Width = 500,
                SizeToContent = SizeToContent.Height,
                MinHeight = 175,
                MaxHeight = 500,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                FontFamily = new FontFamily("Segoe UI"),
                ShowActivated = true
            };
            window.SetResourceReference(Window.ForegroundProperty, "TextBrush");
            MessageBoxResult result = MessageBoxResult.None;
            window.PreviewKeyDown += (sender, args) =>
            {
                if (args.Key != Key.Escape) return;
                result = buttons == MessageBoxButton.YesNo ? MessageBoxResult.No :
                    (buttons == MessageBoxButton.OK ? MessageBoxResult.OK : MessageBoxResult.Cancel);
                window.DialogResult = true;
                args.Handled = true;
            };

            var outer = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), SnapsToDevicePixels = true };
            outer.SetResourceReference(Border.BackgroundProperty, "CardBgBrush");
            outer.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            outer.Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.28 };

            var shell = new DockPanel();
            var titleBar = new Grid { Height = 38, Cursor = Cursors.SizeAll };
            titleBar.SetResourceReference(Grid.BackgroundProperty, "CardHeaderBrush");
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titleText = new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 8, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            titleText.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            titleBar.Children.Add(titleText);
            var close = new Button { Content = "×", Width = 34, Height = 30, Margin = new Thickness(0, 4, 5, 4), Padding = new Thickness(0), FontSize = 18, FontWeight = FontWeights.Normal, Cursor = Cursors.Hand, Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, Foreground = Brushes.Gray };
            close.Click += (s, e) => { window.DialogResult = false; };
            Grid.SetColumn(close, 1);
            titleBar.Children.Add(close);
            titleBar.MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) window.DragMove(); };
            DockPanel.SetDock(titleBar, Dock.Top);
            shell.Children.Add(titleBar);

            var content = new Grid { Margin = new Thickness(22, 18, 22, 16) };
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            string glyph = image == MessageBoxImage.Warning ? "\uE7BA" : image == MessageBoxImage.Error ? "\uE783" : image == MessageBoxImage.Information ? "\uE946" : "\uE8D4";
            var icon = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 27, Margin = new Thickness(2, 1, 16, 0), VerticalAlignment = VerticalAlignment.Top };
            if (image == MessageBoxImage.Error)
                icon.Foreground = new SolidColorBrush(Color.FromRgb(220, 70, 70));
            else if (image == MessageBoxImage.Warning)
                icon.Foreground = new SolidColorBrush(Color.FromRgb(220, 70, 70));
            else
                icon.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            body.Children.Add(icon);
            var text = new TextBlock { Text = message ?? string.Empty, TextWrapping = TextWrapping.Wrap, FontSize = 14, MaxWidth = 390, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) };
            text.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            Grid.SetColumn(text, 1);
            body.Children.Add(text);
            content.Children.Add(body);

            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) };
            Grid.SetRow(footer, 1);
            content.Children.Add(footer);
            shell.Children.Add(content);
            outer.Child = shell;
            window.Content = outer;

            Action<string, MessageBoxResult, bool> addButton = (caption, value, primary) =>
            {
                var button = new Button { Content = caption, MinWidth = 92, MinHeight = 36, Margin = new Thickness(9, 0, 0, 0), Padding = new Thickness(14, 6, 14, 6), IsDefault = primary, Cursor = Cursors.Hand };
                if (primary)
                {
                    Color primaryColor = image == MessageBoxImage.Error || image == MessageBoxImage.Warning
                        ? Color.FromRgb(185, 28, 28)
                        : Color.FromRgb(92, 72, 125);
                    button.Background = new SolidColorBrush(primaryColor);
                    button.Foreground = Brushes.White;
                    button.BorderBrush = new SolidColorBrush(primaryColor);
                }
                else
                {
                    button.SetResourceReference(Control.BackgroundProperty, "InputBrush");
                    button.SetResourceReference(Control.ForegroundProperty, "TextBrush");
                    button.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
                }
                button.Click += (s, e) => { result = value; window.DialogResult = true; };
                footer.Children.Add(button);
            };
            if (!string.IsNullOrWhiteSpace(actionText) && action != null)
            {
                var actionButton = new Button { Content = actionText, MinWidth = 112, MinHeight = 36, Margin = new Thickness(0, 0, 9, 0), Padding = new Thickness(12, 6, 12, 6), Cursor = Cursors.Hand };
                actionButton.SetResourceReference(Control.BackgroundProperty, "InputBrush");
                actionButton.SetResourceReference(Control.ForegroundProperty, "TextBrush");
                actionButton.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
                actionButton.Click += (s, e) => { window.DialogResult = true; action(); };
                footer.Children.Add(actionButton);
            }
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
