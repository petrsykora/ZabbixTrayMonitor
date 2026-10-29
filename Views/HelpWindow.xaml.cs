using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZabbixTrayMonitor.Models;
using ZabbixTrayMonitor.Resources;
using ZabbixTrayMonitor.Services;

// Integrierte Hilfe in der aktuellen Sprache, erreichbar über Tray-Menü, Einstellungen, Info und F1

namespace ZabbixTrayMonitor.Views
{
    public partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            InitializeComponent();

            SourceInitialized += (_, _) => ThemeService.ApplyTitleBarTheme(this, new ConfigService().Load().UseDarkMode);

            Title = string.Format(Strings.Help_Title, ZabbixConfig.AppDisplayName);

            AddTopic(Strings.Help_GettingStarted_Title, Strings.Help_GettingStarted_Text);
            AddTopic(Strings.Help_Install_Title, Strings.Help_Install_Text);
            AddTopic(Strings.Help_Updates_Title, Strings.Help_Updates_Text);
            AddTopic(Strings.Help_Tray_Title, Strings.Help_Tray_Text);
            AddTopic(Strings.Help_Problems_Title, Strings.Help_Problems_Text);
            AddTopic(Strings.Help_Alarms_Title, Strings.Help_Alarms_Text);
            AddTopic(Strings.Help_Settings_Title, Strings.Help_Settings_Text);
            AddTopic(Strings.Help_Severities_Title, Strings.Help_Severities_Text);
            AddTopic(Strings.Help_Security_Title, Strings.Help_Security_Text);
            AddTopic(Strings.Help_Troubleshooting_Title, Strings.Help_Troubleshooting_Text);

            TopicsListBox.SelectedIndex = 0;
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

        // Kapitel als ListBoxItem: Content = Titel (wird in der Liste angezeigt), Tag = Text
        private void AddTopic(string title, string text)
        {
            TopicsListBox.Items.Add(new ListBoxItem { Content = title, Tag = text });
        }

        private void TopicsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TopicsListBox.SelectedItem is not ListBoxItem item)
                return;

            TopicTitleText.Text = item.Content as string;
            TopicText.Text = item.Tag as string;
            ContentScrollViewer.ScrollToTop();
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
