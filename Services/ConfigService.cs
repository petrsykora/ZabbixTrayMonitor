using System;
using System.IO;
using System.Text.Json;
using ZabbixTrayMonitor.Models;

// Lädt und speichert die Einstellungen als JSON in %appdata%

namespace ZabbixTrayMonitor.Services
{
    public class ConfigService
    {
        private readonly string _appFolder;
        private readonly string _configPath;
        private const string ConfigFilename = "config.json";
        private const string CorruptConfigBackupFilename = "config.corrupt.json";

        // konstanter AppData-Ordnername (nicht abhängig von konfigurierbarem AppName) .. Todo
        private const string AppDataBaseName = "ZabbixTrayMonitor";

        // formatiert JSON mit Zeilenumbrüchen und Einrückungen sonst ist es ein langer oneliner
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public ConfigService()
        {
            _appFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppDataBaseName
            );

            _configPath = Path.Combine(_appFolder, ConfigFilename);
        }

        public bool ConfigExists()
        {
            return File.Exists(_configPath);
        }

        public ZabbixConfig Load()
        {
            if (!ConfigExists())
                return new ZabbixConfig();

            try
            {
                var json = File.ReadAllText(_configPath);

                // wandelt JSON in ZabbixConfig-Objekt um, falls ungültig wird ein neues Objekt zurückgegeben
                var config = JsonSerializer.Deserialize<ZabbixConfig>(json, _jsonOptions) ?? new ZabbixConfig();

                // Migration des bisherigen Standardtexts. Eigene konfigurierte Nachrichten bleiben erhalten.
                // Den normalisierten Wert direkt speichern, damit der Legacy-Default nicht bei jedem Start
                // erneut aus der bestehenden config.json geladen wird.
                var resolvedAcknowledgeMessage =
                    ZabbixConfig.ResolveAcknowledgeMessage(config.AcknowledgeMessage);

                var changed = false;

                if (!string.Equals(
                        config.AcknowledgeMessage,
                        resolvedAcknowledgeMessage,
                        StringComparison.Ordinal))
                {
                    config.AcknowledgeMessage = resolvedAcknowledgeMessage;
                    changed = true;
                }

                changed |= MigrateApiEndpoint(config);
                changed |= MigrateAppName(config);

                if (changed)
                    Save(config);

                return config;
            }
            catch
            {
                // Unlesbare config.json sichern, damit sie beim nächsten Speichern nicht unbemerkt mit Defaults überschrieben wird
                BackupCorruptConfig();
                return new ZabbixConfig();
            }
        }

        // Früher konfigurierbarer API-Pfad: ein Unterverzeichnis (zB. "/zabbix/api_jsonrpc.php") wird an die ZabbixUrl angehängt,
        // da der Pfad jetzt fest ZabbixConfig.ZabbixApiPath ist
        private static bool MigrateApiEndpoint(ZabbixConfig config)
        {
            if (config.ZabbixApiEndpoint == null)
                return false;

            var endpoint = config.ZabbixApiEndpoint.Trim();

            if (!endpoint.StartsWith("/"))
                endpoint = "/" + endpoint;

            if (endpoint.EndsWith(ZabbixConfig.ZabbixApiPath, StringComparison.OrdinalIgnoreCase))
            {
                var subPath = endpoint.Substring(0, endpoint.Length - ZabbixConfig.ZabbixApiPath.Length).TrimEnd('/');
                var zabbixUrl = config.ZabbixUrl.Trim().TrimEnd('/');

                if (subPath.Length > 0 &&
                    zabbixUrl.Length > 0 &&
                    !zabbixUrl.EndsWith(subPath, StringComparison.OrdinalIgnoreCase))
                {
                    config.ZabbixUrl = zabbixUrl + subPath;
                }
            }

            config.ZabbixApiEndpoint = null;
            return true;
        }

        // Früher konfigurierbarer AppName war Teil des Credential-Targets ({AppName}.{Suffix}),
        // Token deshalb einmalig auf das feste Target ZabbixConfig.CredentialAppName umziehen
        private static bool MigrateAppName(ZabbixConfig config)
        {
            if (config.AppName == null)
                return false;

            var oldAppName = config.AppName.Trim();

            if (oldAppName.Length > 0 &&
                !string.Equals(oldAppName, ZabbixConfig.CredentialAppName, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var suffix = string.IsNullOrWhiteSpace(config.CredentialTargetSuffix) ? "ApiToken" : config.CredentialTargetSuffix;

                    CredentialService.SaveOrUpdateCredential(
                        CredentialService.BuildTarget(oldAppName, suffix),
                        CredentialService.BuildTarget(ZabbixConfig.CredentialAppName, suffix),
                        config.CredentialUsername,
                        null);
                }
                catch
                {
                    // AppName behalten, damit der Umzug beim nächsten Laden erneut versucht wird
                    return false;
                }
            }

            config.AppName = null;
            return true;
        }

        public void Save(ZabbixConfig config)
        {
            Directory.CreateDirectory(_appFolder);

            var json = JsonSerializer.Serialize(config, _jsonOptions);

            // Erst in temporäre Datei schreiben und dann ersetzen, damit bei Absturz/vollem Datenträger keine halb geschriebene config.json entsteht
            var tempPath = _configPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _configPath, overwrite: true);
        }

        // Load() wird sehr oft aufgerufen (jeder Refresh/Tooltip), deshalb fester Dateiname und nicht überschreiben -> nur eine Sicherung
        private void BackupCorruptConfig()
        {
            try
            {
                var backupPath = Path.Combine(_appFolder, CorruptConfigBackupFilename);

                if (!File.Exists(backupPath))
                    File.Copy(_configPath, backupPath);
            }
            catch { }
        }

        public string GetConfigPath()
        {
            return _configPath;
        }
    }
}
