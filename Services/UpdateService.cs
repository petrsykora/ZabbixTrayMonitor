using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using ZabbixTrayMonitor.Models;
using ZabbixTrayMonitor.Resources;

// Prüft auf GitHub, ob eine neuere Version veröffentlicht wurde.
// Nur Hinweis mit Link auf die Release-Seite, es wird nichts heruntergeladen oder installiert.

namespace ZabbixTrayMonitor.Services
{
    public record UpdateInfo(Version Version, string TagName, string ReleaseUrl);

    public static class UpdateService
    {
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/petrsykora/ZabbixTrayMonitor/releases/latest";

        // Richtlinie für verwaltete Computer (GPO/Intune): DisableUpdateCheck = 1 schaltet die automatische Prüfung ab
        private const string PolicyKeyPath = @"Software\Policies\ZabbixTrayMonitor";
        private const string PolicyDisableUpdateCheck = "DisableUpdateCheck";

        private static readonly HttpClient _client = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient(new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            })
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            // GitHub API verlangt einen User-Agent
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ZabbixTrayMonitor", FormatVersion(CurrentVersion)));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            return client;
        }

        public static Version CurrentVersion => Normalize(Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0));

        public static string FormatVersion(Version version)
        {
            return version.Revision > 0 ? version.ToString(4) : version.ToString(3);
        }

        public static bool IsDisabledByPolicy()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(PolicyKeyPath);
                return key?.GetValue(PolicyDisableUpdateCheck) is int value && value != 0;
            }
            catch
            {
                return false;
            }
        }

        // Liefert das neueste Release, wenn es neuer als die laufende Version ist, sonst null
        public static async Task<UpdateInfo?> GetNewerReleaseAsync()
        {
            using var response = await _client.GetAsync(LatestReleaseApiUrl);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var tagName = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
            var releaseUrl = root.TryGetProperty("html_url", out var urlElement) ? urlElement.GetString() : null;

            if (string.IsNullOrWhiteSpace(tagName) || !TryParseVersion(tagName, out var latestVersion))
                throw new Exception(Strings.Api_InvalidResponse);

            // Nur Links auf die Release-Seiten dieses Projekts öffnen
            if (string.IsNullOrWhiteSpace(releaseUrl) ||
                !releaseUrl.StartsWith(AppInfo.ProjectUrl + "/releases/", StringComparison.OrdinalIgnoreCase))
            {
                releaseUrl = AppInfo.ProjectUrl + "/releases/latest";
            }

            return latestVersion > CurrentVersion
                ? new UpdateInfo(latestVersion, tagName, releaseUrl)
                : null;
        }

        // "v1.3.5" -> 1.3.5.0
        private static bool TryParseVersion(string tagName, out Version version)
        {
            var value = tagName.Trim().TrimStart('v', 'V');

            if (Version.TryParse(value, out var parsed))
            {
                version = Normalize(parsed);
                return true;
            }

            version = new Version(0, 0);
            return false;
        }

        // Fehlende Teile als 0, damit 1.3.5 und 1.3.5.0 gleich verglichen werden
        private static Version Normalize(Version version)
        {
            return new Version(
                Math.Max(version.Major, 0),
                Math.Max(version.Minor, 0),
                Math.Max(version.Build, 0),
                Math.Max(version.Revision, 0));
        }
    }
}
