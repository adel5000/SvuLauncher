using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SvuLauncher.App.Resources;
using SvuLauncher.Core;

namespace SvuLauncher.App.ViewModels;

/// <summary>
/// The legacy frmMain filters (scope, program, course; program and course only for the
/// by-program scope), the session list, launch and the running-session state.
/// </summary>
public sealed partial class SessionsViewModel : ObservableObject
{
    private readonly LaunchOrchestrator _orchestrator;
    private readonly ISessionCatalog _catalog;
    private readonly LaunchPreferences _preferences;
    private readonly Credentials _credentials;
    private readonly ProgramInfo _allPrograms = new(SessionQuery.All.ToString(CultureInfo.InvariantCulture), Strings.AllPrograms);
    private readonly CourseInfo _allCourses = new(SessionQuery.All.ToString(CultureInfo.InvariantCulture), Strings.AllCourses);
    private CancellationTokenSource? _refreshCts;
    private CancellationTokenSource? _autoRefreshCts;
    private ISessionHandle? _runningSession;
    private bool _ready;
    private bool _reloadingCourses;
    private int _courseVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProgramCourseEnabled))]
    private Choice<SessionScope>? _selectedScope;

    [ObservableProperty]
    private ProgramInfo? _selectedProgram;

    [ObservableProperty]
    private CourseInfo? _selectedCourse;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    [NotifyCanExecuteChangedFor(nameof(SignOutCommand))]
    private bool _isSessionRunning;

    [ObservableProperty]
    private string? _runningMessage;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEmpty;

    /// <summary>By program and course with "All Programs": the large catalog-wide list loads only on Refresh.</summary>
    [ObservableProperty]
    private bool _isAwaitingProgram;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private bool _isSettingsOpen;

    /// <summary>Client-side status filter over the loaded sessions; null shows every status.</summary>
    [ObservableProperty]
    private Choice<SessionState?>? _selectedStatusFilter;

    private List<SessionItem> _loaded = [];
    private bool _hasResults;

    public SessionsViewModel(
        LaunchOrchestrator orchestrator, ISessionCatalog catalog, LaunchPreferences preferences,
        SettingsViewModel settings, UserIdentity user, Credentials credentials)
    {
        _orchestrator = orchestrator;
        _catalog = catalog;
        _preferences = preferences;
        _credentials = credentials;
        Settings = settings;
        User = user;
        Scopes =
        [
            new(SessionScope.MyClasses, Strings.Scope_MyClasses),
            new(SessionScope.AllMyCourseClasses, Strings.Scope_AllMyCourseClasses),
            new(SessionScope.ByProgramAndCourse, Strings.Scope_ByProgramAndCourse),
        ];

        StatusFilters =
        [
            new(null, Strings.StatusFilter_All),
            new(SessionState.InProgress, Strings.SessionState_InProgress),
            new(SessionState.Planned, Strings.SessionState_Planned),
            new(SessionState.Ended, Strings.SessionState_Ended),
        ];
        SelectedStatusFilter = StatusFilters[0];

        Programs.Add(_allPrograms);
        Courses.Add(_allCourses);
        SelectedScope = Scopes[0];
        SelectedProgram = _allPrograms;
        SelectedCourse = _allCourses;
    }

    public event Action? SignedOut;

    public UserIdentity User { get; }

    public SettingsViewModel Settings { get; }

    public string Greeting => Strings.Format(Strings.Greeting, User.UserName,
        User.Role == UserRole.Tutor ? Strings.Role_Tutor : Strings.Role_Student);

    public IReadOnlyList<Choice<SessionScope>> Scopes { get; }

    public ObservableCollection<ProgramInfo> Programs { get; } = [];

    public ObservableCollection<CourseInfo> Courses { get; } = [];

    public IReadOnlyList<Choice<SessionState?>> StatusFilters { get; }

    /// <summary>The loaded sessions after the status filter, in-progress first then newest first.</summary>
    public ObservableCollection<SessionItem> Sessions { get; } = [];

    public bool HasError => ErrorMessage is not null;

    /// <summary>Program and course apply only to the by-program scope; selections are kept while disabled.</summary>
    public bool IsProgramCourseEnabled => SelectedScope is { } scope && SessionQuery.UsesProgramAndCourse(scope.Value);

    /// <summary>The most recent filter-triggered load; lets tests await work started by property setters.</summary>
    internal Task Pending { get; private set; } = Task.CompletedTask;

    /// <summary>Filter changes within this window collapse into one load (legacy only loaded on its Execute button).</summary>
    internal TimeSpan AutoRefreshDelay { get; set; } = TimeSpan.FromMilliseconds(300);

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            foreach (var program in await _catalog.GetProgramsAsync(CancellationToken.None))
                Programs.Add(string.IsNullOrWhiteSpace(program.Code) ? program with { Code = program.Id } : program);
        }
        catch (Exception ex)
        {
            // Keep the error visible; Refresh (which clears errors) retries the sessions later.
            ErrorMessage = Strings.Format(Strings.Status_ProgramsFailed, ex.Message);
            IsLoading = false;
            _ready = true;
            return;
        }
        _ready = true;
        await RefreshAsync();
    }

    partial void OnSelectedScopeChanged(Choice<SessionScope>? value)
    {
        if (_ready)
            Pending = ScheduleRefresh();
    }

    partial void OnSelectedProgramChanged(ProgramInfo? value)
    {
        if (_ready)
            Pending = ReloadCoursesAsync(value);
    }

    partial void OnSelectedCourseChanged(CourseInfo? value)
    {
        if (_ready && !_reloadingCourses && IsProgramCourseEnabled)
            Pending = ScheduleRefresh();
    }

    /// <summary>
    /// Debounced automatic load after a filter change. "By program and course" with All Programs
    /// returns the whole catalog (~450 sessions observed), so that one waits for an explicit Refresh.
    /// </summary>
    private Task ScheduleRefresh()
    {
        _autoRefreshCts?.Cancel();
        if (IsProgramCourseEnabled && ReferenceEquals(SelectedProgram, _allPrograms))
        {
            // Detach the in-flight load so its completion leaves this state alone.
            _refreshCts?.Cancel();
            _refreshCts = null;
            _loaded = [];
            _hasResults = false;
            Sessions.Clear();
            Status = null;
            ErrorMessage = null;
            IsLoading = false;
            IsEmpty = false;
            IsAwaitingProgram = true;
            return Task.CompletedTask;
        }
        var cts = _autoRefreshCts = new CancellationTokenSource();
        return DelayedRefreshAsync(cts.Token);
    }

    private async Task DelayedRefreshAsync(CancellationToken ct)
    {
        try
        {
            if (AutoRefreshDelay > TimeSpan.Zero)
                await Task.Delay(AutoRefreshDelay, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (!ct.IsCancellationRequested)
            await RefreshAsync();
    }

    partial void OnSelectedStatusFilterChanged(Choice<SessionState?>? value)
    {
        // Filtering is local; a load in flight applies the new filter when it completes.
        if (!_hasResults || IsLoading)
            return;
        ShowSessions();
        IsEmpty = Sessions.Count == 0;
    }

    private void ShowSessions()
    {
        var state = SelectedStatusFilter?.Value;
        var visible = state is null ? _loaded : _loaded.Where(i => i.State == state);
        Sessions.Clear();
        foreach (var item in SessionItem.Order(visible))
            Sessions.Add(item);
        Status = state is null
            ? Strings.Format(Strings.Status_SessionCount, _loaded.Count)
            : Strings.Format(Strings.Status_SessionCountFiltered, Sessions.Count, _loaded.Count);
    }

    private async Task ReloadCoursesAsync(ProgramInfo? program)
    {
        var version = ++_courseVersion;
        _reloadingCourses = true;
        try
        {
            Courses.Clear();
            Courses.Add(_allCourses);
            SelectedCourse = _allCourses;
        }
        finally
        {
            _reloadingCourses = false;
        }

        // Show the whole program's sessions while its courses load.
        var refresh = IsProgramCourseEnabled ? ScheduleRefresh() : Task.CompletedTask;
        if (program is not null && !ReferenceEquals(program, _allPrograms))
        {
            try
            {
                var courses = await _catalog.GetCoursesAsync(program.Id, CancellationToken.None);
                if (version == _courseVersion)
                {
                    foreach (var course in courses)
                        Courses.Add(course);
                }
            }
            catch (Exception ex)
            {
                if (version == _courseVersion)
                    ErrorMessage = Strings.Format(Strings.Status_CoursesFailed, ex.Message);
            }
        }
        await refresh;
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RefreshAsync()
    {
        if (SelectedScope is not { } scope)
            return;
        int programId = SessionQuery.All, courseId = SessionQuery.All;
        if (IsProgramCourseEnabled)
        {
            if (!TryParseId(SelectedProgram?.Id, out programId))
            {
                ErrorMessage = Strings.Format(Strings.Status_NonNumericProgram, SelectedProgram!.Code, SelectedProgram.Id);
                return;
            }
            TryParseId(SelectedCourse?.Id, out courseId);
        }

        // A newer selection supersedes an in-flight request so stale results never overwrite the list.
        _autoRefreshCts?.Cancel();
        _refreshCts?.Cancel();
        var cts = _refreshCts = new CancellationTokenSource();
        IsLoading = true;
        IsEmpty = false;
        IsAwaitingProgram = false;
        ErrorMessage = null;
        try
        {
            var sessions = await _catalog.GetSessionsAsync(
                _credentials, new SessionQuery(scope.Value, programId, courseId), cts.Token);
            if (cts.IsCancellationRequested)
                return;
            _loaded = sessions.Select(s => new SessionItem(s, User.Role)).ToList();
            _hasResults = true;
            ShowSessions();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _loaded = [];
            _hasResults = false;
            Sessions.Clear();
            Status = null;
            ErrorMessage = Strings.Format(Strings.Status_SessionsFailed, ex.Message);
        }
        finally
        {
            if (ReferenceEquals(_refreshCts, cts))
            {
                IsLoading = false;
                IsEmpty = Sessions.Count == 0 && ErrorMessage is null;
            }
        }
    }

    private static bool TryParseId(string? id, out int value)
    {
        value = SessionQuery.All;
        return id is null || int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private bool CanLaunch(SessionInfo? session) => session is not null && !IsSessionRunning;

    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private async Task LaunchAsync(SessionInfo? session, CancellationToken ct)
    {
        if (session is null)
            return;
        var preferences = _preferences with { MuteMicrophoneOnLaunch = Settings.MuteMicrophoneOnLaunch };
        try
        {
            var handle = await _orchestrator.LaunchAsync(session, User, preferences, ct);
            _runningSession = handle;
            ErrorMessage = null;
            RunningMessage = Strings.Format(Strings.Status_Running, session.Name);
            IsSessionRunning = true;
            _ = WatchAsync(handle);
        }
        catch (SessionNotStartedException)
        {
            ErrorMessage = Strings.Format(Strings.Status_NotStarted, session.SessionId);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            ErrorMessage = Strings.Format(Strings.Status_LaunchFailed, ex.Message);
        }
    }

    private async Task WatchAsync(ISessionHandle handle)
    {
        try
        {
            var code = await handle.Completion;
            Status = code == 0 ? Strings.Status_Ended : Strings.Format(Strings.Status_EndedWithCode, code);
        }
        catch (Exception ex)
        {
            ErrorMessage = Strings.Format(Strings.Status_ClientFailed, ex.Message);
        }
        finally
        {
            if (ReferenceEquals(_runningSession, handle))
            {
                _runningSession = null;
                RunningMessage = null;
                IsSessionRunning = false;
            }
            await handle.DisposeAsync();
        }
    }

    public async ValueTask EndSessionAsync()
    {
        if (_runningSession is { } handle)
            await handle.DisposeAsync();
    }

    [RelayCommand]
    private void ToggleSettings() => IsSettingsOpen = !IsSettingsOpen;

    private bool CanSignOut() => !IsSessionRunning;

    [RelayCommand(CanExecute = nameof(CanSignOut))]
    private void SignOut()
    {
        _refreshCts?.Cancel();
        SignedOut?.Invoke();
    }
}
