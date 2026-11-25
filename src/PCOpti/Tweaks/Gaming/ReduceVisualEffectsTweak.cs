using PCOpti.Models;

namespace PCOpti.Tweaks.Gaming;

public sealed class ReduceVisualEffectsTweak : RegistryTweak
{
    private const string Desktop = @"Control Panel\Desktop";
    private const string WindowMetrics = @"Control Panel\Desktop\WindowMetrics";
    private const string Advanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string VisualEffects = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects";
    private const string Dwm = @"Software\Microsoft\Windows\DWM";

    public ReduceVisualEffectsTweak(TweakContext context) : base(context) { }

    public override string Id => "gaming.visualeffects.performance";
    public override string Name => "Reduce visual effects and animations";
    public override TweakCategory Category => TweakCategory.Gaming;
    public override RiskLevel RiskLevel => RiskLevel.Safe;

    public override string Description =>
        "Uses a \"best performance\" visual effects profile: turns off window, menu and taskbar animations, fading, " +
        "Aero Peek and list shadows, but keeps smooth fonts and thumbnails. Windows feels snappier, especially on " +
        "older or low-end PCs. It does not raise FPS in full-screen games on a modern GPU.";

    public override string? Note => "Some changes fully apply after you sign out and back in.";

    public override IReadOnlyList<RegistrySetting> Settings { get; } = new[]
    {
        // 3 = "Custom" in the Performance Options dialog (we pick the individual options below).
        new RegistrySetting(RegHive.CurrentUser, VisualEffects, "VisualFXSetting", RegValue.DWord(3), RevertTo: null),
        // Performance-oriented UserPreferencesMask (menu/combo/tooltip animations and fades off).
        new RegistrySetting(RegHive.CurrentUser, Desktop, "UserPreferencesMask",
            RegValue.Binary(0x90, 0x12, 0x03, 0x80, 0x10, 0x00, 0x00, 0x00),
            RevertTo: RegValue.Binary(0x9E, 0x1E, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00)),
        new RegistrySetting(RegHive.CurrentUser, WindowMetrics, "MinAnimate", RegValue.Str("0"), RevertTo: RegValue.Str("1")),
        new RegistrySetting(RegHive.CurrentUser, Advanced, "TaskbarAnimations", RegValue.DWord(0), RevertTo: RegValue.DWord(1)),
        new RegistrySetting(RegHive.CurrentUser, Advanced, "ListviewAlphaSelect", RegValue.DWord(0), RevertTo: RegValue.DWord(1)),
        new RegistrySetting(RegHive.CurrentUser, Advanced, "ListviewShadow", RegValue.DWord(0), RevertTo: RegValue.DWord(1)),
        new RegistrySetting(RegHive.CurrentUser, Dwm, "EnableAeroPeek", RegValue.DWord(0), RevertTo: RegValue.DWord(1)),
    };

    protected override void AfterChange() => Ctx.Notifier.BroadcastSettingChange("WindowMetrics");
}
