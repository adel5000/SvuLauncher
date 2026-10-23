using CommunityToolkit.Mvvm.ComponentModel;
using SvuLauncher.Core;

namespace SvuLauncher.App.ViewModels;

/// <summary>Switches between the sign-in page and the sessions page.</summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly LaunchOrchestrator _orchestrator;
    private readonly ISessionCatalog _catalog;
    private readonly ICredentialVault _vault;
    private readonly LaunchPreferences _preferences;

    [ObservableProperty]
    private ObservableObject? _currentPage;

    public ShellViewModel(
        LaunchOrchestrator orchestrator, ISessionCatalog catalog, ICredentialVault vault,
        LaunchPreferences preferences, SettingsViewModel settings)
    {
        _orchestrator = orchestrator;
        _catalog = catalog;
        _vault = vault;
        _preferences = preferences;
        Settings = settings;
        ShowLogin();
    }

    public SettingsViewModel Settings { get; }

    public bool HasRunningSession => CurrentPage is SessionsViewModel { IsSessionRunning: true };

    public ValueTask EndSessionAsync() =>
        CurrentPage is SessionsViewModel sessions ? sessions.EndSessionAsync() : ValueTask.CompletedTask;

    private void ShowLogin()
    {
        var login = new LoginViewModel(_orchestrator, _vault);
        login.SignedIn += ShowSessions;
        CurrentPage = login;
    }

    private void ShowSessions(UserIdentity user, Credentials credentials)
    {
        var sessions = new SessionsViewModel(_orchestrator, _catalog, _preferences, Settings, user, credentials);
        sessions.SignedOut += ShowLogin;
        CurrentPage = sessions;
        _ = sessions.LoadAsync();
    }
}
