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
            EventManager.RegisterClassHandler(typeof(ComboBox), UIElement.PreviewMouseWheelEvent,
                new MouseWheelEventHandler(ComboBox_PreviewMouseWheel), true);
            InputManager.Current.PreProcessInput += OnPreProcessInput;
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            AppSettings.Load();
            CleanupUpdaterBackups();
            if (AppSettings.DarkTheme) ThemeManager.ApplyDark();
            else ThemeManager.ApplyLight();
            var window = new CharacterSelectionWindow();
            MainWindow = window;
            window.Show();
        }

        private static void CleanupUpdaterBackups()
        {
            // EZ_Updater может оставлять резервные копии .EZold не только рядом с EXE,
            // но и во вложенных каталогах Data и Generation. Удаляем их при старте.
            string executablePath;
            try
            {
                executablePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            }
            catch (Exception ex)
            {
                WriteStartupLog("Не удалось определить путь EXE для очистки .EZold: " + ex);
                return;
            }

            string applicationDirectory = System.IO.Path.GetDirectoryName(executablePath);
            if (string.IsNullOrWhiteSpace(applicationDirectory) || !System.IO.Directory.Exists(applicationDirectory))
            {
                WriteStartupLog("Папка приложения для очистки .EZold не найдена: " + (applicationDirectory ?? "<null>"));
                return;
            }

            // Корень приложения, а также Data и Generation со всеми их вложенными папками.
            // Остальные каталоги приложения не затрагиваются.
            CleanupEzOldInDirectory(applicationDirectory, false);
            string[] knownDataDirectories = { "Data", "Generation" };
            foreach (string directoryName in knownDataDirectories)
            {
                string nestedDirectory = System.IO.Path.Combine(applicationDirectory, directoryName);
                if (System.IO.Directory.Exists(nestedDirectory))
                    CleanupEzOldInDirectory(nestedDirectory, true);
            }
        }

        private static void CleanupEzOldInDirectory(string directory, bool recursive)
        {
            try
            {
                foreach (string backupFile in System.IO.Directory.GetFiles(directory, "*.EZold", System.IO.SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        System.IO.FileAttributes attributes = System.IO.File.GetAttributes(backupFile);
                        if ((attributes & System.IO.FileAttributes.ReadOnly) != 0)
                            System.IO.File.SetAttributes(backupFile, attributes & ~System.IO.FileAttributes.ReadOnly);

                        System.IO.File.Delete(backupFile);
                        WriteStartupLog("Удалён файл обновлятора: " + backupFile);
                    }
                    catch (Exception ex)
                    {
                        WriteStartupLog("Не удалось удалить .EZold «" + backupFile + "»: " + ex);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteStartupLog("Ошибка перечисления .EZold в «" + directory + "»: " + ex);
            }

            if (!recursive)
                return;

            string[] childDirectories;
            try
            {
                childDirectories = System.IO.Directory.GetDirectories(directory, "*", System.IO.SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex)
            {
                WriteStartupLog("Не удалось получить вложенные папки «" + directory + "»: " + ex);
                return;
            }

            foreach (string childDirectory in childDirectories)
                CleanupEzOldInDirectory(childDirectory, true);
        }

        private static void WriteStartupLog(string message)
        {
            try
            {
                string logDirectory = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "VtM_LoCh_fP", "logs");
                System.IO.Directory.CreateDirectory(logDirectory);
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(logDirectory, "updater.log"),
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] [StartupCleanup] " +
                    (message ?? string.Empty) + Environment.NewLine);
            }
            catch
            {
                // Ошибка журналирования не должна препятствовать запуску.
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
