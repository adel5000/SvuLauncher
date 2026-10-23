using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SvuLauncher.Core;

namespace SvuLauncher.Infrastructure.Legacy;

public sealed record LegacyClientOptions
{
    /// <summary>Directory holding java.exe, linkjvm.jar, linktivity.properties and the lk*.dll natives.</summary>
    public required string JavaBinDirectory { get; init; }

    /// <summary>Optional pinned SHA-256 (hex) of linkjvm.jar; null disables the check.</summary>
    public string? LinkJvmSha256 { get; init; }

    public IReadOnlyList<string> JvmOptions { get; init; } = [];
}

/// <summary>
/// Adapter that runs the unmodified Linktivity WebDemo 4.0.1.7 client (LinkJvm) in a 32-bit JVM.
/// It keeps the legacy client working while it is isolated behind <see cref="ISessionClient"/>.
/// </summary>
public sealed class LegacyLinktivityClient(LegacyClientOptions options, ILogger<LegacyLinktivityClient> log) : ISessionClient
{
    // LinkJvm.m_userDlls: LinkJvm.run() shows INVALID_INSTALL if any of these are missing.
    internal static readonly string[] RequiredFiles =
    [
        "linkjvm.jar", "linktivity.properties",
        "NativeSupport.dll", "lkappsel.dll", "webdemvp.dll", "lkhost.dll", "lkvoice.dll", "lkvideo.dll",
        "cobrowse.dll", "lkwhtbrd.dll", "wdftuser.dll", "wdfthost.dll", "lkexport.dll",
    ];

    private const ushort ImageFileMachineI386 = 0x014c;

    public Task<ISessionHandle> StartAsync(LaunchTicket ticket, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var bin = options.JavaBinDirectory;
        var java = Path.Combine(bin, "java.exe");
        Preflight(bin, java);

        var psi = new ProcessStartInfo(java)
        {
            // LinkJvm finds linktivity.properties/config.hash/DLLs relative to the working directory.
            WorkingDirectory = bin,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in LinkJvmCommand.BuildArguments(ticket, options.JvmOptions))
            psi.ArgumentList.Add(arg);

        var process = Process.Start(psi) ?? throw new InvalidOperationException("java.exe did not start.");
        log.LogInformation("Started Linktivity client pid {Pid}", process.Id);
        return Task.FromResult<ISessionHandle>(new ProcessSessionHandle(process, log));
    }

    internal void Preflight(string bin, string java)
    {
        if (!File.Exists(java))
            throw new FileNotFoundException("Bundled java.exe not found.", java);
        var missing = RequiredFiles.Where(f => !File.Exists(Path.Combine(bin, f))).ToList();
        if (missing.Count > 0)
            throw new FileNotFoundException($"Legacy client install is incomplete; missing: {string.Join(", ", missing)}");

        // The lk*.dll JNI libraries are 32-bit; a 64-bit JVM cannot System.load() them.
        var machine = ReadPeMachine(java);
        if (machine != ImageFileMachineI386)
            throw new InvalidOperationException($"java.exe must be a 32-bit (x86) JVM; PE machine is 0x{machine:x4}.");

        if (options.LinkJvmSha256 is { } pinned)
        {
            using var stream = File.OpenRead(Path.Combine(bin, "linkjvm.jar"));
            var actual = Convert.ToHexString(SHA256.HashData(stream));
            if (!actual.Equals(pinned, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("linkjvm.jar does not match the pinned SHA-256.");
        }
    }

    internal static ushort ReadPeMachine(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        if (reader.ReadUInt16() != 0x5A4D)
            throw new InvalidDataException($"{path} is not a PE file.");
        reader.BaseStream.Position = 0x3C;
        reader.BaseStream.Position = reader.ReadInt32();
        if (reader.ReadUInt32() != 0x00004550)
            throw new InvalidDataException($"{path} has no PE signature.");
        return reader.ReadUInt16();
    }

    private sealed class ProcessSessionHandle : ISessionHandle
    {
        private readonly Process _process;

        public ProcessSessionHandle(Process process, ILogger log)
        {
            _process = process;
            // LinkJvm prints every argv entry ("n: value") and IPC traffic to stdout; keep it at Debug.
            _process.OutputDataReceived += (_, e) => { if (e.Data is not null) log.LogDebug("linkjvm: {Line}", e.Data); };
            _process.ErrorDataReceived += (_, e) => { if (e.Data is not null) log.LogWarning("linkjvm: {Line}", e.Data); };
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            Completion = WaitAsync(log);
        }

        public int ProcessId => _process.Id;
        public Task<int> Completion { get; }

        private async Task<int> WaitAsync(ILogger log)
        {
            await _process.WaitForExitAsync().ConfigureAwait(false);
            var code = _process.ExitCode;
            if (code == LinkJvmCommand.BadArgumentsExitCode)
                log.LogError("LinkJvm rejected its arguments (exit code 87)");
            else
                log.LogInformation("Linktivity client exited with code {Code}", code);
            return code;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
            await Completion.ConfigureAwait(false);
            _process.Dispose();
        }
    }
}
