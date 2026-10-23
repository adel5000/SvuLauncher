using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SvuLauncher.App.Resources;

namespace SvuLauncher.Tests;

public class ResourceTests
{
    private static readonly string ResourceDir = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "SvuLauncher.App", "Resources"));

    private static Dictionary<string, string> Load(string file) =>
        XDocument.Load(Path.Combine(ResourceDir, file)).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")!.Value);

    private static IEnumerable<string> PropertyKeys() =>
        typeof(Strings).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => p.Name);

    [Fact]
    public void English_and_Arabic_files_have_the_same_keys_as_the_Strings_class()
    {
        var english = Load("Strings.resx");
        var arabic = Load("Strings.ar.resx");
        var keys = PropertyKeys().Order().ToList();

        Assert.Equal(keys, english.Keys.Order());
        Assert.Equal(keys, arabic.Keys.Order());
        Assert.All(arabic.Values, v => Assert.False(string.IsNullOrWhiteSpace(v)));
    }

    [Fact]
    public void Format_placeholders_match_between_languages()
    {
        var english = Load("Strings.resx");
        var arabic = Load("Strings.ar.resx");
        foreach (var (key, value) in english)
        {
            static IEnumerable<string> Holes(string s) => Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).Distinct().Order();
            Assert.True(Holes(value).SequenceEqual(Holes(arabic[key])), $"Placeholder mismatch in {key}");
        }
    }

    [Fact]
    public void Arabic_satellite_assembly_is_used_for_ar_culture()
    {
        var arabic = Strings.ResourceManager.GetString(nameof(Strings.Login_SignIn), CultureInfo.GetCultureInfo("ar"));
        var english = Strings.ResourceManager.GetString(nameof(Strings.Login_SignIn), CultureInfo.GetCultureInfo("en"));
        Assert.Equal("تسجيل الدخول", arabic);
        Assert.Equal("Sign in", english);
    }
}
