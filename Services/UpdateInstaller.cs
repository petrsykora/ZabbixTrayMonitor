using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using ZabbixTrayMonitor.Resources;

// Lädt den Installer einer neuen Version herunter, prüft ihn und startet die Installation.
// Geprüft werden die SHA-256-Prüfsumme aus der GitHub API und die Authenticode-Signatur: die Datei muss unverändert und
// mit einem der Zertifikate in TrustedSignerThumbprints signiert sein. Das Zertifikat ist selbst signiert und auf vielen
// Computern nicht vertrauenswürdig, deshalb zählt der Fingerabdruck und nicht das Vertrauen von Windows.

namespace ZabbixTrayMonitor.Services
{
    public static class UpdateInstaller
    {
        // SHA-1-Fingerabdrücke der Code-Signing-Zertifikate der Releases.
        // Vor dem Wechsel des Zertifikats (das aktuelle gilt bis 29.10.2031) den neuen Fingerabdruck ergänzen und ein Release
        // mit beiden veröffentlichen, sonst können installierte Versionen die neuen Releases nicht mehr automatisch installieren.
        private static readonly string[] TrustedSignerThumbprints =
        {
            "614063AB301CBEFAE04741432C6AD0D27E48B8A1"
        };

        private const string DownloadUrlPrefix = "https://github.com/petrsykora/ZabbixTrayMonitor/releases/download/";

        private const string AutostartKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AutostartValueName = "ZabbixTrayMonitor";

        private static readonly HttpClient _client = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient(new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            })
            {
                // Der Installer mit .NET Runtime ist rund 60 MB groß
                Timeout = TimeSpan.FromMinutes(10)
            };

            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ZabbixTrayMonitor", UpdateService.FormatVersion(UpdateService.CurrentVersion)));

            return client;
        }

        private static string UpdatesFolder => Path.Combine(Path.GetTempPath(), "ZabbixTrayMonitor", "Updates");

        // Nur die mit MSI/setup.exe nach "C:\Program Files\ZabbixTrayMonitor" installierte Anwendung wird aktualisiert,
        // die portable ZIP-Version nicht
        public static bool CanInstall
        {
            get
            {
                try
                {
                    var installFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ZabbixTrayMonitor");
                    var appFolder = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);

                    return string.Equals(appFolder, installFolder, StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            }
        }

        // Variante mit .NET Runtime -> MSI mit Runtime, sonst setup.exe (installiert das MSI ohne Runtime und hält einen mit
        // setup.exe installierten Eintrag unter "Apps" aktuell)
        private static bool IsSelfContained => File.Exists(Path.Combine(AppContext.BaseDirectory, "coreclr.dll"));

        private static string InstallerName(UpdateInfo update) => IsSelfContained
            ? $"ZabbixTrayMonitor-{update.TagName}-win-x64.msi"
            : $"ZabbixTrayMonitor-{update.TagName}-win-x64-setup.exe";

        public static async Task<string> DownloadAndVerifyAsync(UpdateInfo update)
        {
            var name = InstallerName(update);
            var asset = update.Assets.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

            if (asset == null || !asset.DownloadUrl.StartsWith(DownloadUrlPrefix, StringComparison.OrdinalIgnoreCase))
                throw new Exception(string.Format(Strings.Update_InstallerNotFound, name));

            if (string.IsNullOrWhiteSpace(asset.Sha256))
                throw new Exception(string.Format(Strings.Update_ChecksumMissing, name));

            // Alte Downloads entfernen
            try
            {
                if (Directory.Exists(UpdatesFolder))
                    Directory.Delete(UpdatesFolder, recursive: true);
            }
            catch { }

            Directory.CreateDirectory(UpdatesFolder);
            var path = Path.Combine(UpdatesFolder, name);

            using (var response = await _client.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                await using var source = await response.Content.ReadAsStreamAsync();
                await using var target = File.Create(path);
                await source.CopyToAsync(target);
            }

            string sha256;
            await using (var stream = File.OpenRead(path))
            {
                sha256 = Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
            }

            if (!string.Equals(sha256, asset.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(path);
                throw new Exception(string.Format(Strings.Update_ChecksumMismatch, name));
            }

            if (!HasTrustedSignature(path))
            {
                TryDelete(path);
                throw new Exception(string.Format(Strings.Update_SignatureInvalid, name));
            }

            return path;
        }

        // Startet die Installation und danach die neue Version. Das übernimmt ein PowerShell-Skript ohne Administratorrechte:
        // es startet den Installer mit Rückfrage der Benutzerkontensteuerung, wartet auf ihn (der Installer beendet diese
        // Anwendung) und startet die Anwendung danach wieder als normaler Benutzer.
        // Wird die Rückfrage abgelehnt, läuft die Anwendung einfach weiter.
        public static void StartInstall(string installerPath)
        {
            var script = BuildInstallScript(installerPath);

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand " +
                            Convert.ToBase64String(Encoding.Unicode.GetBytes(script)),
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }

        private static string BuildInstallScript(string installerPath)
        {
            var appPath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "ZabbixTrayMonitor.exe");
            var logPath = Path.Combine(UpdatesFolder, "install.log");

            string fileName;
            string arguments;

            if (installerPath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
            {
                // Autostart wie bisher beibehalten (die MSI-Eigenschaft AUTOSTART ist sonst wieder 1)
                fileName = "msiexec.exe";
                arguments = $"/i \"{installerPath}\" /passive /norestart AUTOSTART={(IsAutostartInstalled() ? 1 : 0)} /l*v \"{logPath}\"";
            }
            else
            {
                fileName = installerPath;
                arguments = $"/passive /norestart /log \"{logPath}\"";
            }

            return $@"
$ErrorActionPreference = 'Stop'
try {{
    $installer = Start-Process -FilePath {Quote(fileName)} -ArgumentList {Quote(arguments)} -Verb RunAs -Wait -PassThru
}} catch {{
    exit 1
}}
$session = (Get-Process -Id $PID).SessionId
$running = Get-Process -Name 'ZabbixTrayMonitor' -ErrorAction SilentlyContinue | Where-Object {{ $_.SessionId -eq $session }}
if (-not $running) {{ Start-Process -FilePath {Quote(appPath)} }}
exit $installer.ExitCode
";
        }

        // PowerShell-Zeichenkette in einfachen Anführungszeichen
        private static string Quote(string value) => "'" + value.Replace("'", "''") + "'";

        private static bool IsAutostartInstalled()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(AutostartKeyPath);
                return key?.GetValue(AutostartValueName) != null;
            }
            catch
            {
                return true;
            }
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); } catch { }
        }

        // Signatur unverändert (WinVerifyTrust) und vom Zertifikat des Projekts. Ein nicht vertrauenswürdiges
        // Stammzertifikat wird akzeptiert, wenn der Fingerabdruck stimmt, eine veränderte Datei oder fehlende Signatur nicht.
        private static bool HasTrustedSignature(string path)
        {
            var result = WinTrust.Verify(path);

            if (result != WinTrust.Success && result != WinTrust.UntrustedRoot)
                return false;

            try
            {
#pragma warning disable SYSLIB0057 // liefert das Signaturzertifikat einer signierten Datei, ein Ersatz in X509CertificateLoader fehlt
                using var certificate = X509Certificate.CreateFromSignedFile(path);
#pragma warning restore SYSLIB0057
                var thumbprint = certificate.GetCertHashString();

                return TrustedSignerThumbprints.Any(t => string.Equals(t, thumbprint, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private static class WinTrust
        {
            public const int Success = 0;
            public const int UntrustedRoot = unchecked((int)0x800B0109); // CERT_E_UNTRUSTEDROOT

            private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

            private const uint UiNone = 2;
            private const uint RevokeNone = 0;
            private const uint ChoiceFile = 1;
            private const uint StateActionVerify = 1;
            private const uint StateActionClose = 2;
            private const uint RevocationCheckNone = 0x10;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            private struct FileInfo
            {
                public uint cbStruct;
                public string pcwszFilePath;
                public IntPtr hFile;
                public IntPtr pgKnownSubject;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct Data
            {
                public uint cbStruct;
                public IntPtr pPolicyCallbackData;
                public IntPtr pSIPClientData;
                public uint dwUIChoice;
                public uint fdwRevocationChecks;
                public uint dwUnionChoice;
                public IntPtr pFile;
                public uint dwStateAction;
                public IntPtr hWVTStateData;
                public IntPtr pwszURLReference;
                public uint dwProvFlags;
                public uint dwUIContext;
                public IntPtr pSignatureSettings;
            }

            [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
            private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid pgActionID, ref Data pWVTData);

            public static int Verify(string path)
            {
                var fileInfo = new FileInfo
                {
                    cbStruct = (uint)Marshal.SizeOf<FileInfo>(),
                    pcwszFilePath = path
                };

                var fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<FileInfo>());

                try
                {
                    Marshal.StructureToPtr(fileInfo, fileInfoPtr, false);

                    var data = new Data
                    {
                        cbStruct = (uint)Marshal.SizeOf<Data>(),
                        dwUIChoice = UiNone,
                        fdwRevocationChecks = RevokeNone,
                        dwUnionChoice = ChoiceFile,
                        pFile = fileInfoPtr,
                        dwStateAction = StateActionVerify,
                        dwProvFlags = RevocationCheckNone
                    };

                    var action = GenericVerifyV2;
                    var result = WinVerifyTrust(IntPtr.Zero, ref action, ref data);

                    data.dwStateAction = StateActionClose;
                    WinVerifyTrust(IntPtr.Zero, ref action, ref data);

                    return result;
                }
                finally
                {
                    Marshal.DestroyStructure<FileInfo>(fileInfoPtr);
                    Marshal.FreeHGlobal(fileInfoPtr);
                }
            }
        }
    }
}
