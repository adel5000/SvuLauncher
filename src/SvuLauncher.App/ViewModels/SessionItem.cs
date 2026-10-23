using System.Globalization;
using SvuLauncher.App.Resources;
using SvuLauncher.Core;

namespace SvuLauncher.App.ViewModels;

/// <summary>Session states the service reports (observed: Planned, Started, Done).</summary>
public enum SessionState { Other, Planned, InProgress, Ended }

/// <summary>One row of the session list: a <see cref="SessionInfo"/> with parsed dates and display text.</summary>
public sealed class SessionItem
{
    // Observed service format: "2026-10-20 08:00:00".
    private static readonly string[] DateFormats = ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss"];

    public SessionItem(SessionInfo session, UserRole role)
    {
        Session = session;
        Start = ParseDate(session.StartDate);
        Launched = ParseDate(session.LaunchedDate);
        State = StateOf(session.Status);
        // Legacy frmMain colored a student's row (&H91C8C8) when the session's classID was 0:
        // a session of one of the student's courses held for another class.
        IsOutsideMyClasses = role == UserRole.Student
            && (string.IsNullOrWhiteSpace(session.ClassId)
                || (int.TryParse(session.ClassId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var classId) && classId == 0));
    }

    public SessionInfo Session { get; }

    public string Name => Session.Name;

    public string TutorName => Session.TutorName;

    public DateTime? Start { get; }

    /// <summary>When the session was actually started (legacy "وقت البدء الفعلي"); null until it starts.</summary>
    public DateTime? Launched { get; }

    public bool HasLaunched => Launched is not null;

    public SessionState State { get; }

    public bool IsInProgress => State == SessionState.InProgress;

    public bool IsOutsideMyClasses { get; }

    public string StartText => Start is { } start
        ? start.ToString("ddd d MMM, ", CultureInfo.CurrentUICulture) + start.ToString("t", CultureInfo.CurrentUICulture)
        : Session.StartDate;

    public string? LaunchedText => Launched is { } launched
        ? Strings.Format(Strings.Session_StartedAt, launched.ToString("t", CultureInfo.CurrentUICulture))
        : null;

    public string StatusLabel => State switch
    {
        SessionState.InProgress => Strings.SessionState_InProgress,
        SessionState.Planned => Strings.SessionState_Planned,
        SessionState.Ended => Strings.SessionState_Ended,
        _ => Session.Status,
    };

    public static SessionState StateOf(string status) => status.Trim().ToLowerInvariant() switch
    {
        "started" => SessionState.InProgress,
        "planned" => SessionState.Planned,
        "done" => SessionState.Ended,
        _ => SessionState.Other,
    };

    /// <summary>In-progress sessions first, then newest start first; undated sessions last.</summary>
    public static IEnumerable<SessionItem> Order(IEnumerable<SessionItem> items) =>
        items.OrderByDescending(i => i.IsInProgress)
             .ThenByDescending(i => i.Start ?? DateTime.MinValue)
             .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase);

    private static DateTime? ParseDate(string value) =>
        DateTime.TryParseExact(value.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
}
