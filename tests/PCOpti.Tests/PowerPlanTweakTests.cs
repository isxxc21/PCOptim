using PCOpti.Services;
using PCOpti.Tweaks;
using PCOpti.Tweaks.Gaming;

namespace PCOpti.Tests;

public class PowerPlanTweakTests
{
    private const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private const string High = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string UltimateTemplate = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    private const string UltimateCopy = "11111111-2222-3333-4444-555555555555";

    /// <summary>Simulates powercfg with a list of installed schemes and an active one.</summary>
    private sealed class FakePowerCfg
    {
        public string Active = Balanced;
        public List<string> Schemes = new() { Balanced, High };

        public CommandResult Handle(string exe, string args)
        {
            if (exe != "powercfg.exe") return new CommandResult(0, "", "");
            if (args == "/getactivescheme") return new CommandResult(0, $"Power Scheme GUID: {Active}  (Plan)", "");
            if (args == "/list") return new CommandResult(0, string.Join("\n", Schemes.Select(s => $"Power Scheme GUID: {s}  (x)")), "");
            if (args.StartsWith("/setactive "))
            {
                var g = args[11..];
                if (!Schemes.Contains(g)) return new CommandResult(1, "", "Invalid Parameters");
                Active = g;
                return new CommandResult(0, "", "");
            }
            if (args.StartsWith("-duplicatescheme ")) { Schemes.Add(UltimateCopy); return new CommandResult(0, $"Power Scheme GUID: {UltimateCopy}  (Ultimate Performance)", ""); }
            if (args.StartsWith("-delete ")) { Schemes.Remove(args[8..]); return new CommandResult(0, "", ""); }
            return new CommandResult(1, "", "unknown");
        }
    }

    private static (TestEnv Env, FakePowerCfg Pc) Setup()
    {
        var env = new TestEnv();
        var pc = new FakePowerCfg();
        env.Commands.Handler = pc.Handle;
        return (env, pc);
    }

    [Fact]
    public void ParseGuid_ReadsPowercfgOutput()
    {
        Assert.Equal(Balanced, PowerPlanTweak.ParseGuid($"Power Scheme GUID: {Balanced.ToUpperInvariant()}  (Balanced)"));
        Assert.Null(PowerPlanTweak.ParseGuid("nothing here"));
    }

    [Fact]
    public void HighPerformance_ApplyAndRevert_ReturnsToOriginalPlan()
    {
        var (env, pc) = Setup();
        var tweak = new HighPerformancePowerPlanTweak(env.Context);

        Assert.False(tweak.IsApplied());
        tweak.Apply();
        Assert.Equal(High, pc.Active);
        Assert.True(tweak.IsApplied());

        tweak.Revert();
        Assert.Equal(Balanced, pc.Active);
        Assert.False(tweak.HasBackup);
    }

    [Fact]
    public void Revert_LeavesPlanAlone_IfUserSwitchedSince()
    {
        var (env, pc) = Setup();
        var tweak = new HighPerformancePowerPlanTweak(env.Context);
        tweak.Apply();
        pc.Schemes.Add("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        pc.Active = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"; // user picked another plan in Control Panel

        tweak.Revert();

        Assert.Equal("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", pc.Active);
    }

    [Fact]
    public void Ultimate_CreatesHiddenPlan_AndDeletesItOnRevert()
    {
        var (env, pc) = Setup();
        var tweak = new UltimatePerformancePowerPlanTweak(env.Context);

        tweak.Apply();
        Assert.Contains($"powercfg.exe -duplicatescheme {UltimateTemplate}", env.Commands.Calls);
        Assert.Equal(UltimateCopy, pc.Active);
        Assert.True(tweak.IsApplied()); // detection follows the created copy, not the template GUID

        tweak.Revert();
        Assert.Equal(Balanced, pc.Active);
        Assert.DoesNotContain(UltimateCopy, pc.Schemes);
    }

    [Fact]
    public void Apply_WhenPlanCantBeCreated_ExplainsModernStandby()
    {
        var (env, pc) = Setup();
        pc.Schemes.Remove(High);
        env.Commands.Handler = (exe, args) => args.StartsWith("-duplicatescheme") ? new CommandResult(1, "", "Unable to create") : pc.Handle(exe, args);

        var ex = Assert.Throws<TweakException>(new HighPerformancePowerPlanTweak(env.Context).Apply);

        Assert.Contains("Modern Standby", ex.Message);
        Assert.Equal(Balanced, pc.Active);
    }
}
