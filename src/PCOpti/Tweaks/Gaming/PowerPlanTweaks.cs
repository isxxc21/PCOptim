using PCOpti.Models;

namespace PCOpti.Tweaks.Gaming;

public sealed class HighPerformancePowerPlanTweak : PowerPlanTweak
{
    public HighPerformancePowerPlanTweak(TweakContext context) : base(context) { }

    protected override string SchemeGuid => "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    public override string Id => "gaming.powerplan.high";
    public override string Name => "High Performance power plan";
    public override RiskLevel RiskLevel => RiskLevel.Safe;

    public override string Description =>
        "Switches Windows to the built-in High Performance plan, which keeps the CPU at higher clock speeds " +
        "instead of saving power. On desktops this can reduce small stutters when a game suddenly needs more CPU. " +
        "The FPS gain is usually small (often 0-3%). It uses more power and on laptops it drains the battery faster.";

    public override string? Note => "Revert switches back to the plan you were using before.";
}

public sealed class UltimatePerformancePowerPlanTweak : PowerPlanTweak
{
    public UltimatePerformancePowerPlanTweak(TweakContext context) : base(context) { }

    protected override string SchemeGuid => "e9a42b02-d5df-448d-aa00-03f14749eb61";
    public override string Id => "gaming.powerplan.ultimate";
    public override string Name => "Ultimate Performance power plan";
    public override RiskLevel RiskLevel => RiskLevel.Moderate;

    public override string Description =>
        "Creates and enables Windows' hidden Ultimate Performance plan. It is like High Performance but also stops " +
        "devices idling to save power. Microsoft made it for workstations. For gaming the difference from High " +
        "Performance is very small or zero, and power use and heat are noticeably higher.";

    public override string? Warning =>
        "Not recommended for laptops: it increases heat, fan noise and battery drain. Desktop PCs only.";

    public override string? Note => "PCOpti deletes the plan it created when you revert.";
}
