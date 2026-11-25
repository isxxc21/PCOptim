using PCOpti.Models;
using PCOpti.Services;

namespace PCOpti.Tweaks;

/// <summary>
/// Shared plumbing: wraps Apply/Revert with logging and turns errors into <see cref="TweakException"/>.
/// Subclasses implement <see cref="ApplyCore"/> and <see cref="RevertCore"/>.
/// </summary>
public abstract class TweakBase : ITweak
{
    protected TweakBase(TweakContext context) => Ctx = context;

    protected TweakContext Ctx { get; }

    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract TweakCategory Category { get; }
    public abstract RiskLevel RiskLevel { get; }
    public virtual string? Warning => null;
    public virtual string? Note => null;

    public virtual string? GetUnavailableReason() => null;
    public abstract bool IsApplied();

    public bool HasBackup => Ctx.Backups.Get(Id) is not null;

    public void Apply()
    {
        try
        {
            var details = ApplyCore();
            Ctx.Log.Add(Id, Name, ChangeAction.Applied, details);
        }
        catch (Exception ex)
        {
            var friendly = TweakException.From(ex);
            Ctx.Log.Add(Id, $"{Name} (apply failed)", ChangeAction.Failed, new[] { friendly.Message });
            throw friendly;
        }
    }

    public void Revert()
    {
        try
        {
            var details = RevertCore();
            Ctx.Log.Add(Id, Name, ChangeAction.Reverted, details);
        }
        catch (Exception ex)
        {
            var friendly = TweakException.From(ex);
            Ctx.Log.Add(Id, $"{Name} (revert failed)", ChangeAction.Failed, new[] { friendly.Message });
            throw friendly;
        }
    }

    /// <summary>Applies the change and returns human-readable details for the change log.</summary>
    protected abstract IEnumerable<string> ApplyCore();

    /// <summary>Restores the original state (from backup where one exists) and returns log details.</summary>
    protected abstract IEnumerable<string> RevertCore();
}
