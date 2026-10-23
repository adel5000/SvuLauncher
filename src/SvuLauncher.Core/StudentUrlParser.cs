using System.Globalization;

namespace SvuLauncher.Core;

/// <summary>
/// Extracts host, linkid and sessp from Session.studentURL.
/// Legacy (frmMain response parser, sub_0043cca0) located "linkid=" / "sessp=" with InStr and
/// cut the host before "/de"; this parser uses real URI/query parsing instead.
/// </summary>
public static class StudentUrlParser
{
    public static StudentUrlInfo Parse(string studentUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentUrl);
        var text = studentUrl.Trim();
        if (!text.Contains("://", StringComparison.Ordinal))
            text = "http://" + text;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
            throw new FormatException("studentURL is not an absolute URL.");

        var query = ParseQuery(uri.Query);
        if (!query.TryGetValue("linkid", out var rawLinkId)
            || !long.TryParse(rawLinkId, NumberStyles.None, CultureInfo.InvariantCulture, out var linkId))
            throw new FormatException("studentURL has no numeric linkid parameter.");

        query.TryGetValue("sessp", out var passcode);
        return new StudentUrlInfo(uri.Authority, linkId, string.IsNullOrEmpty(passcode) ? null : passcode);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            var key = Uri.UnescapeDataString(eq < 0 ? pair : pair[..eq]);
            var value = eq < 0 ? "" : Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' '));
            result.TryAdd(key, value);
        }
        return result;
    }
}
