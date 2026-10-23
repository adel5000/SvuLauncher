using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using SvuLauncher.Core;

namespace SvuLauncher.Infrastructure.Security;

/// <summary>
/// Stores "remember me" credentials encrypted with DPAPI (current user scope).
/// Replaces the legacy SaveSetting(..., "Auth", "U"/"P") plaintext registry values.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiCredentialVault(string filePath) : ICredentialVault
{
    private static readonly byte[] Entropy = "SvuLauncher.Credentials.v1"u8.ToArray();

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SvuLauncher", "credentials.bin");

    public void Save(Credentials credentials)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(new Stored(credentials.UserName, credentials.Password));
        try
        {
            var cipher = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            var temp = filePath + ".tmp";
            File.WriteAllBytes(temp, cipher);
            File.Move(temp, filePath, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    public Credentials? TryLoad()
    {
        if (!File.Exists(filePath))
            return null;
        try
        {
            var plain = ProtectedData.Unprotect(File.ReadAllBytes(filePath), Entropy, DataProtectionScope.CurrentUser);
            var stored = JsonSerializer.Deserialize<Stored>(plain);
            CryptographicOperations.ZeroMemory(plain);
            return stored is null ? null : new Credentials(stored.U, stored.P);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            // Corrupt or foreign-user blob: discard it rather than fail sign-in.
            Clear();
            return null;
        }
    }

    public void Clear()
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    private sealed record Stored(string U, string P);
}
