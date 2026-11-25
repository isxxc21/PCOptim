using System.Security;

namespace PCOpti.Tweaks;

/// <summary>A tweak failure with a message that is safe and useful to show the user.</summary>
public sealed class TweakException : Exception
{
    public TweakException(string message, Exception? inner = null) : base(message, inner) { }

    /// <summary>Turns any exception into a clear, user-facing explanation.</summary>
    public static TweakException From(Exception ex) => ex switch
    {
        TweakException t => t,
        UnauthorizedAccessException or SecurityException => new TweakException(
            "Access denied. This setting may be locked by Group Policy, your organisation, or security software, " +
            "or PCOpti isn't running as administrator.", ex),
        IOException io => new TweakException($"A file or registry operation failed: {io.Message}", ex),
        _ => new TweakException($"Unexpected error: {ex.Message}", ex),
    };
}
