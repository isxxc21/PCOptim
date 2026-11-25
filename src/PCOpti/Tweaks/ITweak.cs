using PCOpti.Models;

namespace PCOpti.Tweaks;

/// <summary>
/// One independent, reversible optimisation. Every tweak:
///  - detects its real state from the system (never from a cached flag),
///  - backs up everything it changes before changing it,
///  - can be reverted on its own, without touching any other tweak.
/// To add a tweak: implement this (usually via <see cref="RegistryTweak"/>) and add it to <see cref="TweakCatalog"/>.
/// </summary>
public interface ITweak
{
    /// <summary>Stable id used for backups and the change log. Never rename once released.</summary>
    string Id { get; }
    string Name { get; }
    /// <summary>Plain-English description, including how big the real-world gain is.</summary>
    string Description { get; }
    TweakCategory Category { get; }
    RiskLevel RiskLevel { get; }

    /// <summary>Extra warning shown in the confirmation dialog (Moderate/Advanced tweaks).</summary>
    string? Warning { get; }
    /// <summary>Short note such as "Restart required" shown on the card.</summary>
    string? Note { get; }

    /// <summary>Null if the tweak can run on this PC, otherwise the reason it can't.</summary>
    string? GetUnavailableReason();

    bool IsApplied();
    void Apply();
    void Revert();

    /// <summary>True if PCOpti holds a backup of the original state for this tweak.</summary>
    bool HasBackup { get; }
}
