using PCOpti.Models;

namespace PCOpti.Tweaks.Gaming;

public sealed class GameModeTweak : RegistryTweak
{
    private const string GameBar = @"Software\Microsoft\GameBar";

    public GameModeTweak(TweakContext context) : base(context) { }

    public override string Id => "gaming.gamemode";
    public override string Name => "Enable Game Mode";
    public override TweakCategory Category => TweakCategory.Gaming;
    public override RiskLevel RiskLevel => RiskLevel.Safe;

    public override string Description =>
        "Turns on Windows Game Mode, which gives the game priority for CPU and GPU time and pauses Windows Update " +
        "installs and driver installs while you play. It is already on by default in Windows 10 and 11, so if this " +
        "shows Applied you don't need to do anything. The effect is modest and helps most when other apps are busy.";

    public override IReadOnlyList<RegistrySetting> Settings { get; } = new[]
    {
        new RegistrySetting(RegHive.CurrentUser, GameBar, "AutoGameModeEnabled", RegValue.DWord(1), RevertTo: RegValue.DWord(0)),
    };
}
