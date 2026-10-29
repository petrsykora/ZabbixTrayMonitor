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

            if (UpdateInstaller.CanInstall)
            {
                DescriptionText.Text = Strings.Update_DescriptionInstall;
                InstallButton.IsDefault = true;
            }
            else
            {
                // ZIP-Version: nur die Release-Seite öffnen
                DescriptionText.Text = Strings.Update_Description;
                InstallButton.Visibility = Visibility.Collapsed;
                DownloadButton.IsDefault = true;
            }
        }

        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is not MainWindow mainWindow)
                return;

            SetButtonsEnabled(false);
            ProgressText.Text = Strings.Update_Downloading;
            ProgressText.Visibility = Visibility.Visible;

            var started = await mainWindow.InstallUpdateAsync(_update, interactive: true);

            // Das Fenster kann während des Downloads geschlossen worden sein
            if (!IsLoaded)
                return;

            if (started)
            {
                Close();
                return;
            }

            ProgressText.Visibility = Visibility.Collapsed;
            SetButtonsEnabled(true);
        }

        private void SetButtonsEnabled(bool enabled)
        {
            InstallButton.IsEnabled = enabled;
            DownloadButton.IsEnabled = enabled;
            SkipButton.IsEnabled = enabled;
            LaterButton.IsEnabled = enabled;
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
