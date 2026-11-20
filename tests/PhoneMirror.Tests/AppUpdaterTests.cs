using PhoneMirror.Core;
using Velopack.Sources;

namespace PhoneMirror.Tests;

public class AppUpdaterTests
{
    [Theory]
    [InlineData("https://github.com/fauveauq/phone-mirror", typeof(GithubSource))]
    [InlineData("https://intranet.example.com/phone-mirror/", typeof(SimpleWebSource))]
    [InlineData(@"\\serveur\partage\PhoneMirror", typeof(SimpleFileSource))]
    [InlineData(@"C:\Mises a jour\PhoneMirror", typeof(SimpleFileSource))]
    public void CreateSource_picks_the_source_type_from_the_address(string address, Type expected)
    {
        Assert.IsType(expected, AppUpdater.CreateSource(address));
    }

    [Fact]
    public void Blank_update_source_falls_back_to_default()
    {
        Assert.Null(new UserSettings { UpdateSource = "  " }.Normalized().UpdateSource);
    }
}
