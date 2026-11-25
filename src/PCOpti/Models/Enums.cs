namespace PCOpti.Models;

/// <summary>How careful the user should be before applying a tweak.</summary>
public enum RiskLevel
{
    /// <summary>Harmless and easy to undo. Applied without an extra warning.</summary>
    Safe,
    /// <summary>Can change behaviour the user may rely on. Requires confirmation.</summary>
    Moderate,
    /// <summary>System-level change; only for users who know what it does. Requires confirmation.</summary>
    Advanced,
}

/// <summary>The sidebar page a tweak appears on.</summary>
public enum TweakCategory
{
    Gaming,
    Services,
}
