using Microsoft.Extensions.Logging.Abstractions;
using SvuLauncher.Core;

namespace SvuLauncher.Tests;

// studentURL values below are synthetic; the real format was not observed, only that it
// carries a host plus "linkid=" and "sessp=" parameters.
public class StudentUrlParserTests
{
    [Fact]
    public void Extracts_host_linkid_and_passcode()
    {
        var info = StudentUrlParser.Parse("http://conf.example.org:8080/demo/join.asp?linkid=12345&sessp=abc");
        Assert.Equal("conf.example.org:8080", info.Host);
        Assert.Equal(12345, info.LinkId);
        Assert.Equal("abc", info.SessionPasscode);
    }

    [Fact]
    public void Accepts_scheme_less_url_and_case_insensitive_keys()
    {
        var info = StudentUrlParser.Parse("conf.example.org/demo?LinkID=7");
        Assert.Equal("conf.example.org", info.Host);
        Assert.Equal(7, info.LinkId);
        Assert.Null(info.SessionPasscode);
    }

    [Theory]
    [InlineData("http://conf.example.org/demo")]
    [InlineData("http://conf.example.org/demo?linkid=abc")]
    [InlineData("http://conf.example.org/demo?linkid=-1")]
    public void Rejects_missing_or_non_numeric_linkid(string url) =>
        Assert.Throws<FormatException>(() => StudentUrlParser.Parse(url));

    [Fact]
    public void ToString_redacts_passcode() =>
        Assert.DoesNotContain("secret", new StudentUrlInfo("h", 1, "secret").ToString());
}

public class LinkJvmCommandTests
{
    private static LaunchTicket Ticket(string user = "student01", ClientRole role = ClientRole.User) =>
        new("conf.example.org", "conf.example.org", 443, 12345, user, role, "EN");

    [Fact]
    public void Matches_legacy_positional_contract()
    {
        var args = LinkJvmCommand.BuildArguments(Ticket(), ["-Xmx256m"]);
        Assert.Equal(
            ["-Xmx256m", "-cp", "linkjvm.jar", "LinkJvm", "conf.example.org", "conf.example.org", "443", "12345", "student01", "User", "EN"],
            args);
        Assert.Equal(7, args.Count - args.ToList().IndexOf("LinkJvm") - 1);
    }

    [Fact]
    public void Tutor_launches_as_host() =>
        Assert.Contains("Host", LinkJvmCommand.BuildArguments(Ticket(role: ClientRole.Host)));

    [Theory]
    [InlineData("two words")]
    [InlineData("quo\"te")]
    [InlineData("")]
    public void Rejects_values_that_would_shift_positional_args(string user) =>
        Assert.Throws<ArgumentException>(() => LinkJvmCommand.BuildArguments(Ticket(user)));
}

public class LaunchOrchestratorTests
{
    private static readonly SessionInfo Session = new(
        "900", "http://conf.example.org/demo?linkid=55&sessp=x", "Algorithms", "Open",
        "2026-10-19 18:00", "", "3", "T1", "Dr. Tutor", "C1");

    [Fact]
    public void Tutor_ticket_uses_host_role_and_url_fields()
    {
        var ticket = LaunchOrchestrator.CreateTicket(Session, new UserIdentity("tutor7", UserRole.Tutor), new LaunchPreferences());
        Assert.Equal(new LaunchTicket("conf.example.org", "conf.example.org", 443, 55, "tutor7", ClientRole.Host, "EN"), ticket);
    }

    [Fact]
    public async Task Student_is_blocked_when_host_not_started_and_policy_requires_it()
    {
        var orchestrator = new LaunchOrchestrator(new FakeAuth(), new FakeClient(),
            NullLogger<LaunchOrchestrator>.Instance, new FakeStatus(0));
        await Assert.ThrowsAsync<SessionNotStartedException>(() => orchestrator.LaunchAsync(
            Session, new UserIdentity("s1", UserRole.Student), new LaunchPreferences(RequireHostStarted: true), default));
    }

    [Fact]
    public async Task Status_failure_does_not_block_launch()
    {
        var client = new FakeClient();
        var orchestrator = new LaunchOrchestrator(new FakeAuth(), client,
            NullLogger<LaunchOrchestrator>.Instance, new FakeStatus(null));
        await orchestrator.LaunchAsync(Session, new UserIdentity("s1", UserRole.Student),
            new LaunchPreferences(RequireHostStarted: true), default);
        Assert.Equal(55, client.Started!.LinkId);
    }

    [Fact]
    public async Task Rejected_credentials_throw()
    {
        var orchestrator = new LaunchOrchestrator(new FakeAuth(success: false), new FakeClient(), NullLogger<LaunchOrchestrator>.Instance);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => orchestrator.SignInAsync(new Credentials("u", "p"), default));
    }

    private sealed class FakeAuth(bool success = true) : IAuthService
    {
        public Task<AuthResult> AuthenticateAsync(Credentials credentials, CancellationToken ct) =>
            Task.FromResult(new AuthResult(success, UserRole.Student, success ? "student" : "0"));
    }

    private sealed class FakeStatus(int? count) : IMeetingStatusService
    {
        public Task<int> GetLaunchCountAsync(int serverId, string tutorId, string meetingId, CancellationToken ct) =>
            count is { } c ? Task.FromResult(c) : Task.FromException<int>(new HttpRequestException("offline"));
    }

    private sealed class FakeClient : ISessionClient
    {
        public LaunchTicket? Started { get; private set; }

        public Task<ISessionHandle> StartAsync(LaunchTicket ticket, CancellationToken ct)
        {
            Started = ticket;
            return Task.FromResult<ISessionHandle>(new Handle());
        }

        private sealed class Handle : ISessionHandle
        {
            public int ProcessId => 0;
            public Task<int> Completion => Task.FromResult(0);
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
