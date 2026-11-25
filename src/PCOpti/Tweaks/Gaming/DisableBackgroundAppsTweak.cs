using PCOpti.Models;

namespace PCOpti.Tweaks.Gaming;

public sealed class DisableBackgroundAppsTweak : RegistryTweak
{
    private const string BackgroundAccess = @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications";
    private const string Search = @"Software\Microsoft\Windows\CurrentVersion\Search";

    public DisableBackgroundAppsTweak(TweakContext context) : base(context) { }

    public override string Id => "gaming.backgroundapps.off";
    public override string Name => "Disable background Store apps";
    public override TweakCategory Category => TweakCategory.Gaming;
    public override RiskLevel RiskLevel => RiskLevel.Moderate;

    public override string Description =>
        "Stops Microsoft Store (UWP) apps such as Mail, Weather and Xbox from running in the background. It does " +
        "NOT affect normal desktop programs like Discord, Steam or your browser. The gain is small: a little " +
        "less memory and background CPU use. On recent Windows 11 builds Windows may manage this per app instead.";

    public override string? Warning =>
        "Store apps may stop sending notifications or syncing until you open them (for example Mail, Calendar, Phone Link).";

    public override IReadOnlyList<RegistrySetting> Settings { get; } = new[]
    {
        new RegistrySetting(RegHive.CurrentUser, BackgroundAccess, "GlobalUserDisabled", RegValue.DWord(1), RevertTo: RegValue.DWord(0)),
        new RegistrySetting(RegHive.CurrentUser, Search, "BackgroundAppGlobalToggle", RegValue.DWord(0), RevertTo: RegValue.DWord(1)),
    };
}
