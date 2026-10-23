using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using SvuLauncher.App.Resources;
using SvuLauncher.App.ViewModels;
using SvuLauncher.Core;
using SvuLauncher.Infrastructure;
using SvuLauncher.Infrastructure.Audio;
using Wpf.Ui.Appearance;

namespace SvuLauncher.App;

public partial class App : Application
{
    private HttpClient? _http;
    private ILoggerFactory? _loggerFactory;
    private CoreAudioMicrophoneService? _microphone;
    private SettingsViewModel? _settings;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        var preferencesStore = new UserPreferencesStore(UserPreferencesStore.DefaultPath);
        var preferences = preferencesStore.Load();

        LauncherSettings settings;
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "svulauncher.json");
        try
        {
            settings = LauncherSettings.Load(settingsPath);
        }
        catch (Exception ex)
        {
            ApplyUiLanguage(preferences.UiLanguage);
            ShowError(Strings.Format(Strings.Startup_SettingsError, settingsPath, ex.Message));
            Shutdown(1);
            return;
        }

        // The user's choice wins over the machine-wide default.
        ApplyUiLanguage(preferences.UiLanguage ?? settings.UiLanguage);

        _loggerFactory = LoggerFactory.Create(b => b.AddDebug().SetMinimumLevel(LogLevel.Information));
        // Some getSessionsByProgCourse queries were observed to take over 40 s.
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        _microphone = CreateMicrophone(_loggerFactory.CreateLogger<App>());
        var services = LauncherServices.Create(settings, _http, _loggerFactory, _microphone);

        var window = new MainWindow
        {
            FlowDirection = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight,
        };
        _settings = new SettingsViewModel(_microphone, preferencesStore, theme => ApplyTheme(window, theme));
        window.DataContext = new ShellViewModel(
            services.Orchestrator, services.Directory, services.Vault, services.Preferences, _settings);

        MainWindow = window;
        ApplyTheme(window, preferences.Theme);
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _settings?.Dispose();
        _microphone?.Dispose();
        _http?.Dispose();
        _loggerFactory?.Dispose();
        base.OnExit(e);
    }

    private static CoreAudioMicrophoneService? CreateMicrophone(ILogger log)
    {
        try
        {
            return new CoreAudioMicrophoneService();
        }
        catch (Exception ex)
        {
            // Audio service unavailable (e.g. a locked-down session): the launcher still works.
            log.LogWarning(ex, "Microphone control is unavailable");
            return null;
        }
    }

    private static void ApplyTheme(Window window, string theme)
    {
        if (theme is "Light" or "Dark")
        {
            // WPF-UI throws when unwatching a window that has not loaded; at startup nothing is watched yet.
            if (window.IsLoaded)
                SystemThemeWatcher.UnWatch(window);
            ApplicationThemeManager.Apply(theme == "Dark" ? ApplicationTheme.Dark : ApplicationTheme.Light);
        }
        else
        {
            ApplicationThemeManager.ApplySystemTheme();
            SystemThemeWatcher.Watch(window);
        }
    }

    /// <summary>"ar" or "en" forces a language; empty or null follows the Windows display language.</summary>
    private static void ApplyUiLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return;
        try
        {
            var culture = CultureInfo.GetCultureInfo(language);
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
        catch (CultureNotFoundException)
        {
            // Unknown codes fall back to the Windows display language.
        }
    }

    private static void ShowError(string message)
    {
        var rtl = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
        MessageBox.Show(message, Strings.AppTitle, MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK,
            rtl ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : MessageBoxOptions.None);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowError(e.Exception.Message);
        e.Handled = true;
    }
}
