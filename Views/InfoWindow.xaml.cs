using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using ZabbixTrayMonitor.Models;
using ZabbixTrayMonitor.Resources;
using ZabbixTrayMonitor.Services;

namespace ZabbixTrayMonitor.Views
{
    public partial class InfoWindow : Window
    {
        public InfoWindow()
        {
            InitializeComponent();

            var configService = new ConfigService();

            // Systemtitelleiste an Dark/Light Mode anpassen, Handle gibt es erst ab SourceInitialized
            SourceInitialized += (_, _) => ThemeService.ApplyTitleBarTheme(this, configService.Load().UseDarkMode);

            Title = string.Format(Strings.Info_Title, ZabbixConfig.AppDisplayName);
            AppNameText.Text = ZabbixConfig.AppDisplayName;

            try
            {
                var assembly = Assembly.GetEntryAssembly(); // Assembly ist die gebaute exe
                var assemblyVersion = assembly?.GetName().Version?.ToString() ?? Strings.Common_Unknown;
                var location = Environment.ProcessPath ?? AppContext.BaseDirectory;

                VersionText.Text = GetFileVersion(location, assemblyVersion);

                // "ZabbixTrayMonitor von Darano94" mit Link auf das ursprüngliche Projekt
                var basedOn = string.Format(Strings.About_BasedOnValue, "\u0000", AppInfo.OriginalAuthor).Split('\u0000');
                BasedOnText.Inlines.Add(basedOn[0]);
                BasedOnText.Inlines.Add(CreateLink(AppInfo.OriginalProjectName, AppInfo.OriginalProjectUrl));
                if (basedOn.Length > 1)
                    BasedOnText.Inlines.Add(basedOn[1]);

                ChangesByText.Inlines.Add(AppInfo.ChangesAuthor + " (");
                ChangesByText.Inlines.Add(CreateLink(AppInfo.ProjectUrl.Replace("https://", ""), AppInfo.ProjectUrl));
                ChangesByText.Inlines.Add(")");

                // MIT-Lizenz: Copyright des ursprünglichen Autors und der vollständige Lizenztext müssen erhalten bleiben
                var licenseText = ReadEmbeddedLicense();
                var copyrightLines = licenseText?
                    .Split('\n')
                    .Select(line => line.Trim())
                    .Where(line => line.StartsWith("Copyright", StringComparison.OrdinalIgnoreCase))
                    .ToList() ?? new List<string>();

                LicenseText.Inlines.Add(AppInfo.License);

                foreach (var copyrightLine in copyrightLines)
                {
                    LicenseText.Inlines.Add(new LineBreak());
                    LicenseText.Inlines.Add(copyrightLine);
                }

                if (!string.IsNullOrWhiteSpace(licenseText))
                {
                    var showLicenseLink = new Hyperlink(new Run(Strings.About_ShowLicense));
                    showLicenseLink.Click += (_, _) => MessageBox.Show(
                        this,
                        licenseText,
                        Strings.About_License,
                        MessageBoxButton.OK,
                        MessageBoxImage.None);

                    LicenseText.Inlines.Add(new LineBreak());
                    LicenseText.Inlines.Add(showLicenseLink);
                }

                ProgramText.Text = location;

                // Link öffnet den Ordner mit der config.json im Explorer
                var configPath = configService.GetConfigPath();
                ConfigText.Inlines.Add(CreateLink(configPath, Path.GetDirectoryName(configPath) ?? configPath));
            }
            catch
            {
                VersionText.Text = Strings.Common_Unknown;
            }
        }

        // LICENSE ist als Ressource eingebettet (siehe ZabbixTrayMonitor.csproj), damit der Text auch ohne LICENSE.txt verfügbar ist
        private static string? ReadEmbeddedLicense()
        {
            try
            {
                using var stream = typeof(InfoWindow).Assembly.GetManifestResourceStream("ZabbixTrayMonitor.LICENSE");

                if (stream == null)
                    return null;

                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Replace("\r\n", "\n").Trim();
            }
            catch
            {
                return null;
            }
        }

        private static Hyperlink CreateLink(string text, string target)
        {
            var link = new Hyperlink(new Run(text)) { ToolTip = target };
            link.Click += (_, _) => OpenTarget(target);
            return link;
        }

        // Öffnet Webseiten nur über http/https, Ordner nur wenn sie existieren
        private static void OpenTarget(string target)
        {
            try
            {
                var isWebLink = Uri.TryCreate(target, UriKind.Absolute, out var uri) &&
                                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

                if (!isWebLink && !Directory.Exists(target))
                    return;

                Process.Start(new ProcessStartInfo
                {
                    FileName = isWebLink ? uri!.AbsoluteUri : target,
                    UseShellExecute = true
                });
            }
            catch { }
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

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            HelpWindow.ShowHelp();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
            else if (e.Key == Key.F1)
                HelpWindow.ShowHelp();
        }
    }
}
