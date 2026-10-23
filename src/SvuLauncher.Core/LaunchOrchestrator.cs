using System.Globalization;
using Microsoft.Extensions.Logging;

namespace SvuLauncher.Core;

public sealed record LaunchPreferences(
    int Port = 443,
    string Language = "EN",
    bool RequireHostStarted = false,
    bool MuteMicrophoneOnLaunch = false);

public sealed class SessionNotStartedException(string sessionId)
    : Exception($"Session {sessionId} has not been started by its tutor yet.");

/// <summary>
/// The core launch-and-auth routine: authenticate, resolve a session to a launch ticket,
/// optionally check meeting status, then hand off to the conferencing client.
/// </summary>
public sealed class LaunchOrchestrator(
    IAuthService auth,
    ISessionClient client,
    ILogger<LaunchOrchestrator> log,
    IMeetingStatusService? meetingStatus = null,
    IMicrophoneService? microphone = null)
{
    public async Task<UserIdentity> SignInAsync(Credentials credentials, CancellationToken ct)
    {
        var result = await auth.AuthenticateAsync(credentials, ct).ConfigureAwait(false);
        if (!result.Success)
            throw new UnauthorizedAccessException("The user name or password was rejected.");
        log.LogInformation("Signed in {User} as {Role}", credentials.UserName, result.Role);
        return new UserIdentity(credentials.UserName, result.Role);
    }

    public static LaunchTicket CreateTicket(SessionInfo session, UserIdentity user, LaunchPreferences prefs)
    {
        var url = StudentUrlParser.Parse(session.StudentUrl);
        // Legacy passed the same host as both web host (argv[0]) and server address (argv[1]).
        return new LaunchTicket(
            WebHost: url.Host,
            ServerAddress: url.Host,
            Port: prefs.Port,
            LinkId: url.LinkId,
            UserId: user.UserName,
            Role: user.Role == UserRole.Tutor ? ClientRole.Host : ClientRole.User,
            Language: prefs.Language);
    }

    public async Task<ISessionHandle> LaunchAsync(
        SessionInfo session, UserIdentity user, LaunchPreferences prefs, CancellationToken ct)
    {
        var ticket = CreateTicket(session, user, prefs);

        if (meetingStatus is not null && user.Role == UserRole.Student)
        {
            var count = await TryGetLaunchCountAsync(session, ct).ConfigureAwait(false);
            if (count == 0 && prefs.RequireHostStarted)
                throw new SessionNotStartedException(session.SessionId);
        }

        if (prefs.MuteMicrophoneOnLaunch)
            MuteMicrophone();

        log.LogInformation("Launching session {SessionId} (link {LinkId}) on {Server}:{Port} as {Role}",
            session.SessionId, ticket.LinkId, ticket.ServerAddress, ticket.Port, ticket.Role);
        return await client.StartAsync(ticket, ct).ConfigureAwait(false);
    }

    private void MuteMicrophone()
    {
        try
        {
            if (microphone?.GetState() is not null)
            {
                microphone.SetMuted(true);
                log.LogInformation("Microphone muted before joining the session");
            }
        }
        catch (Exception ex)
        {
            // Joining matters more than the mute preference; report and continue.
            log.LogWarning(ex, "Could not mute the microphone before launch");
        }
    }

    private async Task<int?> TryGetLaunchCountAsync(SessionInfo session, CancellationToken ct)
    {
        if (!int.TryParse(session.ServerNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var serverId))
        {
            log.LogWarning("Session {SessionId} has non-numeric serverNumber {ServerNumber}; skipping status check",
                session.SessionId, session.ServerNumber);
            return null;
        }
        try
        {
            return await meetingStatus!.GetLaunchCountAsync(serverId, session.TutorId, session.SessionId, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Unlike the legacy code, a failed status check is logged, never silently dropped.
            log.LogWarning(ex, "Meeting status check failed for session {SessionId}", session.SessionId);
            return null;
        }
    }
}
