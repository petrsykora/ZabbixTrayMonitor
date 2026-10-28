using System.Globalization;
using ZabbixTrayMonitor.Resources;

// Setzt die Sprache der Oberfläche anhand der Option in den Einstellungen
// Texte liegen in Resources/Strings*.resx: Strings.resx = Englisch (Fallback), Strings.de.resx = Deutsch, Strings.cs.resx = Tschechisch
// Es wird nur die UI-Sprache (CurrentUICulture) umgestellt, Datums-/Zahlenformate bleiben wie im System eingestellt

namespace ZabbixTrayMonitor.Services
{
    public static class LocalizationService
    {
        public const string SystemLanguage = "";

        // Unterstützte Sprachen für die Auswahl in den Einstellungen
        public static readonly string[] SupportedLanguages = { "en", "de", "cs" };

        // Systemsprache beim ersten Aufruf merken, damit später wieder auf "System" zurückgeschaltet werden kann
        private static readonly CultureInfo _systemUICulture = CultureInfo.CurrentUICulture;

        public static string NormalizeLanguage(string? language)
        {
            var value = language?.Trim().ToLowerInvariant() ?? SystemLanguage;

            return Array.IndexOf(SupportedLanguages, value) >= 0
                ? value
                : SystemLanguage;
        }

        public static void ApplyLanguage(string? language)
        {
            var normalizedLanguage = NormalizeLanguage(language);

            var culture = normalizedLanguage == SystemLanguage
                ? _systemUICulture
                : CultureInfo.GetCultureInfo(normalizedLanguage);

            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            // null = Strings verwendet immer die aktuelle CurrentUICulture
            Strings.Culture = null;
        }
    }
}
