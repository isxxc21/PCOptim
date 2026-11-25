using PCOpti.Models;

namespace PCOpti.Tweaks.Gaming;

public sealed class DisableMouseAccelerationTweak : RegistryTweak
{
    private const string Mouse = @"Control Panel\Mouse";

    public DisableMouseAccelerationTweak(TweakContext context) : base(context) { }

    public override string Id => "gaming.mouse.noaccel";
    public override string Name => "Disable mouse acceleration";
    public override TweakCategory Category => TweakCategory.Gaming;
    public override RiskLevel RiskLevel => RiskLevel.Safe;

    public override string Description =>
        "Turns off \"Enhance pointer precision\", so the pointer moves the same distance for the same hand movement " +
        "however fast you move. Many players find aiming more consistent. It doesn't change FPS. Most games that " +
        "use raw input already ignore this setting, so it matters most on the desktop and in older games.";

    public override IReadOnlyList<RegistrySetting> Settings { get; } = new[]
    {
        // Windows defaults: MouseSpeed "1", MouseThreshold1 "6", MouseThreshold2 "10".
        new RegistrySetting(RegHive.CurrentUser, Mouse, "MouseSpeed", RegValue.Str("0"), RevertTo: RegValue.Str("1")),
        new RegistrySetting(RegHive.CurrentUser, Mouse, "MouseThreshold1", RegValue.Str("0"), RevertTo: RegValue.Str("6")),
        new RegistrySetting(RegHive.CurrentUser, Mouse, "MouseThreshold2", RegValue.Str("0"), RevertTo: RegValue.Str("10")),
    };

    /// <summary>Apply the values now so no sign-out is needed.</summary>
    protected override void AfterChange()
    {
        int Read(string name, int fallback) =>
            int.TryParse(Ctx.Registry.GetValue(RegHive.CurrentUser, Mouse, name)?.Data, out var v) ? v : fallback;

        Ctx.Notifier.SetMouseAcceleration(Read("MouseThreshold1", 6), Read("MouseThreshold2", 10), Read("MouseSpeed", 1));
    }
}
