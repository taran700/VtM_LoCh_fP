using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Controls.Primitives;

namespace VtM_LoCh_fP
{
    internal static class ThemeManager
    {
        private const int ThemeDictionaryIndex = 0;
        private const int DwmUseImmersiveDarkMode = 20;
        private const int DwmUseImmersiveDarkModeLegacy = 19;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd, int attribute, ref int value, int valueSize);

        internal static bool IsDark { get; private set; }

        internal static void ApplyLight() => Apply("Themes/LightTheme.xaml", false);

        internal static void ApplyDarkIfSelected()
        {
            if (AppSettings.DarkTheme) ApplyDark();
            else ApplyLight();
        }

        internal static void ApplyDark() => Apply("Themes/DarkTheme.xaml", true);

        private static void Apply(string relativePath, bool dark)
        {
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            var theme = new ResourceDictionary
            {
                Source = new Uri(relativePath, UriKind.Relative)
            };

            if (dictionaries.Count > ThemeDictionaryIndex)
                dictionaries[ThemeDictionaryIndex] = theme;
            else
                dictionaries.Insert(ThemeDictionaryIndex, theme);

            IsDark = dark;
            RefreshOpenWindows();
        }

        internal static void AttachWindow(Window window)
        {
            window.SetResourceReference(Window.BackgroundProperty, "BgBrush");
            window.SetResourceReference(Control.ForegroundProperty, "TextBrush");
            window.SourceInitialized += (s, e) => ApplyNativeTitleBar(window);
        }

        private static void RefreshOpenWindows()
        {
            foreach (Window window in Application.Current.Windows)
            {
                window.SetResourceReference(Window.BackgroundProperty, "BgBrush");
                window.SetResourceReference(Control.ForegroundProperty, "TextBrush");
                if (window.IsLoaded) ApplyNativeTitleBar(window);
            }
        }

        private static void ApplyNativeTitleBar(Window window)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;

                int value = IsDark ? 1 : 0;
                int result = DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkMode, ref value, sizeof(int));
                if (result != 0)
                    DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkModeLegacy, ref value, sizeof(int));
            }
            catch
            {
                
            }
        }
    }
}
