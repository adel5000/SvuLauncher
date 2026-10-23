using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SvuLauncher.App.Resources;
using SvuLauncher.Core;

namespace SvuLauncher.App.ViewModels;

/// <summary>Microphone control plus the per-user preferences (mute on join, language, theme).</summary>
public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IMicrophoneService? _microphone;
    private readonly IUserPreferencesStore _store;
    private readonly Action<string> _applyTheme;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private readonly string _startupLanguage;
    private UserPreferences _preferences;
    private bool _syncing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMicrophoneLive), nameof(IsMicrophoneMuted), nameof(HasNoMicrophone))]
    private bool _hasMicrophone;

    [ObservableProperty]
    private string _microphoneName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMicrophoneLive), nameof(IsMicrophoneMuted))]
    private bool _isMicrophoneOn;

    [ObservableProperty]
    private double _microphoneLevel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMicrophoneError))]
    private string? _microphoneError;

    public bool IsMicrophoneLive => HasMicrophone && IsMicrophoneOn;

    public bool IsMicrophoneMuted => HasMicrophone && !IsMicrophoneOn;

    public bool HasNoMicrophone => !HasMicrophone;

    public bool HasMicrophoneError => MicrophoneError is not null;

    [ObservableProperty]
    private bool _muteMicrophoneOnLaunch;

    [ObservableProperty]
    private Choice<string>? _selectedLanguage;

    [ObservableProperty]
    private Choice<string>? _selectedTheme;

    [ObservableProperty]
    private bool _restartRequired;

    public SettingsViewModel(IMicrophoneService? microphone, IUserPreferencesStore store, Action<string> applyTheme)
    {
        _microphone = microphone;
        _store = store;
        _applyTheme = applyTheme;
        _preferences = store.Load();
        _startupLanguage = _preferences.UiLanguage ?? "";

        Languages = [new("", Strings.Language_System), new("en", Strings.Language_English), new("ar", Strings.Language_Arabic)];
        Themes = [new("System", Strings.Theme_System), new("Light", Strings.Theme_Light), new("Dark", Strings.Theme_Dark)];

        _syncing = true;
        MuteMicrophoneOnLaunch = _preferences.MuteMicrophoneOnLaunch;
        SelectedLanguage = Languages.FirstOrDefault(l => l.Value == _startupLanguage) ?? Languages[0];
        SelectedTheme = Themes.FirstOrDefault(t => t.Value == _preferences.Theme) ?? Themes[0];
        _syncing = false;

        if (_microphone is not null)
            _microphone.Changed += OnMicrophoneChanged;
        RefreshMicrophone();
    }

    public IReadOnlyList<Choice<string>> Languages { get; }

    public IReadOnlyList<Choice<string>> Themes { get; }

    public void RefreshMicrophone()
    {
        MicrophoneState? state = null;
        try
        {
            state = _microphone?.GetState();
        }
        catch (Exception ex)
        {
            MicrophoneError = Strings.Format(Strings.Settings_MicrophoneError, ex.Message);
        }

        _syncing = true;
        HasMicrophone = state is not null;
        MicrophoneName = state?.DeviceName ?? "";
        IsMicrophoneOn = state is { IsMuted: false };
        MicrophoneLevel = Math.Round((state?.Level ?? 0) * 100);
        _syncing = false;
    }

    [RelayCommand]
    private void ToggleMicrophone() => IsMicrophoneOn = !IsMicrophoneOn;

    partial void OnIsMicrophoneOnChanged(bool value)
    {
        if (!_syncing)
            ChangeMicrophone(m => m.SetMuted(!value));
    }

    partial void OnMicrophoneLevelChanged(double value)
    {
        if (!_syncing)
            ChangeMicrophone(m => m.SetLevel((float)(value / 100)));
    }

    partial void OnMuteMicrophoneOnLaunchChanged(bool value)
    {
        if (!_syncing)
            Save(_preferences with { MuteMicrophoneOnLaunch = value });
    }

    partial void OnSelectedLanguageChanged(Choice<string>? value)
    {
        if (_syncing || value is null)
            return;
        Save(_preferences with { UiLanguage = value.Value.Length == 0 ? null : value.Value });
        RestartRequired = value.Value != _startupLanguage;
    }

    partial void OnSelectedThemeChanged(Choice<string>? value)
    {
        if (_syncing || value is null)
            return;
        Save(_preferences with { Theme = value.Value });
        _applyTheme(value.Value);
    }

    private void ChangeMicrophone(Action<IMicrophoneService> change)
    {
        if (_microphone is null)
            return;
        try
        {
            change(_microphone);
            MicrophoneError = null;
        }
        catch (Exception ex)
        {
            MicrophoneError = Strings.Format(Strings.Settings_MicrophoneError, ex.Message);
            RefreshMicrophone();
        }
    }

    // Core Audio raises this on its own threads; state is re-read on the UI thread.
    private void OnMicrophoneChanged(object? sender, EventArgs e)
    {
        if (_ui is null || SynchronizationContext.Current == _ui)
            RefreshMicrophone();
        else
            _ui.Post(_ => RefreshMicrophone(), null);
    }

    private void Save(UserPreferences preferences)
    {
        _preferences = preferences;
        _store.Save(preferences);
    }

    public void Dispose()
    {
        if (_microphone is not null)
            _microphone.Changed -= OnMicrophoneChanged;
    }
}
