using System.Net;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using SvuLauncher.Core;
using SvuLauncher.Infrastructure;
using SvuLauncher.Infrastructure.Audio;
using SvuLauncher.Infrastructure.Legacy;
using SvuLauncher.Infrastructure.Security;
using SvuLauncher.Infrastructure.Soap;

namespace SvuLauncher.Tests;

public class SoapClientTests
{
    [Fact]
    public void Refuses_plain_http_by_default() =>
        Assert.Throws<ArgumentException>(() => new SoapClient(new HttpClient(),
            new Uri("http://svuis.svuonline.org/x.php"), SvuisDirectoryClient.Namespace, SoapStyle.RpcEncoded));

    [Fact]
    public async Task Sends_rpc_encoded_envelope_with_soap_action()
    {
        var handler = new CaptureHandler("""
            <SOAP-ENV:Envelope xmlns:SOAP-ENV="http://schemas.xmlsoap.org/soap/envelope/">
              <SOAP-ENV:Body><ns1:checkUserPassResponse xmlns:ns1="https://svuis.svuonline.org/SVUIS/WebDemo_wservice/">
                <checkUserPassResult>tutor</checkUserPassResult>
              </ns1:checkUserPassResponse></SOAP-ENV:Body></SOAP-ENV:Envelope>
            """);
        var client = new SvuisDirectoryClient(new SoapClient(new HttpClient(handler),
            SvuisDirectoryClient.Endpoint, SvuisDirectoryClient.Namespace, SoapStyle.RpcEncoded));

        var result = await client.AuthenticateAsync(new Credentials("u1", "p&1"), default);

        Assert.True(result.Success);
        Assert.Equal(UserRole.Tutor, result.Role);
        Assert.Equal("\"https://svuis.svuonline.org/SVUIS/WebDemo_wservice/checkUserPass\"", handler.SoapAction);
        var call = XDocument.Parse(handler.Body!).Descendants().Single(e => e.Name.LocalName == "checkUserPass");
        Assert.Equal("p&1", call.Element("passWord")!.Value);
    }

    [Fact]
    public async Task Surfaces_soap_faults()
    {
        var handler = new CaptureHandler("""
            <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/"><s:Body><s:Fault>
              <faultcode>s:Server</faultcode><faultstring>boom</faultstring></s:Fault></s:Body></s:Envelope>
            """, HttpStatusCode.InternalServerError);
        var soap = new SoapClient(new HttpClient(handler), SvuisDirectoryClient.Endpoint, SvuisDirectoryClient.Namespace, SoapStyle.RpcEncoded);
        var ex = await Assert.ThrowsAsync<SoapFaultException>(() => soap.InvokeAsync("getPrograms", [], default));
        Assert.Equal("s:Server", ex.Code);
    }

    [Fact]
    public void Parses_encoded_session_array()
    {
        // Synthetic NuSOAP-style response shaped after tns:SessionArray in the live WSDL.
        var body = XElement.Parse("""
            <ns1:getSessionsByProgCourseResponse xmlns:ns1="urn:x" xmlns:SOAP-ENC="http://schemas.xmlsoap.org/soap/encoding/">
              <return SOAP-ENC:arrayType="ns1:Session[2]">
                <item><sessionID>900</sessionID><studentURL>http://h/demo?linkid=55</studentURL><sessionName>A</sessionName>
                  <status>Open</status><startDate>d</startDate><launchedDate/><serverNumber>3</serverNumber>
                  <tutorID>T1</tutorID><tutorName>N</tutorName><classID>C1</classID></item>
                <item><SESSIONID>901</SESSIONID><studentURL>http://h/demo?linkid=56</studentURL></item>
              </return>
            </ns1:getSessionsByProgCourseResponse>
            """);
        var sessions = SvuisDirectoryClient.ParseSessions(body);
        Assert.Equal(["900", "901"], sessions.Select(s => s.SessionId));
        Assert.Equal("3", sessions[0].ServerNumber);
        Assert.Equal("", sessions[1].TutorId);
    }

    [Fact]
    public void Error_record_returned_as_data_becomes_an_exception()
    {
        // Shape observed live from getTodaySessions(option=2) on 2026-10-19.
        var body = XElement.Parse("""
            <ns1:getTodaySessionsResponse xmlns:ns1="https://svuis.svuonline.org/SVUIS/WebDemo_wservice/"
                xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:SOAP-ENC="http://schemas.xmlsoap.org/soap/encoding/">
              <return SOAP-ENC:arrayType="ns1:Session[1]" xsi:type="ns1:SessionArray">
                <item xsi:type="ns1:Session">
                  <sessionID xsi:nil="true" /><studentURL xsi:nil="true" />
                  <sessionName xsi:type="xsd:string">Invalid Option</sessionName>
                  <status xsi:nil="true" /><startDate xsi:nil="true" /><launchedDate xsi:nil="true" />
                  <serverNumber xsi:nil="true" /><tutorID xsi:nil="true" /><tutorName xsi:nil="true" /><classID xsi:nil="true" />
                </item>
              </return>
            </ns1:getTodaySessionsResponse>
            """);
        var ex = Assert.Throws<SvuServiceException>(() => SvuisDirectoryClient.ParseSessions(body));
        Assert.Equal("Invalid Option", ex.Message);
    }

    [Theory]
    [InlineData(SessionScope.MyClasses, "1", "0", "0")]
    [InlineData(SessionScope.AllMyCourseClasses, "2", "0", "0")]
    [InlineData(SessionScope.ByProgramAndCourse, "3", "53", "1426")]
    public async Task Session_query_sends_legacy_five_argument_call(SessionScope scope, string option, string program, string course)
    {
        var handler = new CaptureHandler("""
            <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/"><s:Body>
              <ns1:getSessionsByProgCourseResponse xmlns:ns1="urn:x"><return /></ns1:getSessionsByProgCourseResponse>
            </s:Body></s:Envelope>
            """);
        var client = new SvuisDirectoryClient(new SoapClient(new HttpClient(handler),
            SvuisDirectoryClient.Endpoint, SvuisDirectoryClient.Namespace, SoapStyle.RpcEncoded));

        await client.GetSessionsAsync(new Credentials("u", "p"), new SessionQuery(scope, 53, 1426), default);

        var call = XDocument.Parse(handler.Body!).Descendants().Single(e => e.Name.LocalName == "getSessionsByProgCourse");
        Assert.Equal(["userName", "passWord", "option", "programID", "courseID"], call.Elements().Select(e => e.Name.LocalName));
        // Program and course are sent only for the by-program scope, as the legacy UI disables them otherwise.
        Assert.Equal(["u", "p", option, program, course], call.Elements().Select(e => e.Value));
    }

    [Theory]
    [InlineData("student", true, UserRole.Student)]
    [InlineData(" Tutor ", true, UserRole.Tutor)]
    [InlineData("0", false, UserRole.Student)]
    public void Interprets_check_user_pass(string raw, bool ok, UserRole role)
    {
        var r = SvuisDirectoryClient.InterpretCheckUserPass(raw);
        Assert.Equal(ok, r.Success);
        Assert.Equal(role, r.Role);
    }

    private sealed class CaptureHandler(string response, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? SoapAction { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            SoapAction = request.Headers.GetValues("SOAPAction").Single();
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(response, Encoding.UTF8, "text/xml") };
        }
    }
}

public class UserPreferencesStoreTests
{
    [Fact]
    public void Round_trips_and_tolerates_missing_or_corrupt_files()
    {
        var path = Path.Combine(Path.GetTempPath(), $"svu-prefs-{Guid.NewGuid():N}.json");
        var store = new UserPreferencesStore(path);
        try
        {
            Assert.Equal(new UserPreferences(), store.Load());

            store.Save(new UserPreferences("ar", "Dark", MuteMicrophoneOnLaunch: true));
            Assert.Equal(new UserPreferences("ar", "Dark", true), store.Load());

            File.WriteAllText(path, "{ not json");
            Assert.Equal(new UserPreferences(), store.Load());
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public class CoreAudioMicrophoneServiceTests
{
    [Fact]
    public void Reads_the_default_capture_device_without_changing_it()
    {
        // Read-only smoke test against the real machine; a machine without a microphone yields null.
        using var microphone = new CoreAudioMicrophoneService();
        var state = microphone.GetState();
        if (state is not null)
        {
            Assert.InRange(state.Level, 0f, 1f);
            Assert.Equal(state, microphone.GetState());
        }
    }
}

public class DpapiCredentialVaultTests
{
    [Fact]
    public void Round_trips_and_does_not_store_plaintext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"svu-vault-{Guid.NewGuid():N}.bin");
        var vault = new DpapiCredentialVault(path);
        try
        {
            vault.Save(new Credentials("alice", "S3cret-Pa55"));
            Assert.DoesNotContain("S3cret-Pa55", Encoding.UTF8.GetString(File.ReadAllBytes(path)));
            Assert.Equal(new Credentials("alice", "S3cret-Pa55"), vault.TryLoad());
            vault.Clear();
            Assert.Null(vault.TryLoad());
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public class LegacyLinktivityClientTests
{
    private const string InstalledBin = @"C:\Program Files (x86)\SVU\SVUSessionsLauncher\SvuSessionLauncher\Java\bin";

    [Fact]
    public void Preflight_reports_missing_files()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var client = new LegacyLinktivityClient(new LegacyClientOptions { JavaBinDirectory = dir },
                NullLogger<LegacyLinktivityClient>.Instance);
            File.Copy(Environment.ProcessPath!, Path.Combine(dir, "java.exe"));
            var ex = Assert.Throws<FileNotFoundException>(() => client.Preflight(dir, Path.Combine(dir, "java.exe")));
            Assert.Contains("linkjvm.jar", ex.Message);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Installed_legacy_client_passes_preflight_when_present()
    {
        if (!Directory.Exists(InstalledBin))
            return; // only meaningful on a machine with the legacy launcher installed
        var client = new LegacyLinktivityClient(
            new LegacyClientOptions
            {
                JavaBinDirectory = InstalledBin,
                LinkJvmSha256 = "DC2E3D728862DF7D3A1877D8282D28A7FBA4726F5915D6C2A264FF259C3E173A",
            },
            NullLogger<LegacyLinktivityClient>.Instance);
        client.Preflight(InstalledBin, Path.Combine(InstalledBin, "java.exe"));
        Assert.Equal(0x014c, LegacyLinktivityClient.ReadPeMachine(Path.Combine(InstalledBin, "java.exe")));
    }
}
