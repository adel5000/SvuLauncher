using System.Xml.Linq;
using SvuLauncher.Core;

namespace SvuLauncher.Infrastructure.Soap;

/// <summary>
/// Client for https://svuis.svuonline.org/SVUIS/WebDemo_wservice/NewSessions2.php (RPC/encoded).
/// Operation and part names come from the live WSDL.
/// </summary>
public sealed class SvuisDirectoryClient(SoapClient soap) : IAuthService, ISessionCatalog
{
    public const string Namespace = "https://svuis.svuonline.org/SVUIS/WebDemo_wservice/";
    public static readonly Uri Endpoint = new("https://svuis.svuonline.org/SVUIS/WebDemo_wservice/NewSessions2.php");

    public async Task<AuthResult> AuthenticateAsync(Credentials credentials, CancellationToken ct)
    {
        var result = await soap.InvokeAsync("checkUserPass",
            [("userName", credentials.UserName), ("passWord", credentials.Password)], ct).ConfigureAwait(false);
        return InterpretCheckUserPass(LeafText(result));
    }

    public async Task<IReadOnlyList<ProgramInfo>> GetProgramsAsync(CancellationToken ct)
    {
        var result = await soap.InvokeAsync("getPrograms", [], ct).ConfigureAwait(false);
        return Records(result, "programID")
            .Select(r => new ProgramInfo(r.Get("programID"), r.Get("programCode")))
            .ToList();
    }

    public async Task<IReadOnlyList<CourseInfo>> GetCoursesAsync(string programId, CancellationToken ct)
    {
        var result = await soap.InvokeAsync("getCourses", [("programID", programId)], ct).ConfigureAwait(false);
        return Records(result, "courseID")
            .Select(r => new CourseInfo(r.Get("courseID"), r.Get("courseCode")))
            .ToList();
    }

    /// <summary>
    /// Maps the scope filter to the service's "option" value: the ItemData of the legacy
    /// cmbOptions items, read from the running legacy client. The service accepts only 1-3
    /// (0, 4 and 5 crash it with a PHP fault).
    /// </summary>
    public static int OptionFor(SessionScope scope) => scope switch
    {
        SessionScope.MyClasses => 1,
        SessionScope.AllMyCourseClasses => 2,
        SessionScope.ByProgramAndCourse => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null),
    };

    public async Task<IReadOnlyList<SessionInfo>> GetSessionsAsync(
        Credentials credentials, SessionQuery query, CancellationToken ct)
    {
        // The legacy client sends a 5th, WSDL-undocumented argument: the selected course
        // (ItemData of the course combo). The service takes it positionally.
        // The password goes with every catalog call; that is a server-side design constraint.
        // Program and course only apply to the by-program scope; the legacy UI disables them otherwise.
        var filtered = SessionQuery.UsesProgramAndCourse(query.Scope);
        var result = await soap.InvokeAsync("getSessionsByProgCourse",
            [("userName", credentials.UserName), ("passWord", credentials.Password),
             ("option", OptionFor(query.Scope)),
             ("programID", filtered ? query.ProgramId : SessionQuery.All),
             ("courseID", filtered ? query.CourseId : SessionQuery.All)],
            ct).ConfigureAwait(false);
        return ParseSessions(result);
    }

    /// <summary>
    /// Legacy frmLogin compared the stored role string with the literals "tutor"/"student".
    /// That this role is the checkUserPassResult value is inferred, not observed at runtime.
    /// </summary>
    public static AuthResult InterpretCheckUserPass(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "tutor" => new AuthResult(true, UserRole.Tutor, raw),
        "student" => new AuthResult(true, UserRole.Student, raw),
        _ => new AuthResult(false, UserRole.Student, raw),
    };

    /// <summary>
    /// The service reports some errors as data: a single Session whose fields are all nil except
    /// sessionName (observed: "Invalid Option"). Such records become a <see cref="SvuServiceException"/>.
    /// </summary>
    public static IReadOnlyList<SessionInfo> ParseSessions(XElement result)
    {
        var sessions = Records(result, "sessionID")
            .Select(r => new SessionInfo(
                r.Get("sessionID"), r.Get("studentURL"), r.Get("sessionName"), r.Get("status"),
                r.Get("startDate"), r.Get("launchedDate"), r.Get("serverNumber"),
                r.Get("tutorID"), r.Get("tutorName"), r.Get("classID")))
            .ToList();

        var errors = sessions.Where(s => s.SessionId.Length == 0).ToList();
        if (errors.Count > 0 && errors.Count == sessions.Count)
            throw new SvuServiceException(errors[0].Name.Length > 0 ? errors[0].Name : "The service returned an empty session record.");
        return sessions.Where(s => s.SessionId.Length > 0).ToList();
    }

    private static string LeafText(XElement result) =>
        (result.Descendants().LastOrDefault(e => !e.HasElements) ?? result).Value;

    /// <summary>Finds array items by a key field; local names are matched case-insensitively like the VB6 LCase() parser.</summary>
    private static IEnumerable<Record> Records(XElement root, string keyField) =>
        root.Descendants()
            .Where(e => e.Elements().Any(c => Same(c.Name.LocalName, keyField)))
            .Select(e => new Record(e));

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private readonly record struct Record(XElement Element)
    {
        public string Get(string field) =>
            Element.Elements().FirstOrDefault(c => Same(c.Name.LocalName, field))?.Value ?? "";
    }
}
