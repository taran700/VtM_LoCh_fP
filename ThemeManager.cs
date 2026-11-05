using System;
using System.Windows;

namespace VtM_LoCh_fP
{
    /// <summary>
    /// Переключает тему, подменяя словарь ресурсов с палитрой.
    /// Словарь стилей (Styles/Controls.xaml) при этом не пересоздаётся,
    /// поэтому все стили продолжают ссылаться на DynamicResource и
    /// перекрашиваются автоматически.
    /// </summary>
    internal static class ThemeManager
    {
        private const int ThemeDictionaryIndex = 0;

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
        }
    }
}
