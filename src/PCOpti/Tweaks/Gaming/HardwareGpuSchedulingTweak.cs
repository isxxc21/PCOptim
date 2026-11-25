using PCOpti.Models;

namespace PCOpti.Tweaks.Gaming;

/// <summary>Use Apply to turn HAGS on and Revert to turn it off (or back to what it was).</summary>
public sealed class HardwareGpuSchedulingTweak : RegistryTweak
{
    private const string GraphicsDrivers = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";

    public HardwareGpuSchedulingTweak(TweakContext context) : base(context) { }

    public override string Id => "gaming.hags";
    public override string Name => "Hardware-Accelerated GPU Scheduling";
    public override TweakCategory Category => TweakCategory.Gaming;
    public override RiskLevel RiskLevel => RiskLevel.Moderate;

    public override string Description =>
        "Lets the graphics card manage its own memory scheduling instead of Windows doing it. Results differ by " +
        "GPU, driver and game: some see slightly lower latency, some see no change, and a few get stutter. It is " +
        "needed for NVIDIA DLSS Frame Generation. It needs a supported GPU (NVIDIA GTX 10-series / AMD RX 5000 or " +
        "newer) and a recent driver. Revert turns it off (or back to how it was).";

    public override string? Warning =>
        "Changes a graphics driver setting and needs a restart. If games stutter or crash afterwards, revert it.";

    public override string? Note => "Restart required.";

    public override string? GetUnavailableReason() =>
        Environment.OSVersion.Version.Build < 19041 ? "Needs Windows 10 version 2004 or newer." : null;

    public override IReadOnlyList<RegistrySetting> Settings { get; } = new[]
    {
        // HwSchMode: 2 = on, 1 = off.
        new RegistrySetting(RegHive.LocalMachine, GraphicsDrivers, "HwSchMode", RegValue.DWord(2), RevertTo: RegValue.DWord(1)),
    };
}
