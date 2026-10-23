using System.Text.Json;
using Microsoft.Extensions.Logging;
using SvuLauncher.Core;
using SvuLauncher.Infrastructure.Legacy;
using SvuLauncher.Infrastructure.Security;
using SvuLauncher.Infrastructure.Soap;

namespace SvuLauncher.Infrastructure;

/// <summary>Contents of svulauncher.json, shared by the CLI and WPF hosts.</summary>
public sealed record LauncherSettings(
    string JavaBinDirectory,
    string? LinkJvmSha256 = null,
    int Port = 443,
    string Language = "EN",
    bool EnableMeetingStatusCheck = false,
    bool AllowInsecureMeetingStatusHttp = false,
    bool RequireHostStarted = false,
    string? UiLanguage = null)
{
    public static LauncherSettings Load(string path) =>
        JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(path))
        ?? throw new InvalidDataException($"{path} is empty.");

    public LaunchPreferences ToPreferences() => new(Port, Language, RequireHostStarted);
}

/// <summary>Composition root for the production object graph.</summary>
public sealed class LauncherServices
{
    public required SvuisDirectoryClient Directory { get; init; }
    public required ICredentialVault Vault { get; init; }
    public required LaunchOrchestrator Orchestrator { get; init; }
    public required LaunchPreferences Preferences { get; init; }

    public static LauncherServices Create(
        LauncherSettings settings, HttpClient http, ILoggerFactory loggerFactory, IMicrophoneService? microphone = null)
    {
        var directory = new SvuisDirectoryClient(
            new SoapClient(http, SvuisDirectoryClient.Endpoint, SvuisDirectoryClient.Namespace, SoapStyle.RpcEncoded));
        IMeetingStatusService? meetingStatus = settings.EnableMeetingStatusCheck
            ? new MeetingStatusClient(new SoapClient(http, MeetingStatusClient.LegacyEndpoint, MeetingStatusClient.Namespace,
                SoapStyle.DocumentLiteral, allowInsecureHttp: settings.AllowInsecureMeetingStatusHttp))
            : null;
        var client = new LegacyLinktivityClient(
            new LegacyClientOptions { JavaBinDirectory = settings.JavaBinDirectory, LinkJvmSha256 = settings.LinkJvmSha256 },
            loggerFactory.CreateLogger<LegacyLinktivityClient>());

        return new LauncherServices
        {
            Directory = directory,
            Vault = new DpapiCredentialVault(DpapiCredentialVault.DefaultPath),
            Orchestrator = new LaunchOrchestrator(
                directory, client, loggerFactory.CreateLogger<LaunchOrchestrator>(), meetingStatus, microphone),
            Preferences = settings.ToPreferences(),
        };
    }
}
