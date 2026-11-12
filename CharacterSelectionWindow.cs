using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Windows;
using System;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using Microsoft.Win32;
using EZ_Updater;
using System.Net.Http;
using System.Net;
using Newtonsoft.Json.Linq;

namespace VtM_LoCh_fP
{
    internal sealed class CharacterSelectionWindow : Window
    {
        internal static readonly ObservableCollection<CharacterProfile> Characters = new ObservableCollection<CharacterProfile>();
        internal static void SaveCharacters() => CharacterStorage.Save(Characters);
        private readonly ListBox _list;
        private Button update;
        private Button checkUpdatesButton;
        private bool updateAvailableCached = false;

        public CharacterSelectionWindow()
        {
            Characters.Clear();
            foreach (var saved in CharacterStorage.Load())
                Characters.Add(saved);
            Title = "VtM V20 — Персонажи"; Width = 1120; Height = 560; MinWidth = 900; MinHeight = 420;
            PreviewKeyDown += CharacterSelectionWindow_PreviewKeyDown;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ThemeManager.AttachWindow(this);
            var root = new Grid { Margin = new Thickness(20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var title = new TextBlock { Text = "Персонажи", FontSize = 24, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,14) };
            title.SetResourceReference(TextBlock.ForegroundProperty, "HeadingBrush");
            root.Children.Add(title);
            _list = new ListBox { ItemsSource = Characters, Margin = new Thickness(0,0,0,14), Padding = new Thickness(6), BorderThickness = new Thickness(0) };
            _list.ItemTemplate = CreateCharacterTemplate();
            _list.ItemContainerStyle = CreateCharacterItemStyle();
            Grid.SetRow(_list, 1); root.Children.Add(_list);
            var footer = new Grid();
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var generate = new Button { Content = "Сгенерировать персонажа", Padding = new Thickness(14, 9, 14, 9), HorizontalAlignment = HorizontalAlignment.Left };
            generate.Click += GenerateCharacter_Click;
            Grid.SetColumn(generate, 0); footer.Children.Add(generate);
            var buttons = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            update = new Button { Content = "Доступно обновление", Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 8, 0), Visibility = Visibility.Collapsed };
            var enableAutoUpdate = new Button { Content = "Включить автообновление", Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 8, 0), Visibility = Visibility.Collapsed };
            checkUpdatesButton = new Button { Content = "Проверить обновления", Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 8, 0) };
            checkUpdatesButton.Click += async (sender, args) =>
            {
                checkUpdatesButton.IsEnabled = false;
                string oldContent = Convert.ToString(checkUpdatesButton.Content);
                checkUpdatesButton.Content = "Проверка…";
                try
                {
                    UpdateCheckResult result = await UpdateService.CheckAsync(true);
                    ApplyUpdateCheckResult(result);
                    ThemedDialog.Show(this, result.Message ?? "Проверка завершена.",
                        result.UpdateAvailable ? "Доступно обновление" : "Проверка обновлений",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                finally
                {
                    checkUpdatesButton.Content = oldContent;
                    checkUpdatesButton.IsEnabled = true;
                }
            };

            update.Click += async (sender, args) =>
            {
                // Кнопка отображается только при подтверждённом наличии новой версии
                if (!updateAvailableCached)
                {
                    update.Visibility = Visibility.Collapsed;
                    return;
                }

                string currentVersion = GetCurrentApplicationVersion();
                string targetVersion = Updater.ReleaseVersion == null ? "неизвестна" : Updater.ReleaseVersion.ToString();
                var choice = ShowUpdateConfirmation(currentVersion, targetVersion);
                if (choice == UpdatePromptChoice.DisableNotifications)
                {
                    AppSettings.UpdateNotificationsEnabled = false;
                    AppSettings.Save();
                    update.Visibility = Visibility.Collapsed;
                    enableAutoUpdate.Visibility = Visibility.Visible;
                }
                else if (choice == UpdatePromptChoice.Skip)
                {
                    update.Visibility = Visibility.Collapsed;
                }
                else if (choice == UpdatePromptChoice.Update)
                {
                    Update_Click(sender, args);
                }
            };

            enableAutoUpdate.Click += async (sender, args) =>
            {
                AppSettings.UpdateNotificationsEnabled = true;
                AppSettings.Save();
                enableAutoUpdate.Visibility = Visibility.Collapsed;
                try
                {
                    UpdateCheckResult result = await UpdateService.CheckAsync(false);
                    ApplyUpdateCheckResult(result);
                }
                catch (Exception ex)
                {
                    updateAvailableCached = UpdateService.RestoreCachedUpdaterState();
                    update.Visibility = updateAvailableCached ? Visibility.Visible : Visibility.Collapsed;
                    ShowUpdateError("Не удалось включить проверку обновлений", ex);
                }
            };

            if (!AppSettings.UpdateNotificationsEnabled)
                enableAutoUpdate.Visibility = Visibility.Visible;

            var import = new Button { Content = "Импорт JSON", Padding = new Thickness(12,8,12,8), Margin = new Thickness(0,0,8,0) };
            import.Click += Import_Click;
            var create = new Button { Content = "Создать персонажа", Padding = new Thickness(12,8,12,8), Margin = new Thickness(0,0,8,0) }; create.Click += Create_Click;
            var open = new Button { Content = "Открыть", Padding = new Thickness(12,8,12,8), IsEnabled = false }; open.Click += Open_Click;
            _list.SelectionChanged += (s, e) => { open.IsEnabled = _list.SelectedItem != null; };
            buttons.Children.Add(update);
            buttons.Children.Add(checkUpdatesButton);
            buttons.Children.Add(enableAutoUpdate);
            buttons.Children.Add(import); buttons.Children.Add(create); buttons.Children.Add(open);
            Grid.SetColumn(buttons, 1); footer.Children.Add(buttons);
            Grid.SetRow(footer, 2); root.Children.Add(footer); Content = root;
            _list.MouseDoubleClick += (s,e) => OpenSelected();
            _list.PreviewKeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter && _list.SelectedItem != null) { OpenSelected(); e.Handled = true; } };
            // Пока окно выбора скрыто, приложение живёт ради открытого листа
            Loaded += async (s, e) =>
            {
                ShowStorageLoadWarnings();
                if (!AppSettings.UpdateNotificationsEnabled) return;
                try
                {
                    UpdateCheckResult result = await UpdateService.CheckAsync(false);
                    ApplyUpdateCheckResult(result);
                    if (!result.WasCached && !result.UpdateAvailable &&
                        !string.IsNullOrWhiteSpace(result.Message) &&
                        result.Message.IndexOf("последняя версия", StringComparison.OrdinalIgnoreCase) < 0)
                        ThemedDialog.Show(this, result.Message, "Проверка обновлений", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch (Exception ex)
                {
                    updateAvailableCached = UpdateService.RestoreCachedUpdaterState();
                    update.Visibility = updateAvailableCached ? Visibility.Visible : Visibility.Collapsed;
                    ShowUpdateError("Не удалось проверить обновления", ex);
                }
            };
            Closed += (s, e) =>
            {
                if (Application.Current != null)
                    Application.Current.Shutdown();
            };
        }


        private enum UpdatePromptChoice
        {
            None,
            DisableNotifications,
            Skip,
            Update
        }

        private static string GetCurrentApplicationVersion()
        {
            string executablePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            string fileVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(executablePath).FileVersion;
            return string.IsNullOrWhiteSpace(fileVersion) ? "неизвестна" : fileVersion;
        }

        private UpdatePromptChoice ShowUpdateConfirmation(string currentVersion, string targetVersion)
        {
            var dialog = new Window
            {
                Title = "Доступно обновление VtM V20",
                Owner = this,
                Width = 760,
                Height = 250,
                MinWidth = 700,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
                Background = new System.Windows.Media.SolidColorBrush(ThemeManager.IsDark
                    ? System.Windows.Media.Color.FromRgb(32, 32, 36)
                    : System.Windows.Media.Color.FromRgb(250, 250, 252)),
                Foreground = new System.Windows.Media.SolidColorBrush(ThemeManager.IsDark
                    ? System.Windows.Media.Color.FromRgb(245, 245, 245)
                    : System.Windows.Media.Color.FromRgb(30, 30, 34))
            };
            ThemeManager.AttachWindow(dialog);

            var root = new DockPanel { Margin = new Thickness(22) };
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            UpdatePromptChoice choice = UpdatePromptChoice.None;
            Button MakeChoiceButton(string caption, UpdatePromptChoice result, bool primary)
            {
                var button = new Button
                {
                    Content = new TextBlock
                    {
                        Text = caption,
                        Foreground = primary ? System.Windows.Media.Brushes.White :
                            new System.Windows.Media.SolidColorBrush(ThemeManager.IsDark
                                ? System.Windows.Media.Color.FromRgb(245, 245, 245)
                                : System.Windows.Media.Color.FromRgb(35, 35, 40)),
                        TextWrapping = TextWrapping.NoWrap,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    Padding = new Thickness(12, 9, 12, 9),
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Background = primary
                        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 32, 40))
                        : new System.Windows.Media.SolidColorBrush(ThemeManager.IsDark
                            ? System.Windows.Media.Color.FromRgb(55, 55, 60)
                            : System.Windows.Media.Color.FromRgb(235, 235, 239)),
                    Foreground = primary
                        ? System.Windows.Media.Brushes.White
                        : new System.Windows.Media.SolidColorBrush(ThemeManager.IsDark
                            ? System.Windows.Media.Color.FromRgb(245, 245, 245)
                            : System.Windows.Media.Color.FromRgb(35, 35, 40)),
                    BorderBrush = primary
                        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 32, 40))
                        : new System.Windows.Media.SolidColorBrush(ThemeManager.IsDark
                            ? System.Windows.Media.Color.FromRgb(90, 90, 96)
                            : System.Windows.Media.Color.FromRgb(190, 190, 196)),
                    BorderThickness = new Thickness(1)
                };
                button.MinWidth = result == UpdatePromptChoice.DisableNotifications ? 230 : 130;
                button.MinHeight = 42;
                button.Margin = new Thickness(5, 0, 5, 0);
                button.Cursor = System.Windows.Input.Cursors.Hand;

                var template = new ControlTemplate(typeof(Button));
                var buttonBorder = new FrameworkElementFactory(typeof(Border), "ButtonBorder");
                buttonBorder.SetBinding(Border.BackgroundProperty,
                    new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
                buttonBorder.SetBinding(Border.BorderBrushProperty,
                    new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
                buttonBorder.SetBinding(Border.BorderThicknessProperty,
                    new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
                buttonBorder.SetBinding(Border.PaddingProperty,
                    new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
                var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
                presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                presenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
                buttonBorder.AppendChild(presenter);
                template.VisualTree = buttonBorder;

                var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                hover.Setters.Add(new Setter(Border.BackgroundProperty,
                    new System.Windows.Media.SolidColorBrush(primary
                        ? System.Windows.Media.Color.FromRgb(158, 43, 52)
                        : (ThemeManager.IsDark ? System.Windows.Media.Color.FromRgb(75, 75, 82) : System.Windows.Media.Color.FromRgb(220, 220, 226))),
                    "ButtonBorder"));
                template.Triggers.Add(hover);
                var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
                pressed.Setters.Add(new Setter(Border.BackgroundProperty,
                    new System.Windows.Media.SolidColorBrush(primary
                        ? System.Windows.Media.Color.FromRgb(105, 24, 31)
                        : (ThemeManager.IsDark ? System.Windows.Media.Color.FromRgb(42, 42, 47) : System.Windows.Media.Color.FromRgb(205, 205, 212))),
                    "ButtonBorder"));
                template.Triggers.Add(pressed);
                button.Template = template;

                button.Click += (s, e) =>
                {
                    choice = result;
                    dialog.DialogResult = true;
                };
                return button;
            }

            actions.Children.Add(MakeChoiceButton("Не уведомлять об обновлениях", UpdatePromptChoice.DisableNotifications, false));
            actions.Children.Add(MakeChoiceButton("Пропустить", UpdatePromptChoice.Skip, false));
            actions.Children.Add(MakeChoiceButton("Обновиться", UpdatePromptChoice.Update, true));

            var description = new TextBlock
            {
                Text = $"Доступна новая версия: {targetVersion}\nТекущая версия: {currentVersion}\nВыберите, что сделать:",
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 8, 2, 22)
            };
            description.Foreground = new System.Windows.Media.SolidColorBrush(ThemeManager.IsDark
                ? System.Windows.Media.Color.FromRgb(245, 245, 245)
                : System.Windows.Media.Color.FromRgb(30, 30, 34));

            DockPanel.SetDock(actions, Dock.Bottom);
            root.Children.Add(actions);
            root.Children.Add(description);
            dialog.Content = root;
            dialog.ShowDialog();
            return choice;
        }

        private void ApplyUpdateCheckResult(UpdateCheckResult result)
        {
            if (result == null) return;
            updateAvailableCached = result.UpdateAvailable;
            update.Content = "Доступно обновление";
            update.Visibility = updateAvailableCached ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowStorageLoadWarnings()
        {
            var issues = CharacterStorage.LoadIssues;
            if (issues == null || issues.Count == 0) return;

            string details = string.Join("\n\n", issues);
            string message = "Некоторые сохранения не удалось прочитать. Приложение продолжит работу, " +
                "а остальные персонажи останутся доступны.\n\n" +
                "Повреждённые файлы не удалены и не перезаписаны. Свяжитесь с разработчиком приложения, " +
                "чтобы запросить восстановление этого сохранения. Передайте разработчику исходный JSON-файл " +
                "из папки «Документы\\VtM_LoCh_fP» и сведения об ошибке.\n\n" +
                "Сведения об ошибках:\n" + details;

            ThemedDialog.Show(this, message, "Ошибка чтения сохранения", MessageBoxButton.OK, MessageBoxImage.Warning);
        }


        private DataTemplate CreateCharacterTemplate()
        {
            var panel = new FrameworkElementFactory(typeof(DockPanel));
            panel.SetValue(DockPanel.LastChildFillProperty, true);

            var actions = new FrameworkElementFactory(typeof(StackPanel));
            actions.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            actions.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            var migrate = new FrameworkElementFactory(typeof(Button));
            migrate.SetValue(ContentControl.ContentProperty, "Мигрировать в Свободного Неоната");
            migrate.SetValue(Control.PaddingProperty, new Thickness(9, 5, 9, 5));
            migrate.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 4, 0));
            migrate.SetBinding(FrameworkElement.TagProperty, new System.Windows.Data.Binding());
            migrate.AddHandler(Button.ClickEvent, new RoutedEventHandler(MigrateCharacter_Click));
            actions.AppendChild(migrate);

            var export = new FrameworkElementFactory(typeof(Button));
            export.SetValue(ContentControl.ContentProperty, "Экспорт JSON");
            export.SetValue(Control.PaddingProperty, new Thickness(9, 5, 9, 5));
            export.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 4, 0));
            export.SetBinding(FrameworkElement.TagProperty, new System.Windows.Data.Binding());
            export.AddHandler(Button.ClickEvent, new RoutedEventHandler(ExportCharacter_Click));
            actions.AppendChild(export);

            var clone = new FrameworkElementFactory(typeof(Button));
            clone.SetValue(ContentControl.ContentProperty, "Клонировать");
            clone.SetValue(Control.PaddingProperty, new Thickness(9, 5, 9, 5));
            clone.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 4, 0));
            clone.SetBinding(FrameworkElement.TagProperty, new System.Windows.Data.Binding());
            clone.AddHandler(Button.ClickEvent, new RoutedEventHandler(CloneCharacter_Click));
            actions.AppendChild(clone);

            var delete = new FrameworkElementFactory(typeof(Button));
            delete.SetValue(ContentControl.ContentProperty, "Удалить");
            delete.SetValue(Control.PaddingProperty, new Thickness(9, 5, 9, 5));
            delete.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 4, 0));
            delete.SetBinding(FrameworkElement.TagProperty, new System.Windows.Data.Binding());
            delete.AddHandler(Button.ClickEvent, new RoutedEventHandler(DeleteCharacter_Click));
            actions.AppendChild(delete);

            actions.SetValue(DockPanel.DockProperty, Dock.Right);
            panel.AppendChild(actions);

            var mode = new FrameworkElementFactory(typeof(TextBlock));
            mode.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("DisplayCreationMode")
            {
                StringFormat = "Режим: {0}"
            });
            mode.SetValue(TextBlock.FontSizeProperty, 12.0);
            mode.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            mode.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));
            mode.SetValue(DockPanel.DockProperty, Dock.Right);
            panel.AppendChild(mode);

            var name = new FrameworkElementFactory(typeof(TextBlock));
            name.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("DisplayName"));
            name.SetValue(TextBlock.FontSizeProperty, 15.0);
            name.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            name.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            name.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 12, 0));
            name.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            name.SetValue(FrameworkElement.MinWidthProperty, 80.0);
            panel.AppendChild(name);

            var migrateStyle = new Style(typeof(Button), (Style)FindResource(typeof(Button)));
            migrateStyle.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed));
            var showMigration = new DataTrigger
            {
                Binding = new System.Windows.Data.Binding("CanMigrateToFreeNeonate"),
                Value = true
            };
            showMigration.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible));
            migrateStyle.Triggers.Add(showMigration);
            migrate.SetValue(FrameworkElement.StyleProperty, migrateStyle);

            return new DataTemplate(typeof(CharacterProfile)) { VisualTree = panel };
        }

        private Style CreateCharacterItemStyle()
        {
            var style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8)));
            style.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0, 0, 0, 7)));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            var template = new ControlTemplate(typeof(ListBoxItem));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "CharacterCard";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.PaddingProperty, new Thickness(8));
            border.SetResourceReference(Border.BackgroundProperty, "CardBgBrush");
            border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            border.AppendChild(presenter);
            template.VisualTree = border;
            var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Border.BorderBrushProperty, new DynamicResourceExtension("AccentBrush"), "CharacterCard"));
            selected.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("SelectionBrush"), "CharacterCard"));
            template.Triggers.Add(selected);
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private void DeleteCharacter_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var character = button == null ? null : button.Tag as CharacterProfile;
            if (character == null) return;
            var answer = ThemedDialog.Show(this,
                "Удалить персонажа «" + (character.Name ?? "Без имени") + "» и его сохранение?",
                "Удаление персонажа", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;
            int restoreIndex = Characters.IndexOf(character);
            CharacterStorage.Delete(character);
            Characters.Remove(character);
            UndoService.Store("персонажа «" + (character.Name ?? "Без имени") + "»", () =>
            {
                if (!Characters.Contains(character)) Characters.Insert(Math.Max(0, Math.Min(restoreIndex, Characters.Count)), character);
                SaveCharacters();
            });
            SaveCharacters();
        }

        private void CharacterSelectionWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Z &&
                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0 &&
                (System.Windows.Input.Keyboard.Modifiers & (System.Windows.Input.ModifierKeys.Alt | System.Windows.Input.ModifierKeys.Shift)) == 0)
            {
                if (UndoService.TryUndo(this)) e.Handled = true;
            }
        }

        private void CloneCharacter_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var source = button == null ? null : button.Tag as CharacterProfile;
            if (source == null) return;

            string baseName = (string.IsNullOrWhiteSpace(source.Name) ? "Персонаж" : source.Name) + " — Клон";
            string newName = baseName;
            int suffix = 2;
            while (Characters.Any(c => string.Equals(c.Name, newName, StringComparison.CurrentCultureIgnoreCase)))
                newName = baseName + " (" + suffix++ + ")";

            try
            {
                var copy = CharacterStorage.CreateClone(source, newName);
                Characters.Add(copy);
                SaveCharacters();
                _list.SelectedItem = copy;
            }
            catch (Exception ex)
            {
                ThemedDialog.Show(this,
                    "Не удалось клонировать персонажа. Исходный персонаж и его сохранение не изменены.\n\n" + ex.Message,
                    "Ошибка клонирования", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MigrateCharacter_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var source = button == null ? null : button.Tag as CharacterProfile;
            if (source == null || !source.CanMigrateToFreeNeonate) return;

            var answer = ThemedDialog.Show(this,
                "Вы собираетесь создать отдельную копию персонажа в режиме «Свободный Неонат».\n\n" +
                "Исходный персонаж в режиме «Птенец» останется без изменений.\n\n" +
                "Все данные, включая характеристики, ресурсы и историю покупок, будут скопированы в новый файл. " +
                "Уже приобретённые характеристики и потраченные ресурсы не пересчитываются. " +
                "Дальнейшее развитие копии будет осуществляться по правилам «Свободного Неоната».\n\nПродолжить?",
                "Миграция персонажа", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            string baseName = (source.Name ?? "Персонаж") + " — Стал Неонатом";
            string newName = baseName;
            int suffix = 2;
            while (Characters.Any(c => string.Equals(c.Name, newName, StringComparison.CurrentCultureIgnoreCase)))
                newName = baseName + " (" + suffix++ + ")";

            try
            {
                var copy = CharacterStorage.CreateMigrationCopy(source, newName);
                Characters.Add(copy);
                SaveCharacters();
                _list.SelectedItem = copy;
                OpenCharacter(copy);
            }
            catch (Exception ex)
            {
                ThemedDialog.Show(this,
                    "Не удалось создать копию персонажа. Исходный персонаж и его сохранение не изменены.\n\n" + ex.Message,
                    "Ошибка миграции", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportCharacter_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var character = button == null ? null : button.Tag as CharacterProfile;
            if (character == null) return;
            var dialog = new SaveFileDialog
            {
                Title = "Экспорт персонажа в JSON",
                Filter = "Файлы персонажей JSON (*.json)|*.json",
                DefaultExt = ".json",
                AddExtension = true,
                FileName = MakeSafeFileName(character.Name) + ".json"
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                CharacterStorage.Export(character, dialog.FileName);
                ThemedDialog.Show(this, "Персонаж экспортирован в JSON.", "Экспорт завершён",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ThemedDialog.Show(this, "Не удалось экспортировать персонажа.\n\n" + ex.Message,
                    "Ошибка экспорта", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string MakeSafeFileName(string name)
        {
            string value = string.IsNullOrWhiteSpace(name) ? "Персонаж" : name;
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value;
        }

        private void GenerateCharacter_Click(object sender, RoutedEventArgs e)
        {
            var intro = new CharacterGenerationIntroWindow { Owner = this };
            if (intro.ShowDialog() != true) return;
            try
            {
                var generated = RandomCharacterGenerator.GenerateAndImport();
                Characters.Add(generated);
                SaveCharacters();
                _list.SelectedItem = generated;
                OpenCharacter(generated);
            }
            catch (Exception ex)
            {
                ThemedDialog.Show(this, "Не удалось сгенерировать персонажа.\n\n" + ex.Message,
                    "Ошибка генерации", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Импорт персонажа из JSON",
                Filter = "Файлы персонажей JSON (*.json)|*.json|Все файлы (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                var character = CharacterStorage.Import(dialog.FileName);
                Characters.Add(character);
                SaveCharacters();
                _list.SelectedItem = character;
                ThemedDialog.Show(this, "Персонаж «" + character.Name + "» импортирован.", "Импорт завершён", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ThemedDialog.Show(this, "Не удалось импортировать JSON-файл. Проверь его структуру.\n\n" + ex.Message,
                    "Ошибка импорта", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CharacterCreateWindow { Owner = this };
            if (dialog.ShowDialog() != true) return;
            var character = new CharacterProfile { Name = dialog.CharacterName, CreationMode = dialog.CreationMode };
            Characters.Add(character); SaveCharacters(); _list.SelectedItem = character; OpenSelected();
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            UpdateService.WriteLog("Начат этап установки обновления через EZ_Updater.");
            try
            {
                if (Updater.CannotWriteOnDir)
                {
                    ShowUpdateError("Недостаточно прав для обновления",
                        new UnauthorizedAccessException(
                            "EZ_Updater сообщает, что приложение не может записывать файлы в папку установки. " +
                            "Проверьте права доступа к папке или установите приложение в каталог, доступный для записи."));
                    return;
                }

                var updateWindow = new Window
                {
                    Title = "Обновление VtM V20",
                    Owner = this,
                    Width = 440,
                    Height = 150,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                ThemeManager.AttachWindow(updateWindow);
                var panel = new StackPanel { Margin = new Thickness(18) };
                var status = new TextBlock { Text = "Подготовка обновления…", Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap };
                var progress = new ProgressBar { Height = 18, Minimum = 0, Maximum = 100, IsIndeterminate = true };
                panel.Children.Add(status);
                panel.Children.Add(progress);
                updateWindow.Content = panel;
                updateWindow.Show();
                Updater.GUI_Context = System.Threading.SynchronizationContext.Current;

                UpdateService.WriteLog("EZ_Updater.Update вызван; повторная проверка релиза не выполняется, используется сохранённая browser download URL.");
                Updater.Update((updateSender, args) =>
                {
                    try
                    {
                        status.Text = string.IsNullOrWhiteSpace(Updater.Message) ? "Обновление выполняется…" : Updater.Message;
                        progress.IsIndeterminate = false;
                        progress.Value = Math.Max(0, Math.Min(100, Updater.ProgressPercentage));
                        string state = Convert.ToString(Updater.ShortState);
                        UpdateService.WriteLog("Состояние EZ_Updater: " + state + "; прогресс=" + Updater.ProgressPercentage + "; сообщение=" + Updater.Message);

                        if (string.Equals(state, "Installed", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                UpdateService.WriteLog("ZIP обновления установлен. Запуск новой версии: " + Updater.ProgramFileName);
                                UpdateService.RecordInstallCompleted();
                                System.Diagnostics.Process.Start(Updater.ProgramFileName);
                                Application.Current.Shutdown();
                            }
                            catch (Exception ex)
                            {
                                status.Text = "Обновление установлено, но приложение не удалось перезапустить.";
                                progress.Value = 100;
                                ShowUpdateError("Обновление установлено, но перезапуск не удался", ex);
                            }
                        }
                        else if (string.Equals(state, "Canceled", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(state, "Failed", StringComparison.OrdinalIgnoreCase))
                        {
                            string updaterMessage = string.IsNullOrWhiteSpace(Updater.Message)
                                ? "Обновлятор не предоставил описание ошибки."
                                : Updater.Message;
                            UpdateService.WriteLog("Установка завершилась без успеха: " + state + "; " + updaterMessage);
                            updateWindow.Close();
                            update.Visibility = Visibility.Visible;
                            updateAvailableCached = true;

                            bool canceled = string.Equals(state, "Canceled", StringComparison.OrdinalIgnoreCase);
                            ThemedDialog.Show(this,
                                (canceled ? "Установка была отменена." : "EZ_Updater сообщил об ошибке установки.") +
                                "\n\nСостояние: " + state + "\nСообщение обновлятора: " + updaterMessage,
                                "Проблема при обновлении", MessageBoxButton.OK,
                                canceled ? MessageBoxImage.Warning : MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        try { updateWindow.Close(); } catch { }
                        update.Visibility = Visibility.Visible;
                        updateAvailableCached = true;
                        ShowUpdateError("Исключение во время установки обновления", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                UpdateService.WriteLog("Исключение запуска установки: " + ex);
                updateAvailableCached = true;
                update.Visibility = Visibility.Visible;
                ShowUpdateError("Не удалось запустить установку обновления", ex);
            }
        }

        private void ShowUpdateError(string title, Exception exception)
        {
            UpdateService.WriteLog(title + ": " + exception);
            string details = exception == null
                ? "Неизвестная ошибка."
                : exception.GetType().FullName + ": " + exception.Message;

            if (exception != null && exception.InnerException != null)
                details += "\n\nВнутреннее исключение:\n" +
                    exception.InnerException.GetType().FullName + ": " +
                    exception.InnerException.Message;

            if (exception != null && !string.IsNullOrWhiteSpace(exception.StackTrace))
                details += "\n\nСтек вызовов:\n" + exception.StackTrace;

            ThemedDialog.Show(this,
                "Во время работы механизма обновления произошла ошибка.\n\n" + details,
                title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void Open_Click(object sender, RoutedEventArgs e) => OpenSelected();
        private void OpenSelected()
        {
            if (!(_list.SelectedItem is CharacterProfile character)) return;
            OpenCharacter(character);
        }

        private void OpenCharacter(CharacterProfile character)
        {
            if (character == null) return;
            MainWindow sheet = null;
            try
            {
                // При ошибке меню остаётся доступным
                sheet = new MainWindow(character);
                sheet.Closed += (s, e) =>
                {
                    if (Application.Current == null || Application.Current.Dispatcher.HasShutdownStarted)
                        return;

                    Application.Current.MainWindow = this;
                    if (!IsVisible)
                        Show();
                    WindowState = WindowState.Normal;
                    Activate();
                };

                Application.Current.MainWindow = sheet;
                Hide();
                sheet.WindowState = WindowState.Maximized;
                sheet.Show();
            }
            catch (Exception ex)
            {
                CharacterStorage.ProtectCharacter(character);
                if (sheet != null)
                {
                    try { sheet.Close(); } catch (Exception closeException) { System.Diagnostics.Trace.TraceError("Не удалось закрыть лист персонажа: " + closeException); }
                }
                if (!IsVisible) Show();
                Application.Current.MainWindow = this;
                ThemedDialog.Show(this,
                    "Не удалось открыть сохранение персонажа «" + (character.Name ?? "Без имени") + "». " +
                    "Приложение продолжит работу, а исходный JSON-файл останется нетронутым.\n\n" +
                    "Свяжитесь с разработчиком приложения для восстановления этого сохранения и передайте ему " +
                    "файл из папки «Документы\\VtM_LoCh_fP» вместе с текстом ошибки.\n\n" +
                    "Сведения об ошибке:\n" + ex,
                    "Ошибка загрузки персонажа", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
