using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SvuLauncher.App.Resources;
using SvuLauncher.Core;

namespace SvuLauncher.App.ViewModels;

public sealed partial class LoginViewModel : ObservableObject
{
    private readonly LaunchOrchestrator _orchestrator;
    private readonly ICredentialVault _vault;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    private string _userName = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    private string _password = "";

    [ObservableProperty]
    private bool _rememberMe;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    public bool HasError => Error is not null;

    public LoginViewModel(LaunchOrchestrator orchestrator, ICredentialVault vault)
    {
        _orchestrator = orchestrator;
        _vault = vault;
        if (vault.TryLoad() is { } saved)
        {
            UserName = saved.UserName;
            Password = saved.Password;
            RememberMe = true;
        }
    }

    public event Action<UserIdentity, Credentials>? SignedIn;

    private bool CanSignIn() => !string.IsNullOrWhiteSpace(UserName) && Password.Length > 0;

    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private async Task SignInAsync(CancellationToken ct)
    {
        Error = null;
        var credentials = new Credentials(UserName.Trim(), Password);
        try
        {
            var user = await _orchestrator.SignInAsync(credentials, ct);
            if (RememberMe)
                _vault.Save(credentials);
            else
                _vault.Clear();
            SignedIn?.Invoke(user, credentials);
        }
        catch (UnauthorizedAccessException)
        {
            Error = Strings.Login_ErrorRejected;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Includes HttpClient timeouts, which surface as TaskCanceledException.
            Error = Strings.Format(Strings.Login_ErrorUnreachable, ex.Message);
        }
    }
}
