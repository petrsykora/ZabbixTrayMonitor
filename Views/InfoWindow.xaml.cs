using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using ZabbixTrayMonitor.Models;
using ZabbixTrayMonitor.Services;
using ZabbixTrayMonitor.Resources;

namespace ZabbixTrayMonitor.Views
{
    public partial class InfoWindow : Window
    {
        public InfoWindow()
        {
            InitializeComponent();

            try
            {
                var configService = new ConfigService();
                var appName = ZabbixConfig.AppDisplayName;

                var assembly = Assembly.GetEntryAssembly(); // Assembly ist die gebaute exe
                var assemblyVersion = assembly?.GetName().Version?.ToString() ?? Strings.Common_Unknown;
                var location = Environment.ProcessPath ?? AppContext.BaseDirectory;
                var fileVersion = GetFileVersion(location, assemblyVersion);
                var configPath = configService.GetConfigPath();

                Title = string.Format(Strings.Info_Title, appName);
                HeaderTitleInfo.Text = Title;

                AppNameText.Text = appName;
                VersionText.Text = string.Format(Strings.Info_Version, fileVersion);
                ProgramText.Text = string.IsNullOrWhiteSpace(location)
                    ? string.Empty
                    : string.Format(Strings.Info_Program, location);

                ConfigText.Text = string.IsNullOrWhiteSpace(configPath)
                    ? string.Empty
                    : string.Format(Strings.Info_Config, configPath);
            }
            catch
            {
                AppNameText.Text = ZabbixConfig.AppDisplayName;
                VersionText.Text = string.Format(Strings.Info_Version, Strings.Common_Unknown);
                ProgramText.Text = string.Empty;
                ConfigText.Text = string.Empty;
            }
        }

        private static string GetFileVersion(string location, string fallbackVersion)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(location))
                    return fallbackVersion;

                // versucht die Produktversion zu bekommen sonst die Dateiversion sonst die Assembly-Version
                var info = FileVersionInfo.GetVersionInfo(location);

                return info.ProductVersion
                    ?? info.FileVersion
                    ?? fallbackVersion;
            }
            catch
            {
                return fallbackVersion;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                    DragMove();
            }
            catch { }
        }
    }
}
