using PCOpti.Models;

namespace PCOpti.Tweaks.Gaming;

public sealed class DisableGameBarTweak : RegistryTweak
{
    private const string GameBar = @"Software\Microsoft\GameBar";

    public DisableGameBarTweak(TweakContext context) : base(context) { }

    public override string Id => "gaming.gamebar.off";
    public override string Name => "Disable Xbox Game Bar shortcuts";
    public override TweakCategory Category => TweakCategory.Gaming;
    public override RiskLevel RiskLevel => RiskLevel.Safe;

    public override string Description =>
        "Stops the Xbox Game Bar overlay opening when you press the Xbox button on a controller, and hides its " +
        "startup tips. Game Bar uses almost no resources while it's closed, so this is mostly about avoiding " +
        "accidental pop-ups, not FPS. To remove Game Bar completely, uninstall it in Settings > Apps.";

    public override string? Note => "Background recording is a separate tweak (Game DVR).";

    public override IReadOnlyList<RegistrySetting> Settings { get; } = new[]
    {
        new RegistrySetting(RegHive.CurrentUser, GameBar, "UseNexusForGameBarEnabled", RegValue.DWord(0), RevertTo: RegValue.DWord(1)),
        new RegistrySetting(RegHive.CurrentUser, GameBar, "ShowStartupPanel", RegValue.DWord(0), RevertTo: null),
    };
}
