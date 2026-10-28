// Repräsentiert die gespeicherte Zabbix-Konfiguration

using System.Text.Json.Serialization;
using ZabbixTrayMonitor.Services;

namespace ZabbixTrayMonitor.Models
{
    public class ZabbixConfig
    {
        // Fester Anzeigename der Anwendung (Fenstertitel, Tooltip)
        public const string AppDisplayName = "Zabbix Monitor";

        // Fester Prefix der Credential-Targets im Windows Credential Manager ("ZabbixTrayMonitor.ApiToken"),
        // bleibt bewusst beim alten Namen damit bereits gespeicherte Tokens weiter gefunden werden
        public const string CredentialAppName = "ZabbixTrayMonitor";

        // Pfad der JSON-RPC API ist in Zabbix fest vorgegeben, ein Unterverzeichnis gehört in die ZabbixUrl
        public const string ZabbixApiPath = "/api_jsonrpc.php";

        public string ZabbixUrl { get; set; } = "";
        public string ZabbixDashboardUrl { get; set; } = "";

        public int PollIntervalSeconds { get; set; } = 60;
        public int TrayToolTipDelayMilliseconds { get; set; } = 250;
        public bool IgnoreCertificateErrors { get; set; } = false; // Ignoriert SSL Fehler zB. bei self-signed Zertifikaten
        public bool UseDarkMode { get; set; } = true;

        // Sprache der Oberfläche: "" = Systemsprache (Standard), "en", "de" oder "cs"
        public string Language { get; set; } = LocalizationService.DefaultLanguage;

        // Alarmbearbeitung
        public const string LegacyAcknowledgeMessage = "Acknowledged via ZabbixTrayMonitor";
        public static string DefaultAcknowledgeMessage =>
            string.Format(ZabbixTrayMonitor.Resources.Strings.Acknowledge_DefaultMessage, System.Environment.UserName);

        public static string ResolveAcknowledgeMessage(string? configuredMessage)
        {
            if (string.IsNullOrWhiteSpace(configuredMessage))
                return DefaultAcknowledgeMessage;

            var message = configuredMessage.Trim();

            return string.Equals(
                message,
                LegacyAcknowledgeMessage,
                System.StringComparison.OrdinalIgnoreCase)
                ? DefaultAcknowledgeMessage
                : message;
        }

        public bool ShowSuppressedProblems { get; set; } = false;
        public bool RefreshAfterProblemAction { get; set; } = true;
        public string AcknowledgeMessage { get; set; } = DefaultAcknowledgeMessage;

        // Früher konfigurierbar, wird nur noch für die Migration aus alten config.json gelesen (ConfigService.Load)
        // und danach nicht mehr gespeichert
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AppName { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ZabbixApiEndpoint { get; set; }

        // Schwellenwerte für die von der Zabbix API zurückgegebenen Severity-Werte
        /**
            0 = Not classified
            1 = Information
            2 = Warning
            3 = Average
            4 = High
            5 = Disaster
         */
        public int WarningSeverityThreshold { get; set; } = 2;
        public int ErrorSeverityThreshold { get; set; } = 4;

        // Feste Farben für Statusanzeige und ProblemsWindow (nicht mehr konfigurierbar, werden nicht gespeichert)
        [JsonIgnore]
        public string StatusColorError => "#FF0015";

        [JsonIgnore]
        public string StatusColorWarning => "#F3C601";

        [JsonIgnore]
        public string StatusColorInfo => "#808080";

        // Default Credential Einstellungen
        public string CredentialTargetSuffix { get; set; } = "ApiToken"; // Suffix nach {CredentialAppName}
        public string CredentialUsername { get; set; } = "api-token";
    }
}