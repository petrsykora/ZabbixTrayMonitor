using System.Windows;
using Microsoft.Win32;

// Schriftgröße nach Systemeinstellung: Windows-Systemschrift x "Textgröße" aus Einstellungen > Barrierefreiheit.
// WPF berücksichtigt die Textgröße von Windows 10/11 nicht von selbst, deshalb wird sie hier gelesen und
// als Ressourcen AppFontSize (Standard), AppFontSizeLarge (Fließtext der Hilfe) und AppFontSizeTitle bereitgestellt.

namespace ZabbixTrayMonitor.Services
{
    public static class FontScaleService
    {
        public const string FontSizeKey = "AppFontSize";
        public const string LargeFontSizeKey = "AppFontSizeLarge";
        public const string TitleFontSizeKey = "AppFontSizeTitle";

        private const string AccessibilityRegistryKey = @"Software\Microsoft\Accessibility";
        private const string TextScaleFactorValue = "TextScaleFactor"; // 100 bis 225 (Prozent)

        private static bool _listening;

        public static void Apply()
        {
            var fontSize = SystemFonts.MessageFontSize * ReadTextScaleFactor();
            var resources = Application.Current.Resources;

            resources[FontSizeKey] = fontSize;
            resources[LargeFontSizeKey] = fontSize * 1.15;
            resources[TitleFontSizeKey] = fontSize * 1.4;
        }

        // Änderungen der Textgröße oder Systemschrift während der Laufzeit übernehmen
        public static void StartListening()
        {
            if (_listening)
                return;

            _listening = true;
            SystemEvents.UserPreferenceChanged += (_, _) =>
                Application.Current?.Dispatcher.BeginInvoke(new Action(Apply));
        }

        private static double ReadTextScaleFactor()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(AccessibilityRegistryKey);

                if (key?.GetValue(TextScaleFactorValue) is int percent && percent >= 100 && percent <= 225)
                    return percent / 100.0;
            }
            catch { }

            return 1.0;
        }
    }
}
