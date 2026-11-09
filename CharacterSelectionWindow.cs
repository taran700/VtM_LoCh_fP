using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Windows;
using System;
using System.Windows.Controls;

namespace VtM_LoCh_fP
{
    internal sealed class CharacterSelectionWindow : Window
    {
        internal static readonly ObservableCollection<CharacterProfile> Characters = new ObservableCollection<CharacterProfile>();
        internal static void SaveCharacters() => CharacterStorage.Save(Characters);
        private readonly ListBox _list;

        public CharacterSelectionWindow()
        {
            foreach (var saved in CharacterStorage.Load())
                Characters.Add(saved);
            Title = "VtM V20 — Персонажи"; Width = 560; Height = 430; MinWidth = 460; MinHeight = 340;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ThemeManager.AttachWindow(this);
            var root = new Grid { Margin = new Thickness(20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var title = new TextBlock { Text = "Персонажи", FontSize = 24, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,14) };
            title.SetResourceReference(TextBlock.ForegroundProperty, "HeadingBrush");
            root.Children.Add(title);
            _list = new ListBox { ItemsSource = Characters, Margin = new Thickness(0,0,0,14), Padding = new Thickness(6) }; Grid.SetRow(_list, 1); root.Children.Add(_list);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var create = new Button { Content = "Создать персонажа", Padding = new Thickness(14,8,14,8), Margin = new Thickness(0,0,8,0) }; create.Click += Create_Click;
            var open = new Button { Content = "Открыть", Padding = new Thickness(14,8,14,8) }; open.Click += Open_Click;
            buttons.Children.Add(create); buttons.Children.Add(open); Grid.SetRow(buttons, 2); root.Children.Add(buttons); Content = root;
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
