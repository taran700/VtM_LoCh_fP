using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using VtM_LoCh_fP.Controls;
using VtM_LoCh_fP.ViewModels;

namespace VtM_LoCh_fP
{
    /// <summary>
    /// Окно-оболочка: заголовок, переключение темы и вкладки-страницы.
    /// Само содержимое страниц лежит во вью (Views/), а не собирается в коде.
    /// </summary>
    public partial class MainWindow : Window
    {
        private const double DragThreshold = 6.0;

        private TabItem _draggedTab;
        private Point _dragStart;
        private DragAdorner _adorner;
        private AdornerLayer _adornerLayer;
        private bool _isDragging;

        private readonly CharacterProfile _character;

        internal MainWindow(CharacterProfile character)
        {
            ThemeManager.ApplyDarkIfSelected();
            InitializeComponent();
            ThemeManager.AttachWindow(this);
            _character = character;
            CharacterPage.InitializeCharacter(character);
            Title = "VtM V20: " + character.Name + " — " + character.CreationMode;
            var rulesService = new CharacterRulesService(character);
            var disciplineService = new DisciplineService();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            bool previousTheme = AppSettings.DarkTheme;
            var settings = new SettingsWindow { Owner = this };

            if (settings.ShowDialog() != true)
                return;

            if (previousTheme != AppSettings.DarkTheme)
            {
                ThemeManager.ApplyDarkIfSelected();

                var replacement = new MainWindow(_character)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = Left,
                    Top = Top,
                    Width = Width,
                    Height = Height,
                    WindowState = WindowState
                };

                Application.Current.MainWindow = replacement;
                replacement.Show();
                Close();
            }
            else
            {
                ThemeManager.ApplyDarkIfSelected();
            }
        }

        // ===================== Перетаскивание вкладок =====================

        private void Tabs_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _draggedTab = FindVisualParent<TabItem>(e.OriginalSource as DependencyObject);
            _dragStart = e.GetPosition(SheetTabs);
        }

        private void Tabs_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedTab == null || e.LeftButton != MouseButtonState.Pressed) return;

            Point current = e.GetPosition(SheetTabs);
            if (!_isDragging &&
                Math.Abs(current.X - _dragStart.X) < DragThreshold &&
                Math.Abs(current.Y - _dragStart.Y) < DragThreshold) return;

            if (!_isDragging) BeginDrag(_dragStart);

            _adorner?.MoveTo(current);
            e.Handled = true;
        }

        private void BeginDrag(Point grabOffset)
        {
            _isDragging = true;
            _adorner = new DragAdorner(SheetTabs, _draggedTab, grabOffset);
            _adornerLayer = AdornerLayer.GetAdornerLayer(SheetTabs);
            _adornerLayer?.Add(_adorner);
            Mouse.OverrideCursor = Cursors.SizeAll;
        }

        private void Tabs_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging)
            {
                _draggedTab = null;
                return;
            }

            EndDrag();
            e.Handled = true;
        }

        private void EndDrag()
        {
            if (_adorner != null && _adornerLayer != null) _adornerLayer.Remove(_adorner);
            _adorner = null;
            _adornerLayer = null;
            _draggedTab = null;
            _isDragging = false;
            Mouse.OverrideCursor = null;
        }

        private void Tabs_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void Tabs_Drop(object sender, DragEventArgs e)
        {
            EndDrag();
            e.Handled = true;
        }

        private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(e.AddedItems.Count > 0) || !(e.AddedItems[0] is TabItem tab)) return;
            if (!(tab.Content is FrameworkElement content)) return;

            // При переключении вкладки всегда показываем её начало.
            content.Dispatcher.BeginInvoke(new Action(() =>
            {
                var scroll = FindVisualChild<ScrollViewer>(content);
                scroll?.ScrollToTop();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        // ===================== Поиск в визуальном дереве =====================

        private static T FindVisualParent<T>(DependencyObject node) where T : DependencyObject
        {
            while (node != null)
            {
                if (node is T parent) return parent;
                node = node is Visual || node is System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(node)
                    : LogicalTreeHelper.GetParent(node);
            }
            return null;
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match) return match;
                var nested = FindVisualChild<T>(child);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}
