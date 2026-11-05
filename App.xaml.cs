using System.Windows;

namespace VtM_LoCh_fP
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            AppSettings.Load();
            if (AppSettings.DarkTheme) ThemeManager.ApplyDark();
            else ThemeManager.ApplyLight();
            var window = new CharacterSelectionWindow();
            MainWindow = window;
            window.Show();
        }
    }
}
