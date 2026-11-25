using PCOpti.Models;
using PCOpti.Services;
using PCOpti.Tweaks;
using PCOpti.Tweaks.SystemServices;

namespace PCOpti.Tests;

public class ServiceTweakTests
{
    private const string MapsKey = @"SYSTEM\CurrentControlSet\Services\MapsBroker";

    private static TestEnv EnvWithMaps(int start, bool delayed)
    {
        var env = new TestEnv();
        env.Registry.SetValue(RegHive.LocalMachine, MapsKey, "Start", RegValue.DWord(start));
        env.Registry.SetValue(RegHive.LocalMachine, MapsKey, "DelayedAutostart", RegValue.DWord(delayed ? 1 : 0));
        // Simulate sc.exe updating the registry, as the real Service Control Manager does.
        env.Commands.Handler = (exe, args) =>
        {
            if (exe == "sc.exe")
            {
                var value = args.Split("start= ")[1];
                var startType = value switch { "auto" or "delayed-auto" => 2, "disabled" => 4, _ => 3 };
                env.Registry.SetValue(RegHive.LocalMachine, MapsKey, "Start", RegValue.DWord(startType));
                env.Registry.SetValue(RegHive.LocalMachine, MapsKey, "DelayedAutostart", RegValue.DWord(value == "delayed-auto" ? 1 : 0));
            }
            return new CommandResult(0, "", "");
        };
        return env;
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void IsApplied_ReflectsStartType(int start, bool expected)
    {
        var env = EnvWithMaps(start, false);
        Assert.Equal(expected, new MapsBrokerServiceTweak(env.Context).IsApplied());
    }

    [Fact]
    public void Apply_SetsManual_AndRevert_RestoresDelayedAutomatic()
    {
        var env = EnvWithMaps(2, delayed: true);
        var tweak = new MapsBrokerServiceTweak(env.Context);

        tweak.Apply();
        Assert.Contains("sc.exe config \"MapsBroker\" start= demand", env.Commands.Calls);
        Assert.True(tweak.IsApplied());
        Assert.Equal("2", env.Backups.Get(tweak.Id)!.State["Start"]);

        tweak.Revert();
        Assert.Contains("sc.exe config \"MapsBroker\" start= delayed-auto", env.Commands.Calls);
        Assert.False(tweak.IsApplied());
        Assert.Null(env.Backups.Get(tweak.Id));
    }

    [Fact]
    public void Apply_WhenAlreadyManual_DoesNothing()
    {
        var env = EnvWithMaps(3, false);
        var tweak = new MapsBrokerServiceTweak(env.Context);

        tweak.Apply();

        Assert.Empty(env.Commands.Calls);
        Assert.Null(env.Backups.Get(tweak.Id));
    }

    [Fact]
    public void Apply_AccessDenied_ThrowsClearError_AndLeavesNoBackup()
    {
        var env = EnvWithMaps(2, false);
        env.Commands.Handler = (_, _) => new CommandResult(5, "[SC] OpenService FAILED 5: Access is denied.", "");
        var tweak = new MapsBrokerServiceTweak(env.Context);

        var ex = Assert.Throws<TweakException>(tweak.Apply);

        Assert.Contains("Access denied", ex.Message);
        Assert.Null(env.Backups.Get(tweak.Id));
    }

    [Fact]
    public void MissingService_IsReportedUnavailable()
    {
        var env = new TestEnv();
        Assert.NotNull(new FaxServiceTweak(env.Context).GetUnavailableReason());
    }

    [Fact]
    public void Revert_WithoutBackup_UsesServiceDefault()
    {
        var env = new TestEnv();
        env.Registry.SetValue(RegHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\RemoteRegistry", "Start", RegValue.DWord(3));
        new RemoteRegistryServiceTweak(env.Context).Revert();
        Assert.Contains("sc.exe config \"RemoteRegistry\" start= disabled", env.Commands.Calls);
    }

    [Theory]
    [InlineData(2, false, "auto")]
    [InlineData(2, true, "delayed-auto")]
    [InlineData(3, false, "demand")]
    [InlineData(4, false, "disabled")]
    public void ScStartArgument_MapsStartTypes(int start, bool delayed, string expected) =>
        Assert.Equal(expected, ServiceTweak.ScStartArgument(start, delayed));
}
