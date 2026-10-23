namespace SvuLauncher.Core;

public enum UserRole { Student, Tutor }

/// <summary>Role token passed to LinkJvm as argv[5].</summary>
public enum ClientRole { User, Host }

public sealed record Credentials(string UserName, string Password)
{
    // Never let the password reach logs via record auto-ToString.
    public override string ToString() => $"Credentials {{ UserName = {UserName}, Password = *** }}";
}

public sealed record UserIdentity(string UserName, UserRole Role);

public sealed record AuthResult(bool Success, UserRole Role, string RawResult);

public sealed record ProgramInfo(string Id, string Code);

/// <summary>The legacy first filter (frmMain cmbOptions), in the legacy item order.</summary>
public enum SessionScope
{
    /// <summary>إظهار جلسات الطالب في صفوفه فقط: sessions in the student's own classes.</summary>
    MyClasses,

    /// <summary>إظهار جلسات الطالب لجميع صفوف مواده: sessions in every class of the student's courses.</summary>
    AllMyCourseClasses,

    /// <summary>إظهار الجلسات بحسب البرنامج والمادة: sessions by the selected program and course.</summary>
    ByProgramAndCourse,
}

/// <summary>Filters for the session list. Program and course id 0 mean "all" (legacy "All Programs"/"All Courses").</summary>
public sealed record SessionQuery(SessionScope Scope, int ProgramId = SessionQuery.All, int CourseId = SessionQuery.All)
{
    public const int All = 0;

    /// <summary>Legacy frmMain enables the program and course combos only for this scope.</summary>
    public static bool UsesProgramAndCourse(SessionScope scope) => scope == SessionScope.ByProgramAndCourse;
}

public sealed record MicrophoneState(string DeviceName, bool IsMuted, float Level);

/// <summary>Per-user choices, persisted separately from the machine-wide svulauncher.json.</summary>
public sealed record UserPreferences(
    string? UiLanguage = null,
    string Theme = "System",
    bool MuteMicrophoneOnLaunch = false);

public sealed record CourseInfo(string Id, string Code);

/// <summary>Mirrors tns:Session from NewSessions2.php?wsdl.</summary>
public sealed record SessionInfo(
    string SessionId,
    string StudentUrl,
    string Name,
    string Status,
    string StartDate,
    string LaunchedDate,
    string ServerNumber,
    string TutorId,
    string TutorName,
    string ClassId);

/// <summary>Fields the legacy launcher extracted from Session.studentURL.</summary>
public sealed record StudentUrlInfo(string Host, long LinkId, string? SessionPasscode)
{
    public override string ToString() =>
        $"StudentUrlInfo {{ Host = {Host}, LinkId = {LinkId}, SessionPasscode = {(SessionPasscode is null ? "null" : "***")} }}";
}

/// <summary>Everything needed to start the conferencing client for one session.</summary>
public sealed record LaunchTicket(
    string WebHost,
    string ServerAddress,
    int Port,
    long LinkId,
    string UserId,
    ClientRole Role,
    string Language);
