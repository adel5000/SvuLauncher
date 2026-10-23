using Microsoft.Extensions.Logging.Abstractions;
using SvuLauncher.App.Resources;
using SvuLauncher.App.ViewModels;
using SvuLauncher.Core;

namespace SvuLauncher.Tests;

public class LoginViewModelTests
{
    [Fact]
    public void Prefills_saved_credentials()
    {
        var vault = new MemoryVault { Stored = new Credentials("alice", "pw") };
        var vm = new LoginViewModel(Fakes.Orchestrator(), vault);
        Assert.Equal("alice", vm.UserName);
        Assert.True(vm.RememberMe);
        Assert.True(vm.SignInCommand.CanExecute(null));
    }

    [Fact]
    public void Requires_user_name_and_password()
    {
        var vm = new LoginViewModel(Fakes.Orchestrator(), new MemoryVault()) { UserName = "alice" };
        Assert.False(vm.SignInCommand.CanExecute(null));
        vm.Password = "pw";
        Assert.True(vm.SignInCommand.CanExecute(null));
    }

    [Fact]
    public async Task Successful_sign_in_saves_only_when_remembered()
    {
        var vault = new MemoryVault { Stored = new Credentials("old", "old") };
        var vm = new LoginViewModel(Fakes.Orchestrator(), vault) { UserName = " bob ", Password = "pw", RememberMe = false };
        UserIdentity? signedIn = null;
        vm.SignedIn += (user, _) => signedIn = user;

        await vm.SignInCommand.ExecuteAsync(null);

        Assert.Equal("bob", signedIn?.UserName);
        Assert.Null(vault.Stored);
    }

    [Fact]
    public async Task Rejected_and_failed_sign_in_show_errors()
    {
        var rejected = new LoginViewModel(Fakes.Orchestrator(new FakeAuth { Success = false }), new MemoryVault()) { UserName = "u", Password = "p" };
        await rejected.SignInCommand.ExecuteAsync(null);
        Assert.Equal(Strings.Login_ErrorRejected, rejected.Error);
        Assert.True(rejected.HasError);

        var offline = new LoginViewModel(Fakes.Orchestrator(new FakeAuth { Failure = new HttpRequestException("no route") }), new MemoryVault()) { UserName = "u", Password = "p" };
        await offline.SignInCommand.ExecuteAsync(null);
        Assert.Contains("no route", offline.Error);
    }
}

public class SessionsViewModelTests
{
    private static SessionsViewModel Create(FakeCatalog catalog, FakeClient? client = null, FakeMicrophone? mic = null) =>
        new(Fakes.Orchestrator(client: client, microphone: mic), catalog, new LaunchPreferences(),
            Fakes.Settings(mic), new UserIdentity("s1", UserRole.Student), new Credentials("s1", "pw"))
        {
            AutoRefreshDelay = TimeSpan.Zero,
        };

    [Fact]
    public async Task Loads_my_classes_with_program_and_course_disabled_by_default()
    {
        var catalog = new FakeCatalog();
        var vm = Create(catalog);

        await vm.LoadAsync();

        Assert.Equal([SessionScope.MyClasses, SessionScope.AllMyCourseClasses, SessionScope.ByProgramAndCourse],
            vm.Scopes.Select(s => s.Value));
        Assert.Equal(new SessionQuery(SessionScope.MyClasses, 0, 0), catalog.LastQuery);
        Assert.False(vm.IsProgramCourseEnabled);
        Assert.Equal([Strings.AllPrograms, "ITE", "82"], vm.Programs.Select(p => p.Code)); // empty code falls back to id
        Assert.Equal([Strings.AllCourses], vm.Courses.Select(c => c.Code));
        Assert.Single(vm.Sessions);
        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public async Task Choosing_a_program_loads_its_courses_and_filters_sessions()
    {
        var catalog = new FakeCatalog();
        var vm = Create(catalog);
        await vm.LoadAsync();

        vm.SelectedScope = vm.Scopes.Single(s => s.Value == SessionScope.ByProgramAndCourse);
        await vm.Pending;
        Assert.True(vm.IsProgramCourseEnabled);

        vm.SelectedProgram = vm.Programs.Single(p => p.Code == "ITE");
        await vm.Pending;
        Assert.False(vm.IsAwaitingProgram);

        Assert.Equal([Strings.AllCourses, "AML601"], vm.Courses.Select(c => c.Code));
        Assert.Equal(new SessionQuery(SessionScope.ByProgramAndCourse, 53, 0), catalog.LastQuery);

        vm.SelectedCourse = vm.Courses[1];
        await vm.Pending;
        Assert.Equal(new SessionQuery(SessionScope.ByProgramAndCourse, 53, 1426), catalog.LastQuery);
    }

    [Theory]
    [InlineData(SessionScope.MyClasses)]
    [InlineData(SessionScope.AllMyCourseClasses)]
    public async Task Other_scopes_disable_and_ignore_program_and_course_but_keep_them(SessionScope scope)
    {
        var catalog = new FakeCatalog();
        var vm = Create(catalog);
        await vm.LoadAsync();
        var byProgram = vm.Scopes.Single(s => s.Value == SessionScope.ByProgramAndCourse);
        vm.SelectedScope = byProgram;
        await vm.Pending;
        vm.SelectedProgram = vm.Programs.Single(p => p.Code == "ITE");
        await vm.Pending;
        vm.SelectedCourse = vm.Courses[1];
        await vm.Pending;

        vm.SelectedScope = vm.Scopes.Single(s => s.Value == scope);
        await vm.Pending;
        Assert.False(vm.IsProgramCourseEnabled);
        Assert.Equal(new SessionQuery(scope, 0, 0), catalog.LastQuery);
        Assert.Equal("AML601", vm.SelectedCourse?.Code);

        vm.SelectedScope = byProgram;
        await vm.Pending;
        Assert.Equal(new SessionQuery(SessionScope.ByProgramAndCourse, 53, 1426), catalog.LastQuery);
    }

    [Fact]
    public async Task By_program_with_all_programs_waits_for_explicit_refresh()
    {
        var catalog = new FakeCatalog();
        var vm = Create(catalog);
        await vm.LoadAsync();
        var calls = catalog.SessionCalls;

        vm.SelectedScope = vm.Scopes.Single(s => s.Value == SessionScope.ByProgramAndCourse);
        await vm.Pending;

        Assert.Equal(calls, catalog.SessionCalls);
        Assert.True(vm.IsAwaitingProgram);
        Assert.Empty(vm.Sessions);
        Assert.False(vm.IsEmpty);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(new SessionQuery(SessionScope.ByProgramAndCourse, 0, 0), catalog.LastQuery);
        Assert.False(vm.IsAwaitingProgram);
        Assert.Single(vm.Sessions);
    }

    [Fact]
    public async Task Rapid_filter_changes_collapse_into_one_load()
    {
        var catalog = new FakeCatalog();
        var vm = Create(catalog);
        await vm.LoadAsync();
        vm.SelectedScope = vm.Scopes.Single(s => s.Value == SessionScope.ByProgramAndCourse);
        await vm.Pending;
        vm.AutoRefreshDelay = TimeSpan.FromMilliseconds(200);
        var calls = catalog.SessionCalls;

        vm.SelectedProgram = vm.Programs.Single(p => p.Code == "ITE");
        var first = vm.Pending;
        await Task.Delay(50); // courses are loaded; the session load is still debounced
        vm.SelectedCourse = vm.Courses.Single(c => c.Code == "AML601");
        await Task.WhenAll(first, vm.Pending);

        Assert.Equal(calls + 1, catalog.SessionCalls);
        Assert.Equal(new SessionQuery(SessionScope.ByProgramAndCourse, 53, 1426), catalog.LastQuery);
    }

    [Fact]
    public async Task Changing_program_while_disabled_loads_courses_without_refreshing_sessions()
    {
        var catalog = new FakeCatalog();
        var vm = Create(catalog);
        await vm.LoadAsync();
        var calls = catalog.SessionCalls;

        vm.SelectedProgram = vm.Programs.Single(p => p.Code == "ITE");
        await vm.Pending;

        Assert.Equal(calls, catalog.SessionCalls);
        Assert.Equal([Strings.AllCourses, "AML601"], vm.Courses.Select(c => c.Code));
    }

    [Fact]
    public async Task Course_load_failure_is_shown_and_sessions_still_load()
    {
        var catalog = new FakeCatalog { CoursesFailure = new HttpRequestException("courses down") };
        var vm = Create(catalog);
        await vm.LoadAsync();
        vm.SelectedScope = vm.Scopes.Single(s => s.Value == SessionScope.ByProgramAndCourse);
        await vm.Pending;

        vm.SelectedProgram = vm.Programs.Single(p => p.Code == "ITE");
        await vm.Pending;

        Assert.Contains("courses down", vm.ErrorMessage);
        Assert.Equal(53, catalog.LastQuery?.ProgramId);
    }

    [Fact]
    public async Task Empty_result_shows_empty_state_and_service_errors_do_not()
    {
        var empty = Create(new FakeCatalog { NoSessions = true });
        await empty.LoadAsync();
        Assert.True(empty.IsEmpty);

        var failing = Create(new FakeCatalog { SessionsFailure = new InvalidDataException("Invalid Option") });
        await failing.LoadAsync();
        Assert.False(failing.IsEmpty);
        Assert.Contains("Invalid Option", failing.ErrorMessage);
    }

    private static SessionInfo Row(string id, string status, string start, string classId = "7") =>
        Fakes.Session with { SessionId = id, Name = "S" + id, Status = status, StartDate = start, ClassId = classId };

    [Fact]
    public async Task Sessions_show_in_progress_first_then_newest_first()
    {
        var catalog = new FakeCatalog
        {
            SessionList =
            [
                Row("1", "Done", "2026-10-20 08:00:00"),
                Row("2", "Planned", "2026-10-20 14:00:00"),
                Row("3", "Started", "2026-10-20 10:00:00"),
                Row("4", "Planned", "2026-10-21 08:00:00"),
                Row("5", "Started", "2026-10-20 12:00:00"),
            ],
        };
        var vm = Create(catalog);

        await vm.LoadAsync();

        Assert.Equal(["5", "3", "4", "2", "1"], vm.Sessions.Select(s => s.Session.SessionId));
    }

    [Fact]
    public async Task Status_filter_narrows_the_loaded_list_without_reloading()
    {
        var catalog = new FakeCatalog
        {
            SessionList = [Row("1", "Done", "2026-10-20 08:00:00"), Row("2", "Started", "2026-10-20 10:00:00"), Row("3", "Done", "2026-10-20 09:00:00")],
        };
        var vm = Create(catalog);
        await vm.LoadAsync();
        var calls = catalog.SessionCalls;

        vm.SelectedStatusFilter = vm.StatusFilters.Single(f => f.Value == SessionState.Ended);

        Assert.Equal(["3", "1"], vm.Sessions.Select(s => s.Session.SessionId));
        Assert.Equal(Strings.Format(Strings.Status_SessionCountFiltered, 2, 3), vm.Status);
        Assert.Equal(calls, catalog.SessionCalls);

        vm.SelectedStatusFilter = vm.StatusFilters.Single(f => f.Value == SessionState.Planned);
        Assert.Empty(vm.Sessions);
        Assert.True(vm.IsEmpty);

        vm.SelectedStatusFilter = vm.StatusFilters[0];
        Assert.Equal(3, vm.Sessions.Count);
        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public async Task Status_filter_applies_to_the_next_load()
    {
        var catalog = new FakeCatalog { SessionList = [Row("1", "Done", "2026-10-20 08:00:00"), Row("2", "Started", "2026-10-20 10:00:00")] };
        var vm = Create(catalog);
        await vm.LoadAsync();
        vm.SelectedStatusFilter = vm.StatusFilters.Single(f => f.Value == SessionState.InProgress);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(["2"], vm.Sessions.Select(s => s.Session.SessionId));
    }

    [Fact]
    public async Task Join_tracks_the_running_session_until_it_exits()
    {
        var client = new FakeClient();
        var vm = Create(new FakeCatalog(), client);
        await vm.LoadAsync();
        var session = vm.Sessions[0].Session;

        await vm.LaunchCommand.ExecuteAsync(session);

        Assert.True(vm.IsSessionRunning);
        Assert.False(vm.LaunchCommand.CanExecute(session));
        Assert.False(vm.SignOutCommand.CanExecute(null));
        Assert.Equal(55, client.Started!.LinkId);

        client.Exit.SetResult(0);
        await client.Disposed.Task;
        Assert.False(vm.IsSessionRunning);
        Assert.Equal(Strings.Status_Ended, vm.Status);
    }

    [Fact]
    public async Task Join_mutes_the_microphone_when_the_preference_is_on()
    {
        var mic = new FakeMicrophone();
        var vm = Create(new FakeCatalog(), mic: mic);
        await vm.LoadAsync();
        vm.Settings.MuteMicrophoneOnLaunch = true;

        await vm.LaunchCommand.ExecuteAsync(vm.Sessions[0].Session);

        Assert.True(mic.State!.IsMuted);
    }

    [Fact]
    public async Task Launch_failure_is_reported_not_thrown()
    {
        var vm = Create(new FakeCatalog(), new FakeClient { Failure = new FileNotFoundException("java.exe missing") });
        await vm.LoadAsync();

        await vm.LaunchCommand.ExecuteAsync(vm.Sessions[0].Session);

        Assert.False(vm.IsSessionRunning);
        Assert.Contains("java.exe missing", vm.ErrorMessage);
    }

    [Fact]
    public async Task Shell_moves_to_sessions_after_sign_in_and_back_on_sign_out()
    {
        var shell = new ShellViewModel(Fakes.Orchestrator(), new FakeCatalog(), new MemoryVault(), new LaunchPreferences(), Fakes.Settings(null));
        var login = Assert.IsType<LoginViewModel>(shell.CurrentPage);
        login.UserName = "u";
        login.Password = "p";

        await login.SignInCommand.ExecuteAsync(null);
        var sessions = Assert.IsType<SessionsViewModel>(shell.CurrentPage);

        sessions.SignOutCommand.Execute(null);
        Assert.IsType<LoginViewModel>(shell.CurrentPage);
    }
}

public class SettingsViewModelTests
{
    [Fact]
    public void Reflects_and_controls_the_microphone()
    {
        var mic = new FakeMicrophone { State = new MicrophoneState("USB Mic", IsMuted: false, Level: 0.42f) };
        var vm = Fakes.Settings(mic);

        Assert.True(vm.HasMicrophone);
        Assert.Equal("USB Mic", vm.MicrophoneName);
        Assert.True(vm.IsMicrophoneLive);
        Assert.Equal(42, vm.MicrophoneLevel);

        vm.ToggleMicrophoneCommand.Execute(null);
        Assert.True(mic.State.IsMuted);
        Assert.True(vm.IsMicrophoneMuted);

        vm.MicrophoneLevel = 80;
        Assert.Equal(0.8f, mic.State.Level, 3);
    }

    [Fact]
    public void Follows_changes_made_outside_the_app()
    {
        var mic = new FakeMicrophone();
        var vm = Fakes.Settings(mic);

        mic.ChangeExternally(new MicrophoneState("Headset", IsMuted: true, Level: 0.1f));

        Assert.False(vm.IsMicrophoneOn);
        Assert.Equal("Headset", vm.MicrophoneName);
        Assert.False(mic.SetCalled); // reflecting external state must not write it back
    }

    [Fact]
    public void Missing_microphone_and_failures_are_reported()
    {
        var none = Fakes.Settings(new FakeMicrophone { State = null });
        Assert.True(none.HasNoMicrophone);
        Assert.False(none.IsMicrophoneLive);

        var broken = new FakeMicrophone { SetFailure = new InvalidOperationException("device busy") };
        var vm = Fakes.Settings(broken);
        vm.IsMicrophoneOn = false;
        Assert.Contains("device busy", vm.MicrophoneError);
    }

    [Fact]
    public void Preferences_are_saved_and_theme_is_applied()
    {
        var store = new MemoryPreferences(new UserPreferences(UiLanguage: "en"));
        string? appliedTheme = null;
        var vm = new SettingsViewModel(null, store, t => appliedTheme = t);

        vm.MuteMicrophoneOnLaunch = true;
        vm.SelectedTheme = vm.Themes.Single(t => t.Value == "Dark");
        vm.SelectedLanguage = vm.Languages.Single(l => l.Value == "ar");

        Assert.Equal(new UserPreferences("ar", "Dark", true), store.Saved);
        Assert.Equal("Dark", appliedTheme);
        Assert.True(vm.RestartRequired);

        vm.SelectedLanguage = vm.Languages.Single(l => l.Value == "en");
        Assert.False(vm.RestartRequired);
    }
}

internal static class Fakes
{
    public static readonly SessionInfo Session = new(
        "900", "http://conf.example.org/demo?linkid=55", "Algorithms", "Open",
        "2026-10-19 18:00", "", "3", "T1", "Dr. Tutor", "C1");

    public static LaunchOrchestrator Orchestrator(FakeAuth? auth = null, FakeClient? client = null, FakeMicrophone? microphone = null) =>
        new(auth ?? new FakeAuth(), client ?? new FakeClient(), NullLogger<LaunchOrchestrator>.Instance, null, microphone);

    public static SettingsViewModel Settings(FakeMicrophone? mic) =>
        new(mic, new MemoryPreferences(new UserPreferences()), _ => { });
}

internal sealed class MemoryVault : ICredentialVault
{
    public Credentials? Stored { get; set; }
    public void Save(Credentials credentials) => Stored = credentials;
    public Credentials? TryLoad() => Stored;
    public void Clear() => Stored = null;
}

internal sealed class MemoryPreferences(UserPreferences initial) : IUserPreferencesStore
{
    public UserPreferences Saved { get; private set; } = initial;
    public UserPreferences Load() => Saved;
    public void Save(UserPreferences preferences) => Saved = preferences;
}

internal sealed class FakeAuth : IAuthService
{
    public bool Success { get; init; } = true;
    public Exception? Failure { get; init; }

    public Task<AuthResult> AuthenticateAsync(Credentials credentials, CancellationToken ct) =>
        Failure is not null
            ? Task.FromException<AuthResult>(Failure)
            : Task.FromResult(new AuthResult(Success, UserRole.Student, Success ? "student" : "0"));
}

internal sealed class FakeCatalog : ISessionCatalog
{
    public SessionQuery? LastQuery { get; private set; }
    public int SessionCalls { get; private set; }
    public bool NoSessions { get; init; }
    public IReadOnlyList<SessionInfo>? SessionList { get; init; }
    public Exception? SessionsFailure { get; init; }
    public Exception? CoursesFailure { get; init; }

    public Task<IReadOnlyList<ProgramInfo>> GetProgramsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ProgramInfo>>([new ProgramInfo("53", "ITE"), new ProgramInfo("82", "")]);

    public Task<IReadOnlyList<CourseInfo>> GetCoursesAsync(string programId, CancellationToken ct) =>
        CoursesFailure is not null
            ? Task.FromException<IReadOnlyList<CourseInfo>>(CoursesFailure)
            : Task.FromResult<IReadOnlyList<CourseInfo>>(programId == "53" ? [new CourseInfo("1426", "AML601")] : []);

    public Task<IReadOnlyList<SessionInfo>> GetSessionsAsync(Credentials credentials, SessionQuery query, CancellationToken ct)
    {
        LastQuery = query;
        SessionCalls++;
        if (SessionsFailure is not null)
            return Task.FromException<IReadOnlyList<SessionInfo>>(SessionsFailure);
        return Task.FromResult<IReadOnlyList<SessionInfo>>(NoSessions ? [] : SessionList ?? [Fakes.Session]);
    }
}

internal sealed class FakeMicrophone : IMicrophoneService
{
    public MicrophoneState? State { get; set; } = new("Microphone", IsMuted: false, Level: 0.5f);
    public Exception? SetFailure { get; init; }
    public bool SetCalled { get; private set; }

    public event EventHandler? Changed;

    public MicrophoneState? GetState() => State;

    public void SetMuted(bool muted) => Set(State! with { IsMuted = muted });

    public void SetLevel(float level) => Set(State! with { Level = level });

    public void ChangeExternally(MicrophoneState state)
    {
        State = state;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Set(MicrophoneState state)
    {
        if (SetFailure is not null)
            throw SetFailure;
        SetCalled = true;
        State = state;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

internal sealed class FakeClient : ISessionClient
{
    public LaunchTicket? Started { get; private set; }
    public Exception? Failure { get; init; }
    public TaskCompletionSource<int> Exit { get; } = new();
    public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<ISessionHandle> StartAsync(LaunchTicket ticket, CancellationToken ct)
    {
        if (Failure is not null)
            return Task.FromException<ISessionHandle>(Failure);
        Started = ticket;
        return Task.FromResult<ISessionHandle>(new Handle(this));
    }

    private sealed class Handle(FakeClient owner) : ISessionHandle
    {
        public int ProcessId => 1;
        public Task<int> Completion => owner.Exit.Task;

        public ValueTask DisposeAsync()
        {
            owner.Disposed.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }
}
