using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using ZabbixTrayMonitor.Services;
using ZabbixTrayMonitor.Models;
using ZabbixTrayMonitor.Resources;
using ZabbixTrayMonitor.Views;

namespace ZabbixTrayMonitor
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DateTime _lastTrayLeftClick = DateTime.MinValue;
        private readonly ZabbixClient _zabbixClient = new();
        private readonly ConfigService _configService = new();
        private readonly DispatcherTimer _refreshTimer = new();
        private readonly DispatcherTimer _trayToolTipDelayTimer = new();
        private readonly DispatcherTimer _updateCheckTimer = new();
        private List<ZabbixProblem> _currentProblems = new();
        private UpdateInfo? _availableUpdate;

        // Zeit und Ergebnis der letzten Update-Prüfung für das Info-Fenster (null = noch keine Prüfung)
        public string? LastUpdateCheckStatus { get; private set; }
        private bool _isCheckingForUpdates = false;
        private bool _isInstallingUpdate = false;

        // Erste Update-Prüfung kurz nach dem Start (nicht gleichzeitig mit dem ersten Abruf der Probleme), danach alle 6 Stunden
        private static readonly TimeSpan FirstUpdateCheckDelay = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(6);
        private ProblemsWindow? _problemsWindow;
        private string? _lastRefreshError;
        private bool _isRefreshing = false;
        private bool _trayToolTipOpenRequested = false;

        public MainWindow()
        {
            InitializeComponent();

            Hide(); // Mainframe ausblenden

            // Der Custom-Tooltip wird direkt vom TaskbarIcon gesteuert. Dadurch stammen
            // Öffnen und Schließen vom tatsächlichen Windows-Tray-Icon statt aus einer
            // eigenen Cursor-Abstandslogik.
            ApplyLocalizedTexts();
            UpdateTrayToolTip($"{ZabbixConfig.AppDisplayName}\n---------\n{Strings.Tray_Initializing}");

            // RefreshTimer für Probleme initialisieren abhängig vom Pollintervall in den Einstellungen
            _refreshTimer.Tick += async (_, _) => await RefreshProblemsAsync();

            // Der Windows-Tray meldet zuverlässig, wann der Mauszeiger das Icon betritt bzw. verlässt.
            // Die eigentliche Anzeige des Custom-Tooltips verzögern wir zusätzlich konfigurierbar.
            _trayToolTipDelayTimer.Tick += TrayToolTipDelayTimer_Tick;

            if (!_configService.ConfigExists())
            {
                var configWindow = new ZabbixConfigWindow();
                configWindow.ShowDialog();

                // Beim ersten Start kann in den Einstellungen bereits eine Sprache gewählt worden sein
                ApplyLocalizedTexts();
            }

            Title = ZabbixConfig.AppDisplayName;

            // RefreshTimer starten
            StartRefreshTimer();
            _ = RefreshProblemsAsync();

            // Update-Prüfung: nur Hinweis per Sprechblase und Menüeintrag, Klick öffnet das Update-Fenster
            TrayIcon.TrayBalloonTipClicked += (_, _) => ShowUpdateWindow();

            _updateCheckTimer.Tick += async (_, _) =>
            {
                _updateCheckTimer.Interval = UpdateCheckInterval;
                await CheckForUpdatesAsync(interactive: false);
            };
            _updateCheckTimer.Interval = FirstUpdateCheckDelay;
            _updateCheckTimer.Start();
        }

        // interactive = vom Benutzer ausgelöst (Info-Fenster): Ergebnis und Fehler immer anzeigen, auch wenn die
        // automatische Prüfung abgeschaltet oder die Version übersprungen ist
        public async Task CheckForUpdatesAsync(bool interactive)
        {
            if (_isCheckingForUpdates)
                return;

            var config = _configService.Load();

            if (!interactive && (!config.CheckForUpdates || UpdateService.IsDisabledByPolicy()))
            {
                LastUpdateCheckStatus = string.Format(Strings.Update_StatusDisabled, DateTime.Now);
                return;
            }

            _isCheckingForUpdates = true;

            try
            {
                var update = await UpdateService.GetNewerReleaseAsync();

                _availableUpdate = update;
                UpdateTrayUpdateMenuItem();

                LastUpdateCheckStatus = update == null
                    ? string.Format(Strings.Update_StatusUpToDate, DateTime.Now)
                    : string.Format(Strings.Update_StatusAvailable, DateTime.Now, UpdateService.FormatVersion(update.Version));

                if (update == null)
                {
                    if (interactive)
                    {
                        MessageBox.Show(
                            string.Format(Strings.Update_UpToDate, UpdateService.FormatVersion(UpdateService.CurrentVersion)),
                            string.Format(Strings.Update_Title, ZabbixConfig.AppDisplayName),
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }

                    return;
                }

                if (interactive)
                {
                    ShowUpdateWindow();
                    return;
                }

                if (IsSkipped(update))
                    return;

                // Automatische Installation nur für die installierte Anwendung und nicht bei Richtlinie DisableUpdateCheck
                // (dann läuft die automatische Prüfung ohnehin nicht)
                if (config.InstallUpdatesAutomatically && UpdateInstaller.CanInstall)
                {
                    await InstallUpdateAsync(update, interactive: false);
                    return;
                }

                TrayIcon.ShowBalloonTip(
                    Strings.Update_BalloonTitle,
                    string.Format(Strings.Update_BalloonText, ZabbixConfig.AppDisplayName, UpdateService.FormatVersion(update.Version)),
                    BalloonIcon.Info);
            }
            catch (Exception ex)
            {
                LastUpdateCheckStatus = string.Format(Strings.Update_StatusFailed, DateTime.Now, ex.Message);

                // Automatische Prüfung still (Ergebnis nur im Info-Fenster), zB. ohne Internet oder bei gesperrtem GitHub
                if (interactive)
                {
                    MessageBox.Show(
                        string.Format(Strings.Update_CheckFailed, ex.Message),
                        string.Format(Strings.Update_Title, ZabbixConfig.AppDisplayName),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            finally
            {
                _isCheckingForUpdates = false;
            }
        }

        // Lädt den Installer herunter, prüft Prüfsumme und Signatur und startet die Installation (Rückfrage der
        // Benutzerkontensteuerung). Der Installer beendet die Anwendung und startet danach die neue Version.
        // interactive = über das Update-Fenster ausgelöst: Fehler als Meldung, sonst als Sprechblase
        public async Task<bool> InstallUpdateAsync(UpdateInfo update, bool interactive)
        {
            if (_isInstallingUpdate)
                return false;

            _isInstallingUpdate = true;
            var version = UpdateService.FormatVersion(update.Version);

            try
            {
                LastUpdateCheckStatus = string.Format(Strings.Update_StatusDownloading, DateTime.Now, version);

                var installerPath = await UpdateInstaller.DownloadAndVerifyAsync(update);

                if (!interactive)
                {
                    TrayIcon.ShowBalloonTip(
                        Strings.Update_BalloonTitle,
                        string.Format(Strings.Update_BalloonInstalling, ZabbixConfig.AppDisplayName, version),
                        BalloonIcon.Info);
                }

                UpdateInstaller.StartInstall(installerPath);

                LastUpdateCheckStatus = string.Format(Strings.Update_StatusInstalling, DateTime.Now, version);
                return true;
            }
            catch (Exception ex)
            {
                LastUpdateCheckStatus = string.Format(Strings.Update_StatusInstallFailed, DateTime.Now, version, ex.Message);

                if (interactive)
                {
                    MessageBox.Show(
                        string.Format(Strings.Update_InstallFailed, ex.Message),
                        string.Format(Strings.Update_Title, ZabbixConfig.AppDisplayName),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else
                {
                    // Klick auf die Sprechblase öffnet das Update-Fenster (Herunterladen von Hand)
                    TrayIcon.ShowBalloonTip(
                        Strings.Update_BalloonTitle,
                        string.Format(Strings.Update_BalloonInstallFailed, ZabbixConfig.AppDisplayName, version),
                        BalloonIcon.Warning);
                }

                return false;
            }
            finally
            {
                _isInstallingUpdate = false;
            }
        }

        private bool IsSkipped(UpdateInfo update)
        {
            return string.Equals(_configService.Load().SkippedUpdateVersion, update.TagName, StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateTrayUpdateMenuItem()
        {
            var visible = _availableUpdate != null && !IsSkipped(_availableUpdate);

            if (_availableUpdate != null)
                TrayUpdateMenuItem.Header = string.Format(Strings.Tray_UpdateAvailable, UpdateService.FormatVersion(_availableUpdate.Version));

            TrayUpdateMenuItem.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            TrayUpdateSeparator.Visibility = TrayUpdateMenuItem.Visibility;
        }

        private void ShowUpdateWindow()
        {
            if (_availableUpdate == null)
                return;

            var existing = Application.Current.Windows.OfType<UpdateWindow>().FirstOrDefault();
            if (existing != null)
            {
                existing.Activate();
                return;
            }

            new UpdateWindow(_availableUpdate).ShowDialog();

            // "Diese Version überspringen" blendet den Menüeintrag aus
            UpdateTrayUpdateMenuItem();
        }

        private void TrayIcon_Update_Click(object sender, RoutedEventArgs e)
        {
            HideTrayToolTip();
            ShowUpdateWindow();
        }

        // öffnet Einstellungsfenster und aktualisiert nach Schließen/Speichern die App
        private void TrayIcon_Settings_Click(object sender, RoutedEventArgs e)
        {
            HideTrayToolTip();

            // Wenn bereits ein Einstellungsfenster offen ist, aktiviere es statt ein neues zu öffnen
            var existingConfigWin = Application.Current.Windows.OfType<ZabbixConfigWindow>().FirstOrDefault();
            if (existingConfigWin != null)
            {
                existingConfigWin.Activate();
                return;
            }

            var config = _configService.Load();
            var previousLanguage = LocalizationService.NormalizeLanguage(config.Language);
            var configWindow = new ZabbixConfigWindow();

            try
            {
                ZabbixTrayMonitor.Services.ThemeService.ApplyTheme(config.UseDarkMode);
            }
            catch { }

            var result = configWindow.ShowDialog(); // 

            if (result == true)
            {
                // Sprache wurde in den Einstellungen bereits umgestellt -> Tray-Menü und Problemfenster mit neuen Texten aufbauen
                if (LocalizationService.NormalizeLanguage(_configService.Load().Language) != previousLanguage)
                    OnLanguageChanged();

                // aktualisiert nach Speichern die Probleme sofort und startet Timer neu der Daten im Hintergrund aktualisiert
                StartRefreshTimer();
                _ = RefreshProblemsAsync();

                // Falls ProblemsWindow offen ist, aktualisiere Theme
                try
                {
                    config = _configService.Load();
                    // Theme global anwenden
                    ZabbixTrayMonitor.Services.ThemeService.ApplyTheme(config.UseDarkMode);

                    if (_problemsWindow != null)
                        ZabbixTrayMonitor.Services.ThemeService.ApplyTitleBarTheme(_problemsWindow, config.UseDarkMode);
                }
                catch { }
            }
        }

        // Texte, die nicht bei jedem Öffnen eines Fensters neu geladen werden (MainWindow lebt die ganze Laufzeit)
        private void ApplyLocalizedTexts()
        {
            TrayRefreshMenuItem.Header = Strings.Common_Refresh;
            TrayShowProblemsMenuItem.Header = Strings.Tray_ShowProblems;
            TraySettingsMenuItem.Header = Strings.Tray_Settings;
            TrayHelpMenuItem.Header = Strings.Tray_Help;
            TrayInfoMenuItem.Header = Strings.Tray_Info;
            TrayExitMenuItem.Header = Strings.Tray_Exit;
            UpdateTrayUpdateMenuItem();
        }

        private void OnLanguageChanged()
        {
            ApplyLocalizedTexts();

            // Problemfenster wird beim nächsten Öffnen mit den neuen Texten neu erstellt
            _problemsWindow?.CloseWindow();
            _problemsWindow = null;
        }

        private async void TrayIcon_Refresh_Click(object sender, RoutedEventArgs e)
        {
            HideTrayToolTip();
            await RefreshProblemsAsync();
        }

        private void OpenZabbixDashboard()
        {
            var config = _configService.Load();

            var url = string.IsNullOrWhiteSpace(config.ZabbixDashboardUrl)
                ? config.ZabbixUrl
                : config.ZabbixDashboardUrl;

            if (string.IsNullOrWhiteSpace(url))
                return;

            // URL validieren lässt nur http und https zu
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return;

            Process.Start(new ProcessStartInfo // öffnet die URL mit dem Standardbrowser
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
        }

        private void TrayIcon_RightMouseDown(object sender, RoutedEventArgs e)
        {
            HideTrayToolTip();

            try
            {
                if (TrayIcon.ContextMenu is not null)
                {
                    TrayIcon.ContextMenu.IsOpen = true;
                }
            }
            catch { }
        }

        private async void TrayIcon_ShowProblems_Click(object sender, RoutedEventArgs e)
        {
            HideTrayToolTip();

            // anti spam Klick
            var now = DateTime.Now;
            if ((now - _lastTrayLeftClick).TotalMilliseconds < 800)
                return;

            _lastTrayLeftClick = now;
            await RefreshProblemsAsync();

            // Wenn der letzte Refresh fehlgeschlagen ist, keine alten Problem-Daten anzeigen
            if (!string.IsNullOrWhiteSpace(_lastRefreshError))
            {
                _problemsWindow?.Hide();
                return;
            }

            // Singleton für ProblemsWindow
            // Fenster aktualisiert sich immer mit den aktuellen Daten wenn es geöffnet wird damit keine Cacheprobleme 
            if (_problemsWindow == null)
            {
                _problemsWindow = new ProblemsWindow(
                    _currentProblems,
                    async () =>
                    {
                        await RefreshProblemsAsync();
                        return _currentProblems;
                    },
                    () => OpenZabbixDashboard(),
                    async (eventId, suppressUntil, message) =>
                    {
                        await ExecuteProblemActionAsync(eventId, suppressUntil, message);
                    },
                    _configService
                );

                _problemsWindow.PlaceNearTray();
            }

            // Sicherstellen, dass das ProblemsWindow beim Öffnen aktuelle Daten zeigt wegen Cacheproblemen
            try
            {
                _problemsWindow.UpdateProblems(_currentProblems);
            }
            catch { }

            // Klick auf das Tray-Icon holt das Fenster immer nach vorne (geschlossen wird es über die Titelleiste),
            // ein minimiertes Fenster wird wiederhergestellt
            if (_problemsWindow.WindowState == WindowState.Minimized)
                _problemsWindow.WindowState = WindowState.Normal;

            _problemsWindow.Show();
            _problemsWindow.Activate();
        }

        private void StartRefreshTimer()
        {
            var config = _configService.Load();

            _refreshTimer.Stop();
            _refreshTimer.Interval = TimeSpan.FromSeconds(config.PollIntervalSeconds);
            _refreshTimer.Start();
        }

        private async Task ExecuteProblemActionAsync(string eventId, DateTime? suppressUntil, string? messageOverride)
        {
            var config = _configService.Load();
            var credSuffix = string.IsNullOrWhiteSpace(config.CredentialTargetSuffix)
                ? "ApiToken"
                : config.CredentialTargetSuffix;

            var token = CredentialService.GetTokenForApp(ZabbixConfig.CredentialAppName, credSuffix);

            if (string.IsNullOrWhiteSpace(config.ZabbixUrl))
                throw new Exception(Strings.Error_NoZabbixUrl);

            if (string.IsNullOrWhiteSpace(token))
                throw new Exception(Strings.Error_NoApiToken);

            var defaultMessage =
                ZabbixConfig.ResolveAcknowledgeMessage(config.AcknowledgeMessage);

            var message = string.IsNullOrWhiteSpace(messageOverride)
                ? defaultMessage
                : messageOverride.Trim();

            await _zabbixClient.AcknowledgeProblemAsync(
                config.ZabbixUrl,
                token,
                config.IgnoreCertificateErrors,
                eventId,
                suppressUntil,
                message
            );
        }

        private async Task RefreshProblemsAsync()
        {
            if (_isRefreshing)
                return;

            _isRefreshing = true;

            // Loading Indikator
            try
            {
                if (_problemsWindow?.IsVisible == true)
                {
                    _problemsWindow.SetLoading(true);
                }
            }
            catch { }

            try
            {
                var config = _configService.Load();
                var credSuffix = string.IsNullOrWhiteSpace(config.CredentialTargetSuffix) ? "ApiToken" : config.CredentialTargetSuffix;
                var token = CredentialService.GetTokenForApp(ZabbixConfig.CredentialAppName, credSuffix);

                if (string.IsNullOrWhiteSpace(config.ZabbixUrl))
                    throw new Exception(Strings.Error_NoZabbixUrl);

                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new Exception(Strings.Error_NoApiToken);
                }

                var problems = await _zabbixClient.GetProblemsAsync(
                    config.ZabbixUrl,
                    token,
                    config.WarningSeverityThreshold,
                    config.IgnoreCertificateErrors
                );

                // Unterdrückte Probleme zentral filtern, damit Tray-Status, Tooltip und
                // Problemfenster immer mit exakt derselben Problemmenge arbeiten.
                // Bestätigte, aber nicht unterdrückte Probleme bleiben weiterhin sichtbar.
                var visibleProblems = config.ShowSuppressedProblems
                    ? problems
                    : problems.Where(p => !p.Suppressed).ToList();

                _currentProblems = visibleProblems;
                _lastRefreshError = null;
                var lastUpdated = DateTime.Now;

                // zähle alle sichtbaren Probleme mit einer Severity >= unserem Error Schwellenwert
                var errorCount = visibleProblems.Count(p => p.Severity >= config.ErrorSeverityThreshold);

                // zähle alle sichtbaren Probleme die mindestens unserem Warnungs Schwellenwert entsprechen aber noch kein Error sind
                var warningCount = visibleProblems.Count(p =>
                    p.Severity >= config.WarningSeverityThreshold &&
                    p.Severity < config.ErrorSeverityThreshold
                );

                // Tray-Icon abhängig von Problemen setzen
                if (errorCount > 0)
                    SetTrayIcon("tray-error.ico");
                else if (warningCount > 0)
                    SetTrayIcon("tray-warning.ico");
                else
                    SetTrayIcon("tray-ok.ico");

                var fullText = BuildTrayToolTipText(
                    config,
                    visibleProblems,
                    errorCount,
                    warningCount,
                    lastUpdated
                );

                UpdateTrayToolTip(fullText);

                // Wenn das ProblemsWindow sichtbar ist Inhalt aktualisieren
                try
                {
                    if (_problemsWindow?.IsVisible == true)
                    {
                        _problemsWindow.UpdateProblems(_currentProblems);
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                // Jeder Fehler beim Abrufen/Verarbeiten der Zabbix-Daten wird als Fehlerstatus angezeigt
                try
                {
                    SetTrayIcon("tray-error.ico");
                }
                catch { }

                _lastRefreshError = ex.Message;
                _currentProblems = new List<ZabbixProblem>();

                // Wenn ein API-/Netzwerkfehler passiert, keine alten Probleme mehr anzeigen
                try
                {
                    if (_problemsWindow?.IsVisible == true)
                    {
                        _problemsWindow.UpdateProblems(_currentProblems);
                    }
                }
                catch { }

                var lastUpdated = DateTime.Now;
                var cfg = _configService.Load();
                var lines = new List<string>
                {
                    ZabbixConfig.AppDisplayName,
                    "---------",
                    string.Format(Strings.Tooltip_ErrorLine, ex.Message),
                    "---------",
                    string.Format(Strings.Tooltip_Server, cfg.ZabbixUrl),
                    string.Format(Strings.Tooltip_Updated, lastUpdated)
                };

                UpdateTrayToolTip(string.Join(Environment.NewLine, lines));
            }
            finally // sicherstellen dass der Loading Indikator wieder weg ist auch wenn Fehler auftreten
            {
                _isRefreshing = false;

                try
                {
                    if (_problemsWindow?.IsVisible == true)
                    {
                        _problemsWindow.SetLoading(false);
                    }
                }
                catch { }
            }
        }

        private string BuildTrayToolTipText(
            ZabbixConfig config,
            List<ZabbixProblem> problems,
            int errorCount,
            int warningCount,
            DateTime lastUpdated)
        {
            // Tooltip bewusst kompakt halten:
            // nur Host + wichtigste Meldung anzeigen, keine Uhrzeit pro Item und keine Detailzeile
            var lines = new List<string>
    {
        ZabbixConfig.AppDisplayName,
        "---------"
    };

            if (errorCount == 0 && warningCount == 0)
            {
                lines.Add(Strings.Tooltip_NoProblems);
                lines.Add("---------");
                lines.Add(string.Format(Strings.Tooltip_Server, config.ZabbixUrl));
                lines.Add(string.Format(Strings.Tooltip_Updated, lastUpdated));
                return string.Join(Environment.NewLine, lines);
            }

            var errorProblems = problems
                .Where(p => p.Severity >= config.ErrorSeverityThreshold)
                .OrderByDescending(p => p.Severity)
                .ThenByDescending(p => p.Time)
                .ToList();

            var warningProblems = problems
                .Where(p =>
                    p.Severity >= config.WarningSeverityThreshold &&
                    p.Severity < config.ErrorSeverityThreshold)
                .OrderByDescending(p => p.Severity)
                .ThenByDescending(p => p.Time)
                .ToList();

            var errorItems = ProblemsMapper.BuildViewModels(errorProblems, config);
            var warningItems = ProblemsMapper.BuildViewModels(warningProblems, config);

            if (errorCount > 0 && warningCount > 0)
            {
                AddProblemSection(lines, Strings.Tooltip_Errors, errorCount, errorItems, 5);

                lines.Add("---------");

                AddProblemSection(lines, Strings.Tooltip_Warnings, warningCount, warningItems, 5);
            }
            else if (errorCount > 0)
            {
                AddProblemSection(lines, Strings.Tooltip_Errors, errorCount, errorItems, 10);
            }
            else if (warningCount > 0)
            {
                AddProblemSection(lines, Strings.Tooltip_Warnings, warningCount, warningItems, 10);
            }

            lines.Add("---------");
            lines.Add(string.Format(Strings.Tooltip_Server, config.ZabbixUrl));
            lines.Add(string.Format(Strings.Tooltip_Updated, lastUpdated));

            return string.Join(Environment.NewLine, lines);
        }

        private static void AddProblemSection(
            List<string> lines,
            string title,
            int count,
            List<ProblemListItem> problems,
            int maxVisibleItems)
        {
            lines.Add($"{title}: {count}");

            foreach (var problem in problems.Take(maxVisibleItems))
            {
                var host = string.IsNullOrWhiteSpace(problem.Host)
                    ? "Zabbix"
                    : problem.Host;

                var message = string.IsNullOrWhiteSpace(problem.Name)
                    ? Strings.Common_NoText
                    : problem.Name;

                host = CleanToolTipLine(host);
                message = CleanToolTipLine(message);

                lines.Add($"- {host}: {message}");
            }

            if (problems.Count > maxVisibleItems)
            {
                lines.Add("- [...]");
            }
        }

        private static string CleanToolTipLine(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            return value.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private void SetTrayIcon(string iconFileName)
        {
            TrayIcon.IconSource = new BitmapImage(
                new Uri($"pack://application:,,,/Assets/{iconFileName}")
            );
        }

        private void TrayIcon_Exit_Click(object sender, RoutedEventArgs e)
        {
            HideTrayToolTip();
            TrayIcon.Dispose();
            Application.Current.Shutdown();
        }

        private void TrayIcon_PreviewTrayToolTipOpen(object sender, RoutedEventArgs e)
        {
            // Während das Problemfenster aktiv ist, keinen zusätzlichen Tray-Tooltip öffnen.
            if (_problemsWindow?.IsActive == true)
            {
                e.Handled = true;
                return;
            }

            var config = _configService.Load();
            var delayMilliseconds = Math.Max(0, config.TrayToolTipDelayMilliseconds);

            // 0 ms bedeutet: Hardcodet darf den Tooltip wie gewohnt sofort öffnen.
            if (delayMilliseconds == 0)
                return;

            // Das automatische Öffnen zunächst unterdrücken. Sobald Windows meldet, dass
            // die Maus das Tray-Icon wieder verlassen hat, wird der Timer abgebrochen.
            e.Handled = true;
            _trayToolTipOpenRequested = true;

            _trayToolTipDelayTimer.Stop();
            _trayToolTipDelayTimer.Interval = TimeSpan.FromMilliseconds(delayMilliseconds);
            _trayToolTipDelayTimer.Start();
        }

        private void TrayIcon_PreviewTrayToolTipClose(object sender, RoutedEventArgs e)
        {
            // Native Close-Meldung nicht blockieren: Sie schließt auch einen von uns
            // nach Ablauf der Verzögerung geöffneten Custom-Tooltip zuverlässig.
            _trayToolTipOpenRequested = false;
            _trayToolTipDelayTimer.Stop();
        }

        private void TrayToolTipDelayTimer_Tick(object? sender, EventArgs e)
        {
            _trayToolTipDelayTimer.Stop();

            if (!_trayToolTipOpenRequested || _problemsWindow?.IsActive == true)
                return;

            try
            {
                if (TrayIcon.ContextMenu?.IsOpen == true)
                    return;

                if (TrayIcon.TrayToolTipResolved is not null)
                {
                    TrayIcon.TrayToolTipResolved.IsOpen = true;
                }
            }
            catch { }
        }

        private void UpdateTrayToolTip(string fullText)
        {
            try
            {
                var tooltipText = string.IsNullOrWhiteSpace(fullText)
                    ? ZabbixConfig.AppDisplayName
                    : fullText;

                TrayToolTipTextBlock.Text = tooltipText;

                // Fallback für Systeme, auf denen kein Custom-Tooltip verfügbar ist.
                // Beim gesetzten TrayToolTip zeigt Hardcodet auf aktuellen Windows-Versionen
                // weiterhin den WPF-Custom-Tooltip an.
                var firstLine = tooltipText
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                    .FirstOrDefault();

                TrayIcon.ToolTipText = string.IsNullOrWhiteSpace(firstLine)
                    ? ZabbixConfig.AppDisplayName
                    : firstLine;
            }
            catch { }
        }

        private void HideTrayToolTip()
        {
            _trayToolTipOpenRequested = false;
            _trayToolTipDelayTimer.Stop();

            try
            {
                if (TrayIcon.TrayToolTipResolved is not null)
                {
                    TrayIcon.TrayToolTipResolved.IsOpen = false;
                }
            }
            catch { }
        }

        private void TrayIcon_Help_Click(object sender, RoutedEventArgs e)
        {
            HideTrayToolTip();
            HelpWindow.ShowHelp();
        }

        private void TrayIcon_Info_Click(object sender, RoutedEventArgs e)
        {
            HideTrayToolTip();

            // Wenn Info- oder Einstellungsfenster bereits offen sind, bringe es nach vorne
            var existingInfoWin = Application.Current.Windows.OfType<Views.InfoWindow>().FirstOrDefault();
            if (existingInfoWin != null)
            {
                existingInfoWin.Activate();
                return;
            }

            var existingConfigWin = Application.Current.Windows.OfType<ZabbixConfigWindow>().FirstOrDefault();
            if (existingConfigWin != null)
            {
                existingConfigWin.Activate();
                return;
            }

            try
            {
                var infoWindow = new Views.InfoWindow();
                infoWindow.ShowDialog();
            }
            catch
            {
                MessageBox.Show(string.Format(Strings.Info_Version, Strings.Common_Unknown), Strings.Tray_Info, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}