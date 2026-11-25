using PCOpti.Tweaks.Gaming;
using PCOpti.Tweaks.SystemServices;

namespace PCOpti.Tweaks;

/// <summary>
/// The single place every tweak is registered. The Gaming and Services pages are generated from this list,
/// so adding a tweak means writing one class and adding one line here.
/// </summary>
public static class TweakCatalog
{
    public static IReadOnlyList<ITweak> Create(TweakContext ctx) => new ITweak[]
    {
        // ---- Gaming ----
        new HighPerformancePowerPlanTweak(ctx),
        new UltimatePerformancePowerPlanTweak(ctx),
        new GameModeTweak(ctx),
        new DisableGameBarTweak(ctx),
        new DisableGameDvrTweak(ctx),
        new HardwareGpuSchedulingTweak(ctx),
        new DisableMouseAccelerationTweak(ctx),
        new ReduceVisualEffectsTweak(ctx),
        new DisableTransparencyTweak(ctx),
        new DisableBackgroundAppsTweak(ctx),
        new DisableNotificationsTweak(ctx),

        // ---- Services (curated, safe to set to Manual) ----
        new PrintSpoolerServiceTweak(ctx),
        new FaxServiceTweak(ctx),
        new RemoteRegistryServiceTweak(ctx),
        new TelemetryServiceTweak(ctx),
        new MapsBrokerServiceTweak(ctx),
        new LinkTrackingServiceTweak(ctx),
    };
}
