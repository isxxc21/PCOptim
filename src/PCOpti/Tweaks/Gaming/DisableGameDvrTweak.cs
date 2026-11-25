using PCOpti.Models;

namespace PCOpti.Tweaks.Gaming;

public sealed class DisableGameDvrTweak : RegistryTweak
{
    private const string GameDvr = @"Software\Microsoft\Windows\CurrentVersion\GameDVR";
    private const string GameConfigStore = @"System\GameConfigStore";

    public DisableGameDvrTweak(TweakContext context) : base(context) { }

    public override string Id => "gaming.gamedvr.off";
    public override string Name => "Disable Game DVR background recording";
    public override TweakCategory Category => TweakCategory.Gaming;
    public override RiskLevel RiskLevel => RiskLevel.Safe;

    public override string Description =>
        "Turns off Windows' game capture, including \"Record what happened\", which constantly records the last few " +
        "minutes of gameplay in the background. If that was on, turning it off can give a real FPS gain on weaker " +
        "GPUs and laptops. If it was already off (the default on many PCs) nothing changes. You can no longer use " +
        "Win+Alt+G or Win+Alt+R to record clips.";

    public override IReadOnlyList<RegistrySetting> Settings { get; } = new[]
    {
        new RegistrySetting(RegHive.CurrentUser, GameConfigStore, "GameDVR_Enabled", RegValue.DWord(0), RevertTo: RegValue.DWord(1)),
        new RegistrySetting(RegHive.CurrentUser, GameDvr, "AppCaptureEnabled", RegValue.DWord(0), RevertTo: null),
        new RegistrySetting(RegHive.CurrentUser, GameDvr, "HistoricalCaptureEnabled", RegValue.DWord(0), RevertTo: null),
    };
}
