using PCOpti.Models;

namespace PCOpti.Tweaks.Gaming;

public sealed class DisableTransparencyTweak : RegistryTweak
{
    private const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public DisableTransparencyTweak(TweakContext context) : base(context) { }

    public override string Id => "gaming.transparency.off";
    public override string Name => "Disable transparency effects";
    public override TweakCategory Category => TweakCategory.Gaming;
    public override RiskLevel RiskLevel => RiskLevel.Safe;

    public override string Description =>
        "Turns off the see-through blur on the taskbar, Start menu and Windows panels (Settings > Personalisation > " +
        "Colours > Transparency effects). This saves a little GPU work on the desktop and can help integrated " +
        "graphics and weak laptops. On a gaming GPU you won't notice an FPS difference.";

    public override IReadOnlyList<RegistrySetting> Settings { get; } = new[]
    {
        new RegistrySetting(RegHive.CurrentUser, Personalize, "EnableTransparency", RegValue.DWord(0), RevertTo: RegValue.DWord(1)),
    };

    protected override void AfterChange() => Ctx.Notifier.BroadcastSettingChange("ImmersiveColorSet");
}
