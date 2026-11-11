using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Runtime.InteropServices;
using System;

namespace VtM_LoCh_fP
{
    public partial class App : Application
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out NativePoint point);

        protected override void OnStartup(StartupEventArgs e)
        {
            // Используем TLS 1.2 для всех сетевых запросов, в том числе на старых Windows.
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            base.OnStartup(e);
            CleanupUpdaterBackups();
            EventManager.RegisterClassHandler(typeof(ComboBox), UIElement.PreviewMouseWheelEvent,
                new MouseWheelEventHandler(ComboBox_PreviewMouseWheel), true);
            InputManager.Current.PreProcessInput += OnPreProcessInput;
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            AppSettings.Load();
            if (AppSettings.DarkTheme) ThemeManager.ApplyDark();
            else ThemeManager.ApplyLight();
            var window = new CharacterSelectionWindow();
            MainWindow = window;
            window.Show();
        }

        private static void CleanupUpdaterBackups()
        {
            // EZ_Updater оставляет резервные копии рядом с приложением с расширением .EZold.
            // Удаляем их только при старте уже запущенного приложения, то есть после того,
            // как новая версия смогла запуститься. Не обходим вложенные папки и не трогаем
            // пользовательские данные.
            string applicationDirectory = AppDomain.CurrentDomain.BaseDirectory;
            try
            {
                foreach (string backupFile in System.IO.Directory.GetFiles(
                    applicationDirectory, "*.EZold", System.IO.SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        System.IO.File.Delete(backupFile);
                    }
                    catch (System.IO.IOException)
                    {
                        // Если файл ещё занят, оставляем его для следующей попытки при запуске.
                    }
                    catch (System.UnauthorizedAccessException)
                    {
                        // Нет прав на удаление — не мешаем запуску приложения.
                    }
                    catch (System.Security.SecurityException)
                    {
                        // Политика безопасности запрещает удаление — оставляем резервную копию.
                    }
                }
            }
            catch (System.IO.IOException)
            {
                // Ошибка очистки не должна блокировать запуск приложения.
            }
            catch (System.UnauthorizedAccessException)
            {
                // Ошибка доступа не должна блокировать запуск приложения.
            }
            catch (System.Security.SecurityException)
            {
                // Ограничения безопасности не должны блокировать запуск приложения.
            }
        }

        private static void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
        {
            if (!(e.StagingItem.Input is MouseWheelEventArgs wheelEvent))
                return;

            var openCombo = FindOpenComboBox();
            if (openCombo == null)
                return;
            var popup = openCombo.Template?.FindName("PART_Popup", openCombo) as Popup;
            var popupChild = popup?.Child as FrameworkElement;
            if (popupChild == null || popup == null || !popup.IsOpen)
            {
                openCombo.IsDropDownOpen = false;
                return;
            }

            try
            {
                if (!GetCursorPos(out NativePoint nativePoint))
                    return;

                Point screenPoint = new Point(nativePoint.X, nativePoint.Y);
                Point popupPoint = popupChild.PointFromScreen(screenPoint);
                var actualBounds = new Rect(0, 0, popupChild.ActualWidth, popupChild.ActualHeight);
                const double edgeTolerance = 12.0;
                var expandedBounds = new Rect(
                    -edgeTolerance,
                    -edgeTolerance,
                    popupChild.ActualWidth + edgeTolerance * 2,
                    popupChild.ActualHeight + edgeTolerance * 2);

                if (actualBounds.Contains(popupPoint) || expandedBounds.Contains(popupPoint))
                {
                    wheelEvent.Handled = true;

                    var dropDownViewer = openCombo.Template?.FindName(
                        "PART_DropDownScrollViewer", openCombo) as ScrollViewer;
                    if (dropDownViewer == null)
                        dropDownViewer = FindScrollViewer(popupChild);

                    if (dropDownViewer != null && dropDownViewer.ScrollableHeight > 0 && wheelEvent.Delta != 0)
                    {
                        int wheelLines = SystemParameters.WheelScrollLines;
                        if (wheelLines < 1) wheelLines = 3;
                        int notches = Math.Max(1, (int)Math.Ceiling(Math.Abs(wheelEvent.Delta) / 120.0));
                        int lines = wheelLines * notches;

                        for (int i = 0; i < lines; i++)
                        {
                            if (wheelEvent.Delta > 0)
                            {
                                if (dropDownViewer.VerticalOffset <= 0) break;
                                dropDownViewer.LineUp();
                            }
                            else
                            {
                                if (dropDownViewer.VerticalOffset >= dropDownViewer.ScrollableHeight) break;
                                dropDownViewer.LineDown();
                            }
                        }

                        wheelEvent.Handled = true;
                    }
                    return;
                }

                openCombo.IsDropDownOpen = false;
            }
            catch (InvalidOperationException)
            {
                openCombo.IsDropDownOpen = false;
            }
        }

        private static ScrollViewer FindScrollViewer(DependencyObject parent)
        {
            if (parent is ScrollViewer viewer)
                return viewer;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var result = FindScrollViewer(VisualTreeHelper.GetChild(parent, i));
                if (result != null)
                    return result;
            }

            return null;
        }

        private static ComboBox FindOpenComboBox()
        {
            foreach (Window window in Application.Current.Windows)
            {
                var combo = FindOpenComboBoxInTree(window);
                if (combo != null)
                    return combo;
            }

            return null;
        }

        private static ComboBox FindOpenComboBoxInTree(DependencyObject parent)
        {
            if (parent is ComboBox combo && combo.IsDropDownOpen)
                return combo;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var result = FindOpenComboBoxInTree(VisualTreeHelper.GetChild(parent, i));
                if (result != null)
                    return result;
            }

            return null;
        }

        private static void ComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
        }


    }
}
