using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZabbixTrayMonitor.Models;
using ZabbixTrayMonitor.Resources;
using ZabbixTrayMonitor.Services;

// Hilfefenster mit kurzer Anleitung in der aktuellen Sprache, erreichbar über Tray-Menü, Einstellungen, Info und F1

namespace ZabbixTrayMonitor.Views
{
    public partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            InitializeComponent();

            SourceInitialized += (_, _) => ThemeService.ApplyTitleBarTheme(this, new ConfigService().Load().UseDarkMode);

            Title = string.Format(Strings.Help_Title, ZabbixConfig.AppDisplayName);
            DocumentationLink.ToolTip = AppInfo.DocumentationUrl;

            AddSection(Strings.Help_GettingStarted_Title, Strings.Help_GettingStarted_Text);
            AddSection(Strings.Help_Tray_Title, Strings.Help_Tray_Text);
            AddSection(Strings.Help_Problems_Title, Strings.Help_Problems_Text);
            AddSection(Strings.Help_Alarms_Title, Strings.Help_Alarms_Text);
            AddSection(Strings.Help_Settings_Title, Strings.Help_Settings_Text);
            AddSection(Strings.Help_Troubleshooting_Title, Strings.Help_Troubleshooting_Text);
        }

        // Nur ein Hilfefenster gleichzeitig, ein offenes wird nach vorne geholt
        public static void ShowHelp()
        {
            var existing = Application.Current.Windows.OfType<HelpWindow>().FirstOrDefault();

            if (existing != null)
            {
                if (existing.WindowState == WindowState.Minimized)
                    existing.WindowState = WindowState.Normal;

                existing.Activate();
                return;
            }

            new HelpWindow().Show();
        }

        private void AddSection(string title, string text)
        {
            SectionsPanel.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, SectionsPanel.Children.Count == 0 ? 0 : 14, 0, 6)
            });

            SectionsPanel.Children.Add(new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap
            });

            // Farben aus dem Theme (DynamicResource), damit Dark/Light Mode passt
            foreach (var textBlock in SectionsPanel.Children.OfType<TextBlock>().Skip(SectionsPanel.Children.Count - 2))
                textBlock.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush");
        }

        private void DocumentationLink_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = AppInfo.DocumentationUrl,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }
    }
}
