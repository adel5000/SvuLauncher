using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

namespace SvuLauncher.Infrastructure.Soap;

public enum SoapStyle
{
    /// <summary>RPC/encoded (svuis NewSessions2.php, NuSOAP-style).</summary>
    RpcEncoded,
    /// <summary>Document/literal (.asmx on sessions.svuonline.org).</summary>
    DocumentLiteral,
}

public sealed class SoapFaultException(string code, string reason) : Exception($"SOAP fault {code}: {reason}")
{
    public string Code { get; } = code;
}

/// <summary>An error the service returned inside a normal (non-fault) response.</summary>
public sealed class SvuServiceException(string message) : Exception(message);

/// <summary>
/// Minimal SOAP 1.1 client. Replaces the retired MS SOAP Toolkit 3.0 (mssoap30.dll).
/// Unlike the legacy launcher it never downgrades from HTTPS to HTTP on failure.
/// </summary>
public sealed class SoapClient
{
    internal static readonly XNamespace Env = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Enc = "http://schemas.xmlsoap.org/soap/encoding/";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace Xsd = "http://www.w3.org/2001/XMLSchema";

    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly XNamespace _ns;
    private readonly SoapStyle _style;

    public SoapClient(HttpClient http, Uri endpoint, string serviceNamespace, SoapStyle style, bool allowInsecureHttp = false)
    {
        if (endpoint.Scheme != Uri.UriSchemeHttps && !allowInsecureHttp)
            throw new ArgumentException($"Refusing non-HTTPS SOAP endpoint {endpoint}.", nameof(endpoint));
        _http = http;
        _endpoint = endpoint;
        _ns = serviceNamespace;
        _style = style;
    }

    public async Task<XElement> InvokeAsync(string operation, IReadOnlyList<(string Name, object Value)> parts, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(BuildEnvelope(operation, parts).ToString(SaveOptions.DisableFormatting),
                Encoding.UTF8, "text/xml"),
        };
        request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{SoapAction(operation)}\"");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        // SOAP 1.1 faults arrive with HTTP 500, so parse before checking the status.
        XDocument doc;
        try
        {
            doc = XDocument.Parse(body);
        }
        catch (System.Xml.XmlException ex)
        {
            response.EnsureSuccessStatusCode();
            throw new InvalidDataException($"{operation}: response is not XML.", ex);
        }
        return ExtractResult(doc, operation);
    }

    internal string SoapAction(string operation)
    {
        var ns = _ns.NamespaceName;
        return ns.EndsWith('/') ? ns + operation : ns + "/" + operation;
    }

    internal XDocument BuildEnvelope(string operation, IReadOnlyList<(string Name, object Value)> parts)
    {
        XElement call;
        if (_style == SoapStyle.RpcEncoded)
        {
            call = new XElement(_ns + operation,
                parts.Select(p => new XElement(p.Name, new XAttribute(Xsi + "type", XsdType(p.Value)), Format(p.Value))));
        }
        else
        {
            call = new XElement(_ns + operation,
                parts.Select(p => new XElement(_ns + p.Name, Format(p.Value))));
        }

        var bodyEl = new XElement(Env + "Body", call);
        if (_style == SoapStyle.RpcEncoded)
            bodyEl.Add(new XAttribute(Env + "encodingStyle", Enc.NamespaceName));

        return new XDocument(new XElement(Env + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soap", Env.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsd", Xsd.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "tns", _ns.NamespaceName),
            bodyEl));
    }

    internal static XElement ExtractResult(XDocument doc, string operation)
    {
        var body = doc.Root?.Element(Env + "Body") ?? throw new InvalidDataException($"{operation}: no SOAP Body.");
        var fault = body.Element(Env + "Fault");
        if (fault is not null)
            throw new SoapFaultException(
                fault.Element("faultcode")?.Value ?? "unknown",
                fault.Element("faultstring")?.Value ?? "");
        return body.Elements().FirstOrDefault() ?? throw new InvalidDataException($"{operation}: empty SOAP Body.");
    }

    private static string Format(object value) => value switch
    {
        string s => s,
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    private static string XsdType(object value) => value switch
    {
        int or long or short => "xsd:integer",
        bool => "xsd:boolean",
        _ => "xsd:string",
    };
}
