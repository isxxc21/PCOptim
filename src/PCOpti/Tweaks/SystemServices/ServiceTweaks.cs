using PCOpti.Models;

namespace PCOpti.Tweaks.SystemServices;

// Curated, deliberately short list. Every service here is safe to set to Manual (Windows can still start it
// when needed). Critical services (RPC, DCOM, Windows Update, Defender, audio, networking, etc.) are never offered.
// Honest note shown on the page: each of these saves only a few MB of RAM or a fraction of a second of boot time.

public sealed class PrintSpoolerServiceTweak : ServiceTweak
{
    public PrintSpoolerServiceTweak(TweakContext c) : base(c) { }
    public override string ServiceName => "Spooler";
    public override string Name => "Print Spooler: set to Manual";
    public override RiskLevel RiskLevel => RiskLevel.Moderate;
    public override string Description =>
        "Handles printing. If you never print or use \"Print to PDF\", it doesn't need to start with Windows. " +
        "Setting it to Manual also shrinks the attack surface (it has had serious security bugs, such as PrintNightmare).";
    public override string? Warning =>
        "Only do this if you don't use a printer. Printing and \"Microsoft Print to PDF\" may not work until you revert it.";
}

public sealed class FaxServiceTweak : ServiceTweak
{
    public FaxServiceTweak(TweakContext c) : base(c) { }
    public override string ServiceName => "Fax";
    protected override int DefaultStart => StartManual;
    public override string Name => "Fax: set to Manual";
    public override RiskLevel RiskLevel => RiskLevel.Safe;
    public override string Description =>
        "Sends and receives faxes through a fax modem. Almost no one needs this. It is usually already Manual, " +
        "in which case this shows as Applied and there is nothing to gain.";
}

public sealed class RemoteRegistryServiceTweak : ServiceTweak
{
    public RemoteRegistryServiceTweak(TweakContext c) : base(c) { }
    public override string ServiceName => "RemoteRegistry";
    protected override int DefaultStart => StartDisabled;
    public override string Name => "Remote Registry: keep off";
    public override RiskLevel RiskLevel => RiskLevel.Safe;
    public override string Description =>
        "Lets other computers on the network edit this PC's registry. Home PCs don't need it, and it's a security " +
        "risk if it's running. Windows disables it by default, so this normally already shows as Applied. " +
        "Apply only changes it if something set it to start automatically.";
}

public sealed class TelemetryServiceTweak : ServiceTweak
{
    public TelemetryServiceTweak(TweakContext c) : base(c) { }
    public override string ServiceName => "DiagTrack";
    public override string Name => "Connected User Experiences and Telemetry: set to Manual";
    public override RiskLevel RiskLevel => RiskLevel.Safe;
    public override string Description =>
        "Collects and uploads diagnostic data to Microsoft. Setting it to Manual stops it running all the time. " +
        "The performance gain is small (occasional background CPU and disk use). Windows feature updates sometimes " +
        "reset it to Automatic, so check this page after big updates.";
}

public sealed class MapsBrokerServiceTweak : ServiceTweak
{
    public MapsBrokerServiceTweak(TweakContext c) : base(c) { }
    public override string ServiceName => "MapsBroker";
    protected override bool DefaultDelayed => true;
    public override string Name => "Downloaded Maps Manager: set to Manual";
    public override RiskLevel RiskLevel => RiskLevel.Safe;
    public override string Description =>
        "Keeps offline maps for the Windows Maps app up to date. If you don't use offline maps, it doesn't need " +
        "to start with Windows. The Maps app still starts it when needed.";
}

public sealed class LinkTrackingServiceTweak : ServiceTweak
{
    public LinkTrackingServiceTweak(TweakContext c) : base(c) { }
    public override string ServiceName => "TrkWks";
    public override string Name => "Distributed Link Tracking Client: set to Manual";
    public override RiskLevel RiskLevel => RiskLevel.Safe;
    public override string Description =>
        "Keeps shortcuts working when files move between NTFS drives on a corporate network. On a home PC it " +
        "does almost nothing. Shortcuts on your own PC still work. This saves a tiny amount of memory.";
}
