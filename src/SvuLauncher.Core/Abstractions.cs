namespace SvuLauncher.Core;

/// <summary>Authenticates a user (legacy: svuis checkUserPass).</summary>
public interface IAuthService
{
    Task<AuthResult> AuthenticateAsync(Credentials credentials, CancellationToken ct);
}

/// <summary>Lists programs, courses and sessions (legacy: svuis getPrograms/getCourses/getSessionsByProgCourse).</summary>
public interface ISessionCatalog
{
    Task<IReadOnlyList<ProgramInfo>> GetProgramsAsync(CancellationToken ct);
    Task<IReadOnlyList<CourseInfo>> GetCoursesAsync(string programId, CancellationToken ct);
    Task<IReadOnlyList<SessionInfo>> GetSessionsAsync(Credentials credentials, SessionQuery query, CancellationToken ct);
}

/// <summary>The default capture device (legacy: clsVolume/winmm mixer). Changed may be raised on any thread.</summary>
public interface IMicrophoneService
{
    /// <summary>Null when the machine has no active capture device.</summary>
    MicrophoneState? GetState();
    void SetMuted(bool muted);
    void SetLevel(float level);
    event EventHandler? Changed;
}

public interface IUserPreferencesStore
{
    UserPreferences Load();
    void Save(UserPreferences preferences);
}

/// <summary>Meeting status lookups (legacy: sessions.svuonline.org getMeetingLaunchCount).</summary>
public interface IMeetingStatusService
{
    Task<int> GetLaunchCountAsync(int serverId, string tutorId, string meetingId, CancellationToken ct);
}

/// <summary>Persists "remember me" credentials. Implementations must encrypt at rest.</summary>
public interface ICredentialVault
{
    void Save(Credentials credentials);
    Credentials? TryLoad();
    void Clear();
}

/// <summary>Starts the conferencing client. The legacy Linktivity JVM is one implementation.</summary>
public interface ISessionClient
{
    Task<ISessionHandle> StartAsync(LaunchTicket ticket, CancellationToken ct);
}

public interface ISessionHandle : IAsyncDisposable
{
    int ProcessId { get; }
    Task<int> Completion { get; }
}
