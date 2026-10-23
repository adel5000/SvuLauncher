using System.Globalization;
using Microsoft.Extensions.Logging;
using SvuLauncher.Core;
using SvuLauncher.Infrastructure;

const string Usage = """
    usage:
      svulauncher login [--save]
      svulauncher logout
      svulauncher programs
      svulauncher courses <programId>
      svulauncher sessions [--scope mine|courses|program] [--program <id>] [--course <id>]
      svulauncher launch <sessionId> [--scope mine|courses|program] [--program <id>] [--course <id>] [--dry-run]

    Scopes (legacy order): mine = sessions in your own classes (default), courses = every
    class of your courses, program = by program and course. --program and --course apply
    only with --scope program and default to 0 ("all").
    """;

if (args.Length == 0)
{
    Console.Error.WriteLine(Usage);
    return 2;
}

var settings = LauncherSettings.Load(Path.Combine(AppContext.BaseDirectory, "svulauncher.json"));
using var loggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole(o => o.SingleLine = true).SetMinimumLevel(LogLevel.Information));
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var services = LauncherServices.Create(settings, http, loggerFactory);
var directory = services.Directory;
var vault = services.Vault;
var orchestrator = services.Orchestrator;
var prefs = services.Preferences;

try
{
    switch (args[0])
    {
        case "login":
        {
            var creds = PromptCredentials();
            var user = await orchestrator.SignInAsync(creds, cts.Token);
            if (args.Contains("--save"))
                vault.Save(creds);
            Console.WriteLine($"Signed in as {user.UserName} ({user.Role}).");
            return 0;
        }
        case "logout":
            vault.Clear();
            Console.WriteLine("Saved credentials removed.");
            return 0;
        case "programs":
            foreach (var p in await directory.GetProgramsAsync(cts.Token))
                Console.WriteLine($"{p.Id,8}  {p.Code}");
            return 0;
        case "courses" when args.Length > 1:
            foreach (var c in await directory.GetCoursesAsync(args[1], cts.Token))
                Console.WriteLine($"{c.Id,8}  {c.Code}");
            return 0;
        case "sessions":
        {
            var creds = vault.TryLoad() ?? PromptCredentials();
            var sessions = await directory.GetSessionsAsync(creds, Query(), cts.Token);
            foreach (var s in sessions)
                Console.WriteLine($"{s.SessionId,8}  {s.StartDate,-20} {s.Status,-10} {s.TutorName,-25} {s.Name}");
            return 0;
        }
        case "launch" when args.Length > 1:
        {
            var creds = vault.TryLoad() ?? PromptCredentials();
            var user = await orchestrator.SignInAsync(creds, cts.Token);
            var sessions = await directory.GetSessionsAsync(creds, Query(), cts.Token);
            var session = sessions.FirstOrDefault(s => s.SessionId == args[1])
                ?? throw new KeyNotFoundException($"Session {args[1]} is not in the list for these filters.");

            if (args.Contains("--dry-run"))
            {
                var ticket = LaunchOrchestrator.CreateTicket(session, user, prefs);
                Console.WriteLine("java.exe " + string.Join(' ', LinkJvmCommand.BuildArguments(ticket)));
                return 0;
            }

            await using var handle = await orchestrator.LaunchAsync(session, user, prefs, cts.Token);
            return await handle.Completion.WaitAsync(cts.Token);
        }
        default:
            Console.Error.WriteLine(Usage);
            return 2;
    }
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    loggerFactory.CreateLogger("svulauncher").LogError(ex, "Command failed");
    return 1;
}

string? StringOption(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

int IntOption(string name, int fallback) =>
    StringOption(name) is { } value ? int.Parse(value, CultureInfo.InvariantCulture) : fallback;

SessionQuery Query()
{
    var scope = StringOption("--scope") switch
    {
        null or "mine" => SessionScope.MyClasses,
        "courses" => SessionScope.AllMyCourseClasses,
        "program" => SessionScope.ByProgramAndCourse,
        var other => throw new ArgumentException($"Unknown --scope '{other}'; use mine, courses or program."),
    };
    if (SessionQuery.UsesProgramAndCourse(scope))
        return new(scope, IntOption("--program", SessionQuery.All), IntOption("--course", SessionQuery.All));
    if (args.Contains("--program") || args.Contains("--course"))
        Console.Error.WriteLine("warning: --program and --course are ignored unless --scope program.");
    return new(scope);
}

static Credentials PromptCredentials()
{
    Console.Write("User name: ");
    var user = Console.ReadLine()?.Trim() ?? "";
    Console.Write("Password: ");
    if (Console.IsInputRedirected)
    {
        // Console.ReadKey throws on redirected input (scripts, CI); read a line instead.
        var line = Console.ReadLine() ?? "";
        Console.WriteLine();
        return new Credentials(user, line);
    }

    var password = new System.Text.StringBuilder();
    for (var key = Console.ReadKey(intercept: true); key.Key != ConsoleKey.Enter; key = Console.ReadKey(intercept: true))
    {
        if (key.Key == ConsoleKey.Backspace) { if (password.Length > 0) password.Length--; }
        else if (!char.IsControl(key.KeyChar)) password.Append(key.KeyChar);
    }
    Console.WriteLine();
    return new Credentials(user, password.ToString());
}
