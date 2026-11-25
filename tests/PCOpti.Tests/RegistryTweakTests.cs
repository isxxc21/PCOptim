using PCOpti.Models;
using PCOpti.Tweaks;
using PCOpti.Tweaks.Gaming;

namespace PCOpti.Tests;

public class RegistryTweakTests
{
    private const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>Two-value tweak used to test the base class on its own.</summary>
    private sealed class SampleTweak : RegistryTweak
    {
        public SampleTweak(TweakContext c) : base(c) { }
        public override string Id => "test.sample";
        public override string Name => "Sample";
        public override string Description => "Test";
        public override TweakCategory Category => TweakCategory.Gaming;
        public override RiskLevel RiskLevel => RiskLevel.Safe;
        public override IReadOnlyList<RegistrySetting> Settings { get; } = new[]
        {
            new RegistrySetting(RegHive.CurrentUser, @"Software\Test", "A", RegValue.DWord(0), RegValue.DWord(1)),
            new RegistrySetting(RegHive.CurrentUser, @"Software\Test", "B", RegValue.Str("off"), null),
        };
    }

    [Fact]
    public void IsApplied_IsFalse_WhenAnyValueDiffers()
    {
        var env = new TestEnv();
        env.Registry.SetValue(RegHive.CurrentUser, @"Software\Test", "A", RegValue.DWord(0));
        env.Registry.SetValue(RegHive.CurrentUser, @"Software\Test", "B", RegValue.Str("on"));

        Assert.False(new SampleTweak(env.Context).IsApplied());
    }

    [Fact]
    public void IsApplied_DetectsRealState_EvenWithoutBackup()
    {
        var env = new TestEnv();
        env.Registry.SetValue(RegHive.CurrentUser, @"Software\Test", "A", RegValue.DWord(0));
        env.Registry.SetValue(RegHive.CurrentUser, @"Software\Test", "B", RegValue.Str("off"));

        var tweak = new SampleTweak(env.Context);
        Assert.True(tweak.IsApplied());
        Assert.False(tweak.HasBackup);
    }

    [Fact]
    public void Apply_WritesValues_BacksUpOriginals_AndLogs()
    {
        var env = new TestEnv();
        env.Registry.SetValue(RegHive.CurrentUser, @"Software\Test", "A", RegValue.DWord(1));
        var tweak = new SampleTweak(env.Context);

        tweak.Apply();

        Assert.True(tweak.IsApplied());
        var backup = env.Backups.Get("test.sample")!;
        var a = backup.Registry.Single(r => r.Name == "A");
        var b = backup.Registry.Single(r => r.Name == "B");
        Assert.True(a.Existed);
        Assert.Equal(RegValue.DWord(1), a.Value);
        Assert.False(b.Existed);
        Assert.Contains(env.Log.GetAll(), e => e.Source == "test.sample" && e.Action == ChangeAction.Applied);
    }

    [Fact]
    public void Revert_RestoresExactOriginalState()
    {
        var env = new TestEnv();
        env.Registry.SetValue(RegHive.CurrentUser, @"Software\Test", "A", RegValue.DWord(7));
        var before = env.Registry.Dump();
        var tweak = new SampleTweak(env.Context);

        tweak.Apply();
        tweak.Revert();

        Assert.Equal(before, env.Registry.Dump());       // "B" was deleted again, "A" is back to 7
        Assert.Null(env.Backups.Get("test.sample"));
        Assert.False(tweak.IsApplied());
    }

    [Fact]
    public void ApplyingTwice_KeepsTheFirstBackup()
    {
        var env = new TestEnv();
        env.Registry.SetValue(RegHive.CurrentUser, @"Software\Test", "A", RegValue.DWord(5));
        var tweak = new SampleTweak(env.Context);

        tweak.Apply();
        tweak.Apply(); // second snapshot would see the applied values; must not replace the original
        tweak.Revert();

        Assert.Equal(RegValue.DWord(5), env.Registry.GetValue(RegHive.CurrentUser, @"Software\Test", "A"));
    }

    [Fact]
    public void Revert_WithoutBackup_UsesRevertToValue_OrDeletes()
    {
        var env = new TestEnv();
        env.Registry.SetValue(RegHive.CurrentUser, @"Software\Test", "A", RegValue.DWord(0));
        env.Registry.SetValue(RegHive.CurrentUser, @"Software\Test", "B", RegValue.Str("off"));
        var tweak = new SampleTweak(env.Context);

        tweak.Revert();

        Assert.Equal(RegValue.DWord(1), env.Registry.GetValue(RegHive.CurrentUser, @"Software\Test", "A"));
        Assert.Null(env.Registry.GetValue(RegHive.CurrentUser, @"Software\Test", "B"));
    }

    [Fact]
    public void Apply_WhenAWriteIsDenied_RollsBack_AndThrowsFriendlyError()
    {
        var env = new TestEnv();
        env.Registry.SetValue(RegHive.CurrentUser, @"Software\Test", "A", RegValue.DWord(9));
        env.Registry.DenyWrites.Add("B"); // e.g. locked by Group Policy
        var before = env.Registry.Dump();
        var tweak = new SampleTweak(env.Context);

        var ex = Assert.Throws<TweakException>(tweak.Apply);

        Assert.Contains("Access denied", ex.Message);
        Assert.Equal(before, env.Registry.Dump()); // "A" was put back
        Assert.Null(env.Backups.Get("test.sample")); // no misleading backup left behind
        Assert.Contains(env.Log.GetAll(), e => e.Action == ChangeAction.Failed);
    }

    [Fact]
    public void Transparency_ApplyAndRevert_BroadcastsChange()
    {
        var env = new TestEnv();
        env.Registry.SetValue(RegHive.CurrentUser, Personalize, "EnableTransparency", RegValue.DWord(1));
        var tweak = new DisableTransparencyTweak(env.Context);

        Assert.False(tweak.IsApplied());
        tweak.Apply();
        Assert.True(tweak.IsApplied());
        Assert.Equal(RegValue.DWord(0), env.Registry.GetValue(RegHive.CurrentUser, Personalize, "EnableTransparency"));
        tweak.Revert();
        Assert.Equal(RegValue.DWord(1), env.Registry.GetValue(RegHive.CurrentUser, Personalize, "EnableTransparency"));
        Assert.Equal(2, env.Notifier.Broadcasts.Count);
    }

    [Fact]
    public void MouseAcceleration_AppliesLive_WithCurrentRegistryValues()
    {
        var env = new TestEnv();
        const string mouse = @"Control Panel\Mouse";
        env.Registry.SetValue(RegHive.CurrentUser, mouse, "MouseSpeed", RegValue.Str("1"));
        env.Registry.SetValue(RegHive.CurrentUser, mouse, "MouseThreshold1", RegValue.Str("6"));
        env.Registry.SetValue(RegHive.CurrentUser, mouse, "MouseThreshold2", RegValue.Str("10"));
        var tweak = new DisableMouseAccelerationTweak(env.Context);

        tweak.Apply();
        tweak.Revert();

        Assert.Equal((0, 0, 0), env.Notifier.MouseCalls[0]);
        Assert.Equal((6, 10, 1), env.Notifier.MouseCalls[1]);
    }

    [Fact]
    public void HardwareGpuScheduling_RevertWithoutBackup_TurnsItOff()
    {
        var env = new TestEnv();
        const string path = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
        env.Registry.SetValue(RegHive.LocalMachine, path, "HwSchMode", RegValue.DWord(2)); // on by default (e.g. new PC)
        var tweak = new HardwareGpuSchedulingTweak(env.Context);

        Assert.True(tweak.IsApplied());
        tweak.Revert();

        Assert.Equal(RegValue.DWord(1), env.Registry.GetValue(RegHive.LocalMachine, path, "HwSchMode"));
        Assert.False(tweak.IsApplied());
    }
}
