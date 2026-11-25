using PCOpti.Models;

namespace PCOpti.Tweaks.Gaming;

public sealed class DisableNotificationsTweak : RegistryTweak
{
    private const string Push = @"Software\Microsoft\Windows\CurrentVersion\PushNotifications";
    private const string NotifSettings = @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings";

    public DisableNotificationsTweak(TweakContext context) : base(context) { }

    public override string Id => "gaming.notifications.off";
    public override string Name => "Disable notification pop-ups";
    public override TweakCategory Category => TweakCategory.Gaming;
    public override RiskLevel RiskLevel => RiskLevel.Moderate;

    public override string Description =>
        "Turns off all Windows notification banners (toasts), so nothing pops up over your game. Windows Focus " +
        "Assist / Do Not Disturb already hides notifications during full-screen games by default; this also " +
        "covers borderless-window games. It isn't an FPS boost, it just removes interruptions.";

    public override string? Warning =>
        "You will see no notification pop-ups anywhere (chat messages, reminders, security alerts) until you revert this.";

    public override string? Note => "May need you to sign out, or restart Explorer, to take full effect.";

    public override IReadOnlyList<RegistrySetting> Settings { get; } = new[]
    {
        new RegistrySetting(RegHive.CurrentUser, Push, "ToastEnabled", RegValue.DWord(0), RevertTo: RegValue.DWord(1)),
        new RegistrySetting(RegHive.CurrentUser, NotifSettings, "NOC_GLOBAL_SETTING_TOASTS_ENABLED", RegValue.DWord(0), RevertTo: null),
    };

    protected override void AfterChange() => Ctx.Notifier.BroadcastSettingChange("Policy");
}
