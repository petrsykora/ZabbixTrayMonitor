using System;
using System.Windows;
using System.Windows.Controls;
using ZabbixTrayMonitor.Models;
using ZabbixTrayMonitor.Resources;
using ZabbixTrayMonitor.Services;

// Einstellungs-WPF-Fenster

namespace ZabbixTrayMonitor
{
    /// <summary>
    /// Interaktionslogik für ZabbixConfigWindow.xaml
    /// </summary>
    public partial class ZabbixConfigWindow : Window
    {
        private readonly ConfigService _configService = new();
        private readonly ZabbixClient _zabbixClient = new();
        private const string TokenPlaceholder = "***********************";

        // Severity-Namen wie in Zabbix, deshalb nicht übersetzt
        private const string SeverityLegend =
            "0 = Not classified\n" +
            "1 = Information\n" +
            "2 = Warning\n" +
            "3 = Average\n" +
            "4 = High\n" +
            "5 = Disaster";

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var url = ZabbixUrlTextBox.Text.Trim().TrimEnd('/'); //sicherstellen dass bei der URl keine doppelten Slahes sind

                if (string.IsNullOrWhiteSpace(url))
                {
                    MessageBox.Show(
                        Strings.Settings_EnterUrlFirst,
                        Strings.Common_Error,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

                    return;
                }

                var ignoreCertificateErrors = IgnoreCertificateErrorsCheckBox.IsChecked == true;

                var version = await _zabbixClient.GetVersionAsync(url, ignoreCertificateErrors);

                // apiinfo.version funktioniert ohne Token, deshalb den Token zusätzlich mit einer authentifizierten Abfrage prüfen
                var token = GetTokenForTest();

                if (string.IsNullOrWhiteSpace(token))
                {
                    MessageBox.Show(
                        string.Format(Strings.Settings_TestConnectionNoToken, version),
                        Strings.Settings_TestConnectionCaption,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );

                    return;
                }

                string problemCount;

                try
                {
                    problemCount = await _zabbixClient.CheckApiTokenAsync(url, token, ignoreCertificateErrors);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        string.Format(Strings.Settings_TestConnectionTokenFailed, version, ex.Message),
                        Strings.Settings_TestConnectionCaption,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

                    return;
                }

                MessageBox.Show(
                    string.Format(Strings.Settings_TestConnectionSuccess, version, problemCount),
                    Strings.Settings_TestConnectionCaption,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(Strings.Settings_TestConnectionFailed, ex.Message),
                    Strings.Settings_TestConnectionCaption,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        public ZabbixConfigWindow()
        {
            InitializeComponent();

            var config = _configService.Load();

            ZabbixTrayMonitor.Services.ThemeService.ApplyTheme(config.UseDarkMode);

            var credentialSuffix = NormalizeCredentialSuffix(config.CredentialTargetSuffix);
            var credentialUsername = NormalizeCredentialUsername(config.CredentialUsername);

            this.Title = string.Format(Strings.Settings_Title, ZabbixConfig.AppDisplayName);

            // Fenster passt sich dem Inhalt an, aber nie höher als der Arbeitsbereich (Rest per ScrollViewer)
            MaxHeight = SystemParameters.WorkArea.Height;

            // Systemtitelleiste an Dark/Light Mode anpassen, Handle gibt es erst ab SourceInitialized
            SourceInitialized += (_, _) => ThemeService.ApplyTitleBarTheme(this, config.UseDarkMode);

            IgnoreCertificateErrorsCheckBox.IsChecked = config.IgnoreCertificateErrors;
            DarkModeCheckBox.IsChecked = config.UseDarkMode;
            InitializeLanguageComboBox(config.Language);
            PollIntervalSecondsTextBox.Text = config.PollIntervalSeconds.ToString();
            TrayToolTipDelayMillisecondsTextBox.Text = config.TrayToolTipDelayMilliseconds.ToString();

            if (!string.IsNullOrWhiteSpace(config.ZabbixUrl))
                ZabbixUrlTextBox.Text = config.ZabbixUrl;

            if (!string.IsNullOrWhiteSpace(config.ZabbixDashboardUrl))
                ZabbixDashboardUrlTextBox.Text = config.ZabbixDashboardUrl;

            // Schwellenwerte
            WarningSeverityTextBox.Text = config.WarningSeverityThreshold.ToString();
            ErrorSeverityTextBox.Text = config.ErrorSeverityThreshold.ToString();

            // Alarmbearbeitung
            ShowSuppressedProblemsCheckBox.IsChecked = config.ShowSuppressedProblems;
            RefreshAfterProblemActionCheckBox.IsChecked = config.RefreshAfterProblemAction;
            AcknowledgeMessageTextBox.Text =
                ZabbixConfig.ResolveAcknowledgeMessage(config.AcknowledgeMessage);

            // Credential UI
            CredentialTargetPrefixTextBox.Text = ZabbixConfig.CredentialAppName + ".";
            CredentialTargetSuffixTextBox.Text = credentialSuffix;
            CredentialUsernameTextBox.Text = credentialUsername;

            // Wenn fürs Target bereits ein Token existiert zeige Platzhalter und setze Username
            var target = CredentialService.BuildTarget(ZabbixConfig.CredentialAppName, credentialSuffix);

            try
            {
                if (CredentialService.HasToken(target))
                {
                    ApiTokenTextBox.Password = TokenPlaceholder;

                    var usernameFromCredentialManager = CredentialService.GetUsername(target);

                    if (!string.IsNullOrWhiteSpace(usernameFromCredentialManager))
                        CredentialUsernameTextBox.Text = usernameFromCredentialManager;
                }
            }
            catch { }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var existingConfig = _configService.Load(); // lade bestehende Config und aktualisiere nur geänderte Felder

            var previousSuffix = NormalizeCredentialSuffix(existingConfig.CredentialTargetSuffix);
            var fullTargetOld = CredentialService.BuildTarget(ZabbixConfig.CredentialAppName, previousSuffix);

            if (!ValidateRequiredFields())
                return;

            if (!TryReadPollInterval(out var pollIntervalSeconds))
                return;

            if (!TryReadTrayToolTipDelay(out var trayToolTipDelayMilliseconds))
                return;

            if (!TryReadSeverityThresholds(out var warningThreshold, out var errorThreshold))
                return;

            var zabbixUrl = ZabbixUrlTextBox.Text.Trim().TrimEnd('/');

            if (!ValidateZabbixUrlSecurity(existingConfig.ZabbixUrl, zabbixUrl))
                return;

            var ignoreCertificateErrors = IgnoreCertificateErrorsCheckBox.IsChecked == true;

            if (!ConfirmIgnoreCertificateErrors(existingConfig.IgnoreCertificateErrors, ignoreCertificateErrors))
                return;

            var dashboardUrl = ZabbixDashboardUrlTextBox.Text.Trim().TrimEnd('/');

            if (string.IsNullOrWhiteSpace(dashboardUrl))
            {
                dashboardUrl = zabbixUrl; // Wenn keine Dashboard-URL angegeben ist auf die Zabbix-URL setzen
            }

            var credentialSuffix = NormalizeCredentialSuffix(CredentialTargetSuffixTextBox.Text);
            var credentialUsername = NormalizeCredentialUsername(CredentialUsernameTextBox.Text);
            var fullTargetNew = CredentialService.BuildTarget(ZabbixConfig.CredentialAppName, credentialSuffix);

            if (!ValidateApiTokenForSave(fullTargetOld, fullTargetNew))
                return;

            // Update Werte in akueller Config
            existingConfig.ZabbixUrl = zabbixUrl;
            existingConfig.ZabbixDashboardUrl = dashboardUrl;
            existingConfig.PollIntervalSeconds = pollIntervalSeconds;
            existingConfig.TrayToolTipDelayMilliseconds = trayToolTipDelayMilliseconds;
            existingConfig.IgnoreCertificateErrors = ignoreCertificateErrors;
            existingConfig.UseDarkMode = DarkModeCheckBox.IsChecked == true;
            existingConfig.Language = LanguageComboBox.SelectedValue as string ?? LocalizationService.DefaultLanguage;
            existingConfig.WarningSeverityThreshold = warningThreshold;
            existingConfig.ErrorSeverityThreshold = errorThreshold;
            existingConfig.CredentialTargetSuffix = credentialSuffix;
            existingConfig.CredentialUsername = credentialUsername;
            existingConfig.ShowSuppressedProblems = ShowSuppressedProblemsCheckBox.IsChecked == true;
            existingConfig.RefreshAfterProblemAction = RefreshAfterProblemActionCheckBox.IsChecked == true;
            existingConfig.AcknowledgeMessage =
                ZabbixConfig.ResolveAcknowledgeMessage(AcknowledgeMessageTextBox.Text);

            if (!SaveCredentialChanges(fullTargetOld, fullTargetNew, credentialUsername))
                return;

            _configService.Save(existingConfig);

            ZabbixTrayMonitor.Services.ThemeService.ApplyTheme(existingConfig.UseDarkMode);
            LocalizationService.ApplyLanguage(existingConfig.Language);

            DialogResult = true;
            Close();
        }

        private bool ValidateRequiredFields()
        {
            if (!ValidateRequiredTextBox(ZabbixUrlTextBox, Strings.Settings_EnterUrl))
                return false;

            if (!ValidateRequiredPasswordBox(ApiTokenTextBox, Strings.Settings_EnterApiToken))
                return false;

            if (!ValidateRequiredTextBox(CredentialTargetSuffixTextBox, Strings.Settings_EnterCredentialSuffix))
                return false;

            if (!ValidateRequiredTextBox(CredentialUsernameTextBox, Strings.Settings_EnterCredentialUsername))
                return false;

            return true;
        }

        // Der API Token wird bei jedem Request als Bearer-Header mitgeschickt, deshalb nur http/https zulassen und bei http warnen
        // (nur wenn die URL geändert wurde, nicht bei jedem Speichern einer bereits bestätigten http-URL)
        private bool ValidateZabbixUrlSecurity(string previousZabbixUrl, string zabbixUrl)
        {
            if (!Uri.TryCreate(zabbixUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                ShowValidationError(Strings.Settings_UrlInvalid);
                ZabbixUrlTextBox.Focus();
                ZabbixUrlTextBox.SelectAll();
                return false;
            }

            if (uri.Scheme == Uri.UriSchemeHttps ||
                string.Equals(previousZabbixUrl?.Trim().TrimEnd('/'), zabbixUrl, StringComparison.OrdinalIgnoreCase))
                return true;

            var result = MessageBox.Show(
                Strings.Settings_HttpWarning,
                Strings.Settings_HttpWarningCaption,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No
            );

            if (result == MessageBoxResult.Yes)
                return true;

            ZabbixUrlTextBox.Focus();
            return false;
        }

        // Nur beim Einschalten nachfragen, nicht bei jedem Speichern einer bereits so gespeicherten Konfiguration
        private bool ConfirmIgnoreCertificateErrors(bool wasIgnoringCertificateErrors, bool ignoreCertificateErrors)
        {
            if (!ignoreCertificateErrors || wasIgnoringCertificateErrors)
                return true;

            var result = MessageBox.Show(
                Strings.Settings_CertificateWarning,
                Strings.Settings_CertificateWarningCaption,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No
            );

            if (result == MessageBoxResult.Yes)
                return true;

            IgnoreCertificateErrorsCheckBox.Focus();
            return false;
        }

        private bool ValidateApiTokenForSave(string fullTargetOld, string fullTargetNew)
        {
            var token = ApiTokenTextBox.Password.Trim();

            if (string.IsNullOrWhiteSpace(token))
            {
                ShowValidationError(Strings.Settings_EnterApiToken);
                ApiTokenTextBox.Focus();
                return false;
            }

            // Platzhalter ist nur gültig, wenn wirklich ein Token im Credential Manager existiert
            if (token == TokenPlaceholder)
            {
                try
                {
                    if (CredentialService.HasToken(fullTargetOld) || CredentialService.HasToken(fullTargetNew))
                        return true;
                }
                catch { }

                MessageBox.Show(
                    Strings.Settings_NoTokenInCredentialManager,
                    Strings.Common_Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                ApiTokenTextBox.Clear();
                ApiTokenTextBox.Focus();

                return false;
            }

            return true;
        }

        private bool SaveCredentialChanges(string fullTargetOld, string fullTargetNew, string credentialUsername)
        {
            try
            {
                var token = ApiTokenTextBox.Password.Trim();

                // Token unverändert: vorhandene Credentials übernehmen und evtl. Target/Username aktualisieren
                if (token == TokenPlaceholder)
                {
                    var updated = CredentialService.SaveOrUpdateCredential(
                        fullTargetOld,
                        fullTargetNew,
                        credentialUsername,
                        null
                    );

                    if (!updated)
                    {
                        MessageBox.Show(
                            Strings.Settings_ExistingTokenNotFound,
                            Strings.Common_Error,
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );

                        ApiTokenTextBox.Clear();
                        ApiTokenTextBox.Focus();

                        return false;
                    }

                    return true;
                }

                if (string.IsNullOrWhiteSpace(token))
                {
                    ShowValidationError(Strings.Settings_EnterApiToken);
                    ApiTokenTextBox.Focus();
                    return false;
                }

                // Neuer Token: neue Credentias speichern und altes Target löschen
                CredentialService.SaveOrUpdateCredential(
                    fullTargetOld,
                    fullTargetNew,
                    credentialUsername,
                    token
                );

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(Strings.Settings_CredentialsSaveFailed, ex.Message),
                    Strings.Common_Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                return false;
            }
        }

        private bool TryReadPollInterval(out int pollIntervalSeconds)
        {
            if (!int.TryParse(PollIntervalSecondsTextBox.Text.Trim(), out pollIntervalSeconds))
            {
                MessageBox.Show(
                    Strings.Settings_PollIntervalInvalid,
                    Strings.Common_Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                PollIntervalSecondsTextBox.Focus();
                PollIntervalSecondsTextBox.SelectAll();

                return false;
            }

            if (pollIntervalSeconds < 10)
            {
                MessageBox.Show(
                    Strings.Settings_PollIntervalTooShort,
                    Strings.Common_Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                PollIntervalSecondsTextBox.Focus();
                PollIntervalSecondsTextBox.SelectAll();

                return false;
            }

            return true;
        }

        private bool TryReadTrayToolTipDelay(out int delayMilliseconds)
        {
            if (!int.TryParse(TrayToolTipDelayMillisecondsTextBox.Text.Trim(), out delayMilliseconds))
            {
                MessageBox.Show(
                    Strings.Settings_TooltipDelayInvalid,
                    Strings.Common_Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                TrayToolTipDelayMillisecondsTextBox.Focus();
                TrayToolTipDelayMillisecondsTextBox.SelectAll();

                return false;
            }

            if (delayMilliseconds < 0 || delayMilliseconds > 10000)
            {
                MessageBox.Show(
                    Strings.Settings_TooltipDelayRange,
                    Strings.Common_Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                TrayToolTipDelayMillisecondsTextBox.Focus();
                TrayToolTipDelayMillisecondsTextBox.SelectAll();

                return false;
            }

            return true;
        }

        private bool TryReadSeverityThresholds(out int warningThreshold, out int errorThreshold)
        {
            warningThreshold = 0;
            errorThreshold = 0;

            if (!int.TryParse(WarningSeverityTextBox.Text.Trim(), out warningThreshold))
            {
                MessageBox.Show(
                    Strings.Settings_WarningThresholdInvalid,
                    Strings.Common_Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                WarningSeverityTextBox.Focus();
                WarningSeverityTextBox.SelectAll();

                return false;
            }

            if (warningThreshold < 0 || warningThreshold > 5)
            {
                MessageBox.Show(
                    Strings.Settings_WarningThresholdRange + "\n\n" + SeverityLegend,
                    Strings.Common_Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                WarningSeverityTextBox.Focus();
                WarningSeverityTextBox.SelectAll();

                return false;
            }

            if (!int.TryParse(ErrorSeverityTextBox.Text.Trim(), out errorThreshold))
            {
                MessageBox.Show(
                    Strings.Settings_ErrorThresholdInvalid,
                    Strings.Common_Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                ErrorSeverityTextBox.Focus();
                ErrorSeverityTextBox.SelectAll();

                return false;
            }

            if (errorThreshold < 0 || errorThreshold > 5)
            {
                MessageBox.Show(
                    Strings.Settings_ErrorThresholdRange + "\n\n" + SeverityLegend,
                    Strings.Common_Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                ErrorSeverityTextBox.Focus();
                ErrorSeverityTextBox.SelectAll();

                return false;
            }

            if (errorThreshold <= warningThreshold)
            {
                MessageBox.Show(
                    Strings.Settings_ErrorThresholdNotGreater,
                    Strings.Common_Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                ErrorSeverityTextBox.Focus();
                ErrorSeverityTextBox.SelectAll();

                return false;
            }

            return true;
        }

        // Sprachnamen bewusst in der jeweiligen Sprache und nicht übersetzt, nur "System" wird übersetzt
        // Einträge als ComboBoxItem (Text = Name, Tag = Sprachcode), damit nur der Name angezeigt wird
        private void InitializeLanguageComboBox(string language)
        {
            LanguageComboBox.Items.Add(new ComboBoxItem { Content = Strings.Settings_LanguageSystem, Tag = LocalizationService.SystemLanguage });
            LanguageComboBox.Items.Add(new ComboBoxItem { Content = "English", Tag = "en" });
            LanguageComboBox.Items.Add(new ComboBoxItem { Content = "Deutsch", Tag = "de" });
            LanguageComboBox.Items.Add(new ComboBoxItem { Content = "Čeština", Tag = "cs" });

            LanguageComboBox.SelectedValue = LocalizationService.NormalizeLanguage(language);
        }

        private static bool ValidateRequiredTextBox(TextBox textBox, string errorMessage)
        {
            if (!string.IsNullOrWhiteSpace(textBox.Text))
                return true;

            ShowValidationError(errorMessage);

            textBox.Focus();
            textBox.SelectAll();

            return false;
        }

        private static bool ValidateRequiredPasswordBox(PasswordBox passwordBox, string errorMessage)
        {
            if (!string.IsNullOrWhiteSpace(passwordBox.Password))
                return true;

            ShowValidationError(errorMessage);

            passwordBox.Focus();

            return false;
        }

        private static void ShowValidationError(string errorMessage)
        {
            MessageBox.Show(
                errorMessage,
                Strings.Common_Error,
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }

        // Token für den Verbindungstest: neu eingegebener Token, sonst (Platzhalter) der gespeicherte aus dem Credential Manager
        private string? GetTokenForTest()
        {
            var token = ApiTokenTextBox.Password.Trim();

            if (token != TokenPlaceholder)
                return token;

            try
            {
                var suffixes = new[]
                {
                    NormalizeCredentialSuffix(CredentialTargetSuffixTextBox.Text),
                    NormalizeCredentialSuffix(_configService.Load().CredentialTargetSuffix)
                };

                foreach (var suffix in suffixes)
                {
                    var storedToken = CredentialService.GetToken(
                        CredentialService.BuildTarget(ZabbixConfig.CredentialAppName, suffix));

                    if (!string.IsNullOrWhiteSpace(storedToken))
                        return storedToken;
                }
            }
            catch { }

            return null;
        }

        private static string NormalizeCredentialSuffix(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "ApiToken"
                : value.Trim();
        }

        private static string NormalizeCredentialUsername(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? CredentialService.DefaultCredentialUsername
                : value.Trim();
        }

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            Views.HelpWindow.ShowHelp();
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.F1)
                return;

            e.Handled = true;
            Views.HelpWindow.ShowHelp();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}