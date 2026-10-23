using System.Globalization;
using SvuLauncher.Core;

namespace SvuLauncher.Infrastructure.Soap;

/// <summary>
/// Client for sessions.svuonline.org/WDemoServicestest/Service1.asmx (document/literal, tempuri.org).
/// At the time of analysis this host answered only over plain HTTP, so callers must opt in to
/// insecure transport explicitly when constructing the underlying <see cref="SoapClient"/>.
/// </summary>
public sealed class MeetingStatusClient(SoapClient soap) : IMeetingStatusService
{
    public const string Namespace = "http://tempuri.org/";
    public static readonly Uri LegacyEndpoint = new("http://sessions.svuonline.org/WDemoServicestest/Service1.asmx");

    public async Task<int> GetLaunchCountAsync(int serverId, string tutorId, string meetingId, CancellationToken ct)
    {
        var result = await soap.InvokeAsync("getMeetingLaunchCount",
            [("_serverId", serverId), ("_tutorId", tutorId), ("_meetingId", meetingId)], ct).ConfigureAwait(false);
        var text = result.Descendants().LastOrDefault(e => !e.HasElements)?.Value ?? result.Value;
        return int.Parse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture);
    }
}
