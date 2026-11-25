using PCOpti.Models;
using PCOpti.Services;
using PCOpti.Tweaks;

namespace PCOpti.Tests;

public class CatalogTests
{
    [Fact]
    public void EveryTweak_HasUniqueId_AndRealDescription()
    {
        var tweaks = TweakCatalog.Create(new TestEnv().Context);

        Assert.Equal(tweaks.Count, tweaks.Select(t => t.Id).Distinct().Count());
        Assert.All(tweaks, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Name));
            Assert.True(t.Description.Length > 60, $"{t.Id} needs a proper description");
        });
    }

    [Fact]
    public void RiskyTweaks_HaveWarnings()
    {
        var tweaks = TweakCatalog.Create(new TestEnv().Context);
        Assert.All(tweaks.Where(t => t.RiskLevel != RiskLevel.Safe), t => Assert.False(string.IsNullOrWhiteSpace(t.Warning)));
    }

    /// <summary>Every registry tweak must apply, detect itself, and revert to the exact original registry.</summary>
    [Fact]
    public void EveryRegistryTweak_RoundTripsToTheExactOriginalState()
    {
        var env = new TestEnv();
        // Pre-populate some "user" values so both the existed and not-existed paths are exercised.
        env.Registry.SetValue(RegHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", RegValue.DWord(1));
        env.Registry.SetValue(RegHive.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", RegValue.Str("1"));
        var before = env.Registry.Dump();

        foreach (var tweak in TweakCatalog.Create(env.Context).OfType<RegistryTweak>())
        {
            tweak.Apply();
            Assert.True(tweak.IsApplied(), $"{tweak.Id} not detected after apply");
            tweak.Revert();
            Assert.False(tweak.HasBackup, $"{tweak.Id} left a backup");
        }

        Assert.Equal(before, env.Registry.Dump());
    }

    [Fact]
    public void TweaksAreIndependent_RevertingOneDoesNotTouchAnother()
    {
        var env = new TestEnv();
        var tweaks = TweakCatalog.Create(env.Context).OfType<RegistryTweak>().ToList();
        var locations = tweaks.SelectMany(t => t.Settings.Select(s => (t.Id, s.Location.ToLowerInvariant()))).ToList();

        // No two tweaks may write the same registry value.
        Assert.Equal(locations.Count, locations.Select(l => l.Item2).Distinct().Count());
    }
}

public class BackupStoreTests
{
    [Fact]
    public void JsonStore_PersistsAcrossInstances_AndKeepsFirstBackup()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pcopti-test-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonBackupStore(path);
            Assert.True(store.TryAdd(new TweakBackup { Id = "x", State = { ["v"] = "original" } }));
            Assert.False(store.TryAdd(new TweakBackup { Id = "x", State = { ["v"] = "second" } }));

            var reloaded = new JsonBackupStore(path);
            Assert.Equal("original", reloaded.Get("x")!.State["v"]);

            reloaded.Remove("x");
            Assert.Null(new JsonBackupStore(path).Get("x"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void JsonStore_RegValuesSurviveSerialisation()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pcopti-test-{Guid.NewGuid():N}.json");
        try
        {
            var value = RegValue.Binary(0x90, 0x12, 0x03, 0x80);
            new JsonBackupStore(path).TryAdd(new TweakBackup
            {
                Id = "y",
                Registry = { new RegistrySnapshot { Hive = RegHive.CurrentUser, Path = "P", Name = "N", Existed = true, Value = value } },
            });
            Assert.Equal(value, new JsonBackupStore(path).Get("y")!.Registry[0].Value);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public class StartupServiceTests
{
    private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    [Fact]
    public void ApprovedFlag_EvenFirstByteIsEnabled()
    {
        Assert.True(StartupService.IsApprovedEnabled(null));
        Assert.True(StartupService.IsApprovedEnabled(RegValue.Binary(0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)));
        Assert.True(StartupService.IsApprovedEnabled(RegValue.Binary(0x06, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)));
        Assert.False(StartupService.IsApprovedEnabled(RegValue.Binary(0x03, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8)));
        Assert.False(StartupService.IsApprovedEnabled(StartupService.MakeApprovedValue(false, DateTime.UtcNow)));
        Assert.True(StartupService.IsApprovedEnabled(StartupService.MakeApprovedValue(true, DateTime.UtcNow)));
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\App\\app.exe\" --minimized", "C:\\Program Files\\App\\app.exe")]
    [InlineData("C:\\Tools\\tool.exe -silent", "C:\\Tools\\tool.exe")]
    [InlineData("C:\\Tools\\script.cmd", "C:\\Tools\\script.cmd")]
    public void ExtractExecutable_HandlesQuotesAndArguments(string command, string expected) =>
        Assert.Equal(expected, StartupService.ExtractExecutable(command));

    [Fact]
    public void Disable_NeverDeletesTheEntry_AndRestoreGoesBack()
    {
        var env = new TestEnv();
        env.Registry.SetValue(RegHive.CurrentUser, Run, "Chat", RegValue.Str("\"C:\\nope\\chat.exe\" -tray"));
        env.Commands.Handler = (_, _) => new CommandResult(0, "[]", ""); // no scheduled tasks
        var service = new StartupService(env.Registry, env.Commands, env.Backups, env.Log,
            Array.Empty<(string, StartupSource)>()); // don't read this machine's real Startup folders

        var entry = service.GetEntries().Single();
        Assert.True(entry.Enabled);

        service.SetEnabled(entry, false);
        Assert.False(service.GetEntries().Single().Enabled);
        Assert.NotNull(env.Registry.GetValue(RegHive.CurrentUser, Run, "Chat")); // the Run value is untouched

        service.Restore(env.Backups.Get(StartupService.BackupPrefix + entry.Id)!);
        Assert.True(service.GetEntries().Single().Enabled);
        Assert.Null(env.Registry.GetValue(RegHive.CurrentUser, Approved, "Chat")); // flag removed again, as before
    }
}

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("1.4", 1, 4, 0)]
    [InlineData("v2.0.0-beta.1", 2, 0, 0)]
    [InlineData("V3", 3, 0, 0)]
    public void ParseVersion_HandlesCommonTagFormats(string tag, int major, int minor, int build) =>
        Assert.Equal(new Version(major, minor, build), UpdateChecker.ParseVersion(tag));

    [Fact]
    public void ParseVersion_RejectsGarbage() => Assert.Null(UpdateChecker.ParseVersion("latest"));
}
