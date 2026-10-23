using System.Globalization;

namespace SvuLauncher.Core;

/// <summary>
/// Builds the argv contract recovered from LinkJvm.parseArgs():
///   [0] web host (CodeBase/DocBase/help URLs)   [1] conferencing server address
///   [2] port   [3] session id   [4] user id   [5] User|Host   [6] language
/// LinkJvm.main exits with code 87 when fewer than 7 args are given.
/// </summary>
public static class LinkJvmCommand
{
    public const string MainClass = "LinkJvm";
    public const string ClassPath = "linkjvm.jar";
    public const int BadArgumentsExitCode = 87;

    public static IReadOnlyList<string> BuildArguments(LaunchTicket ticket, IEnumerable<string>? jvmOptions = null)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        // The legacy VB6 code joined argv with spaces, so a space in any value shifted every
        // following positional argument. LinkJvm itself is positional, so reject such values.
        RequireToken(ticket.WebHost, nameof(ticket.WebHost));
        RequireToken(ticket.ServerAddress, nameof(ticket.ServerAddress));
        RequireToken(ticket.UserId, nameof(ticket.UserId));
        RequireToken(ticket.Language, nameof(ticket.Language));
        if (ticket.Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(ticket), "Port must be 1-65535.");
        if (ticket.LinkId <= 0)
            throw new ArgumentOutOfRangeException(nameof(ticket), "LinkId must be positive.");

        var args = new List<string>(jvmOptions ?? []);
        args.AddRange(
        [
            "-cp", ClassPath, MainClass,
            ticket.WebHost,
            ticket.ServerAddress,
            ticket.Port.ToString(CultureInfo.InvariantCulture),
            ticket.LinkId.ToString(CultureInfo.InvariantCulture),
            ticket.UserId,
            ticket.Role == ClientRole.Host ? "Host" : "User",
            ticket.Language,
        ]);
        return args;
    }

    private static void RequireToken(string value, string name)
    {
        if (string.IsNullOrEmpty(value) || value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c == '"'))
            throw new ArgumentException($"{name} must be a non-empty token without whitespace or quotes.", name);
    }
}
