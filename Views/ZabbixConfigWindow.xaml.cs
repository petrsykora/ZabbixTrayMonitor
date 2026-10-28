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
                var endpointText = ZabbixApiEndpointTextBox.Text.Trim();

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

                if (string.IsNullOrWhiteSpace(endpointText))
                {
                    MessageBox.Show(
                        Strings.Settings_EnterApiPath,
                        Strings.Common_Error,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

                    return;
                }

                var endpoint = NormalizeZabbixApiEndpoint(endpointText);
                var version = await _zabbixClient.GetVersionAsync(url, endpoint, IgnoreCertificateErrorsCheckBox.IsChecked == true);

                MessageBox.Show(
                    string.Format(Strings.Settings_TestConnectionSuccess, version),
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

            var appName = NormalizeAppName(config.AppName);
            var credentialSuffix = NormalizeCredentialSuffix(config.CredentialTargetSuffix);
            var credentialUsername = NormalizeCredentialUsername(config.CredentialUsername);

            this.Title = string.Format(Strings.Settings_Title, appName);
            HeaderTitle.Text = this.Title;
            AppNameTextBoxHeader.Text = appName;

            // Appname on the fly ändern
            AppNameTextBoxHeader.TextChanged += AppNameTextBoxHeader_TextChanged;

            IgnoreCertificateErrorsCheckBox.IsChecked = config.IgnoreCertificateErrors;
            DarkModeCheckBox.IsChecked = config.UseDarkMode;
            SetSelectedLanguage(config.Language);
            PollIntervalSecondsTextBox.Text = config.PollIntervalSeconds.ToString();
            TrayToolTipDelayMillisecondsTextBox.Text = config.TrayToolTipDelayMilliseconds.ToString();

            if (!string.IsNullOrWhiteSpace(config.ZabbixUrl))
                ZabbixUrlTextBox.Text = config.ZabbixUrl;

            ZabbixApiEndpointTextBox.Text = NormalizeZabbixApiEndpoint(config.ZabbixApiEndpoint);

            if (!string.IsNullOrWhiteSpace(config.ZabbixDashboardUrl))
                ZabbixDashboardUrlTextBox.Text = config.ZabbixDashboardUrl;

            // Schwellenwerte und Farben 
            WarningSeverityTextBox.Text = config.WarningSeverityThreshold.ToString();
            ErrorSeverityTextBox.Text = config.ErrorSeverityThreshold.ToString();
            StatusColorErrorTextBox.Text = config.StatusColorError;
            StatusColorWarningTextBox.Text = config.StatusColorWarning;
            StatusColorInfoTextBox.Text = config.StatusColorInfo;

            // Alarmbearbeitung
            ShowSuppressedProblemsCheckBox.IsChecked = config.ShowSuppressedProblems;
            RefreshAfterProblemActionCheckBox.IsChecked = config.RefreshAfterProblemAction;
            AcknowledgeMessageTextBox.Text =
                ZabbixConfig.ResolveAcknowledgeMessage(config.AcknowledgeMessage);

            // Credential UI
            CredentialTargetPrefixTextBox.Text = appName + ".";
            CredentialTargetSuffixTextBox.Text = credentialSuffix;
            CredentialUsernameTextBox.Text = credentialUsername;

            // Wenn fürs Target bereits ein Token existiert zeige Platzhalter und setze Username
            var target = CredentialService.BuildTarget(appName, credentialSuffix);

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

        // Fenster verschieben
        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                {
                    this.DragMove();
                }
            }
            catch { }
        }

        private void AppNameTextBoxHeader_TextChanged(object? sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var appName = NormalizeAppName(AppNameTextBoxHeader.Text);

            CredentialTargetPrefixTextBox.Text = appName + ".";
            this.Title = string.Format(Strings.Settings_Title, appName);
            HeaderTitle.Text = this.Title;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var existingConfig = _configService.Load(); // lade bestehende Config und aktualisiere nur geänderte Felder

            var previousAppName = NormalizeAppName(existingConfig.AppName);
            var previousSuffix = NormalizeCredentialSuffix(existingConfig.CredentialTargetSuffix);
            var fullTargetOld = CredentialService.BuildTarget(previousAppName, previousSuffix);

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

            var zabbixApiEndpoint = NormalizeZabbixApiEndpoint(ZabbixApiEndpointTextBox.Text);
            var dashboardUrl = ZabbixDashboardUrlTextBox.Text.Trim().TrimEnd('/');

            if (string.IsNullOrWhiteSpace(dashboardUrl))
            {
                dashboardUrl = zabbixUrl; // Wenn keine Dashboard-URL angegeben ist auf die Zabbix-URL setzen
            }

            var appName = NormalizeAppName(AppNameTextBoxHeader.Text);
            var credentialSuffix = NormalizeCredentialSuffix(CredentialTargetSuffixTextBox.Text);
            var credentialUsername = NormalizeCredentialUsername(CredentialUsernameTextBox.Text);
            var fullTargetNew = CredentialService.BuildTarget(appName, credentialSuffix);

            if (!ValidateApiTokenForSave(fullTargetOld, fullTargetNew))
                return;

            // Update Werte in akueller Config
            existingConfig.AppName = appName;
            existingConfig.ZabbixUrl = zabbixUrl;
            existingConfig.ZabbixApiEndpoint = zabbixApiEndpoint;
            existingConfig.ZabbixDashboardUrl = dashboardUrl;
            existingConfig.PollIntervalSeconds = pollIntervalSeconds;
            existingConfig.TrayToolTipDelayMilliseconds = trayToolTipDelayMilliseconds;
            existingConfig.IgnoreCertificateErrors = ignoreCertificateErrors;
            existingConfig.UseDarkMode = DarkModeCheckBox.IsChecked == true;
            existingConfig.Language = GetSelectedLanguage();
            existingConfig.WarningSeverityThreshold = warningThreshold;
            existingConfig.ErrorSeverityThreshold = errorThreshold;
            existingConfig.CredentialTargetSuffix = credentialSuffix;
            existingConfig.CredentialUsername = credentialUsername;
            existingConfig.StatusColorError = NormalizeHexColor(StatusColorErrorTextBox.Text, "#FF0015");
            existingConfig.StatusColorWarning = NormalizeHexColor(StatusColorWarningTextBox.Text, "#F3C601");
            existingConfig.StatusColorInfo = NormalizeHexColor(StatusColorInfoTextBox.Text, "#808080");
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
            if (!ValidateRequiredTextBox(AppNameTextBoxHeader, Strings.Settings_EnterAppName))
                return false;

            if (!ValidateRequiredTextBox(ZabbixUrlTextBox, Strings.Settings_EnterUrl))
                return false;

            if (!ValidateRequiredTextBox(ZabbixApiEndpointTextBox, Strings.Settings_EnterApiPath))
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

        private void SetSelectedLanguage(string language)
        {
            switch (LocalizationService.NormalizeLanguage(language))
            {
                case "en":
                    LanguageEnglishRadioButton.IsChecked = true;
                    break;
                case "de":
                    LanguageGermanRadioButton.IsChecked = true;
                    break;
                case "cs":
                    LanguageCzechRadioButton.IsChecked = true;
                    break;
                default:
                    LanguageSystemRadioButton.IsChecked = true;
                    break;
            }
        }

        private string GetSelectedLanguage()
        {
            if (LanguageEnglishRadioButton.IsChecked == true)
                return "en";

            if (LanguageGermanRadioButton.IsChecked == true)
                return "de";

            if (LanguageCzechRadioButton.IsChecked == true)
                return "cs";

            return LocalizationService.SystemLanguage;
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

        private static string NormalizeAppName(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "ZabbixTrayMonitor"
                : value.Trim();
        }

        private static string NormalizeZabbixApiEndpoint(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "/api_jsonrpc.php";

            value = value.Trim();

            return value.StartsWith("/") ? value : "/" + value;
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

        private static string NormalizeHexColor(string value, string fallback)
        {
            var color = value.Trim();

            if (!IsValidHexColor(color))
                return fallback;

            return color;
        }

        private static bool IsValidHexColor(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (value.Length != 7)
                return false;

            if (value[0] != '#')
                return false;

            for (var i = 1; i < value.Length; i++)
            {
                if (!Uri.IsHexDigit(value[i]))
                    return false;
            }

            return true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}