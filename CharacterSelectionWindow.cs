using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Windows;
using System;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using Microsoft.Win32;

namespace VtM_LoCh_fP
{
    internal sealed class CharacterSelectionWindow : Window
    {
        internal static readonly ObservableCollection<CharacterProfile> Characters = new ObservableCollection<CharacterProfile>();
        internal static void SaveCharacters() => CharacterStorage.Save(Characters);
        private readonly ListBox _list;

        public CharacterSelectionWindow()
        {
            Characters.Clear();
            foreach (var saved in CharacterStorage.Load())
                Characters.Add(saved);
            Title = "VtM V20 — Персонажи"; Width = 1120; Height = 560; MinWidth = 900; MinHeight = 420;
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
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var import = new Button { Content = "Импорт JSON", Padding = new Thickness(12,8,12,8), Margin = new Thickness(0,0,8,0) };
            import.Click += Import_Click;
            var create = new Button { Content = "Создать персонажа", Padding = new Thickness(12,8,12,8), Margin = new Thickness(0,0,8,0) }; create.Click += Create_Click;
            var open = new Button { Content = "Открыть", Padding = new Thickness(12,8,12,8), IsEnabled = false }; open.Click += Open_Click;
            _list.SelectionChanged += (s, e) => { open.IsEnabled = _list.SelectedItem != null; };
            buttons.Children.Add(import); buttons.Children.Add(create); buttons.Children.Add(open);
            Grid.SetRow(buttons, 2); root.Children.Add(buttons); Content = root;
            _list.MouseDoubleClick += (s,e) => OpenSelected();
            // Пока окно выбора скрыто, приложение живёт ради открытого листа
            // Закрытие самого меню — единственная команда полного завершения
            Loaded += (s, e) => ShowStorageLoadWarnings();
            Closed += (s, e) =>
            {
                if (Application.Current != null)
                    Application.Current.Shutdown();
            };
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

            MessageBox.Show(this, message, "Ошибка чтения сохранения", MessageBoxButton.OK, MessageBoxImage.Warning);
        }


        private DataTemplate CreateCharacterTemplate()
        {
            var panel = new FrameworkElementFactory(typeof(DockPanel));
            panel.SetValue(DockPanel.LastChildFillProperty, true);

            var actions = new FrameworkElementFactory(typeof(StackPanel));
            actions.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            actions.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            // Кнопка миграции добавляется первой, поэтому всегда занимает
            // крайнюю левую позицию среди видимых действий персонажа.
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
            name.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
            name.SetValue(TextBlock.FontSizeProperty, 15.0);
            name.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            name.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            name.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 12, 0));
            name.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            name.SetValue(FrameworkElement.MinWidthProperty, 80.0);
            panel.AppendChild(name);



            // Явно наследуем общий стиль кнопок приложения: собственный Style без BasedOn
            // отключал неявный стиль из Styles/Controls.xaml и показывал системную кнопку.
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
                "Удалить персонажа «" + (character.Name ?? "Без имени") + "» и его сохранение? Это действие нельзя отменить.",
                "Удаление персонажа", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;
            CharacterStorage.Delete(character);
            Characters.Remove(character);
            SaveCharacters();
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
                    "Не удалось клонировать персонажа. Исходный персонаж и его сохранение не изменены.\\n\\n" + ex.Message,
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
                MessageBox.Show(this, "Персонаж «" + character.Name + "» импортирован.", "Импорт завершён", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось импортировать JSON-файл. Проверь его структуру.\n\n" + ex.Message,
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
                // Сначала полностью создаём лист. При ошибке меню остаётся доступным.
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
                sheet.Show();
            }
            catch (Exception ex)
            {
                CharacterStorage.ProtectCharacter(character);
                if (sheet != null)
                {
                    try { sheet.Close(); } catch { }
                }
                if (!IsVisible) Show();
                Application.Current.MainWindow = this;
                MessageBox.Show(this,
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
