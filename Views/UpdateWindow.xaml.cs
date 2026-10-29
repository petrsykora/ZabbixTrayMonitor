using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using ZabbixTrayMonitor.Models;
using ZabbixTrayMonitor.Resources;
using ZabbixTrayMonitor.Services;

namespace ZabbixTrayMonitor.Views
{
    public partial class UpdateWindow : Window
    {
        private readonly UpdateInfo _update;
        private readonly ConfigService _configService = new();

        public UpdateWindow(UpdateInfo update)
        {
            InitializeComponent();

            _update = update;

            SourceInitialized += (_, _) => ThemeService.ApplyTitleBarTheme(this, _configService.Load().UseDarkMode);

            Title = string.Format(Strings.Update_Title, ZabbixConfig.AppDisplayName);
            CurrentVersionText.Text = UpdateService.FormatVersion(UpdateService.CurrentVersion);
            NewVersionText.Text = UpdateService.FormatVersion(update.Version);
        }

        // Öffnet die Release-Seite im Browser, installiert wird vom Benutzer selbst
        private void Download_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _update.ReleaseUrl,
                    UseShellExecute = true
                });
            }
            catch { }

            Close();
        }

        // Kein erneuter Hinweis für diese Version, erst wieder bei einer neueren
        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = _configService.Load();
                config.SkippedUpdateVersion = _update.TagName;
                _configService.Save(config);
            }
            catch { }

            Close();
        }

        private void Later_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F1)
                HelpWindow.ShowHelp();
        }
    }
}
