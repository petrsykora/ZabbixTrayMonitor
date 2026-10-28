using System.Windows;
using ZabbixTrayMonitor.Services;

namespace ZabbixTrayMonitor
{
    /// <summary>
    /// Interaktionslogik für App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Theme und Sprache aus gespeicherter Config anwenden
            try
            {
                var configService = new ConfigService();
                if (configService.ConfigExists())
                {
                    var cfg = configService.Load();
                    LocalizationService.ApplyLanguage(cfg.Language);
                    ThemeService.ApplyTheme(cfg.UseDarkMode);
                }
            }
            catch { }
        }
    }
}
