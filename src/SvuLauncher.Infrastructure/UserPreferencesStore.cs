using System.Text.Json;
using SvuLauncher.Core;

namespace SvuLauncher.Infrastructure;

/// <summary>Stores <see cref="UserPreferences"/> as JSON under %LOCALAPPDATA%\SvuLauncher.</summary>
public sealed class UserPreferencesStore(string filePath) : IUserPreferencesStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SvuLauncher", "preferences.json");

    public UserPreferences Load()
    {
        try
        {
            return File.Exists(filePath)
                ? JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(filePath)) ?? new UserPreferences()
                : new UserPreferences();
        }
        catch (JsonException)
        {
            // A hand-edited or truncated file should not stop the app from starting.
            return new UserPreferences();
        }
    }

    public void Save(UserPreferences preferences)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var temp = filePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(preferences, Json));
        File.Move(temp, filePath, overwrite: true);
    }
}
