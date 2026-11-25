using System.Runtime.InteropServices;
using System.ServiceProcess;
using PCOpti.Models;

namespace PCOpti.Services;

public sealed record ScanResult(long Bytes, int Files);
public sealed record CleanResult(long BytesFreed, int FilesDeleted, int FilesSkipped, string? Message = null);

/// <summary>One category on the Cleaner page. Scan never deletes; Clean only runs after the user confirms.</summary>
public abstract class CleanTarget
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract ScanResult Scan();
    public abstract CleanResult Clean();
}

/// <summary>Deletes files inside fixed folders. Never follows junctions/symlinks and never deletes the folders themselves.</summary>
public class FolderCleanTarget : CleanTarget
{
    private readonly Func<IEnumerable<string>> _folders;
    private readonly string _pattern;
    private readonly TimeSpan _minAge;

    public FolderCleanTarget(string id, string name, string description, Func<IEnumerable<string>> folders,
        string pattern = "*", TimeSpan? minAge = null)
    {
        Id = id; Name = name; Description = description;
        _folders = folders; _pattern = pattern; _minAge = minAge ?? TimeSpan.Zero;
    }

    public override string Id { get; }
    public override string Name { get; }
    public override string Description { get; }

    private static readonly EnumerationOptions Options = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint, // don't follow junctions out of the folder
        ReturnSpecialDirectories = false,
    };

    private IEnumerable<string> SafeFolders() => _folders()
        .Where(f => !string.IsNullOrWhiteSpace(f))
        .Select(Path.GetFullPath)
        // Paranoia: refuse drive roots and very short paths whatever happens upstream.
        .Where(f => f.Length > 10 && Path.GetPathRoot(f) != f && Directory.Exists(f))
        .Distinct(StringComparer.OrdinalIgnoreCase);

    private IEnumerable<FileInfo> Candidates()
    {
        var cutoff = DateTime.UtcNow - _minAge;
        foreach (var folder in SafeFolders())
        {
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(folder, _pattern, Options).ToList(); }
            catch { continue; }
            foreach (var f in files)
            {
                FileInfo fi;
                try { fi = new FileInfo(f); if (!fi.Exists) continue; } catch { continue; }
                if (_minAge > TimeSpan.Zero && fi.LastWriteTimeUtc > cutoff) continue;
                yield return fi;
            }
        }
    }

    public override ScanResult Scan()
    {
        long bytes = 0; int count = 0;
        foreach (var f in Candidates()) { try { bytes += f.Length; count++; } catch { } }
        return new ScanResult(bytes, count);
    }

    public override CleanResult Clean()
    {
        Before();
        try
        {
            long freed = 0; int deleted = 0, skipped = 0;
            foreach (var f in Candidates())
            {
                try
                {
                    var len = f.Length;
                    if (f.IsReadOnly) f.IsReadOnly = false;
                    f.Delete();
                    freed += len; deleted++;
                }
                catch { skipped++; } // in use or protected: leave it
            }
            RemoveEmptySubfolders();
            return new CleanResult(freed, deleted, skipped);
        }
        finally
        {
            After();
        }
    }

    /// <summary>Hooks for targets that must stop a service while cleaning.</summary>
    protected virtual void Before() { }
    protected virtual void After() { }

    private void RemoveEmptySubfolders()
    {
        if (_pattern != "*") return;
        foreach (var folder in SafeFolders())
        {
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(folder, "*", Options).OrderByDescending(d => d.Length))
                {
                    try { if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir); } catch { }
                }
            }
            catch { }
        }
    }
}

/// <summary>Windows Update download cache. The update services are stopped while cleaning and restarted afterwards.</summary>
public sealed class WindowsUpdateCacheTarget : FolderCleanTarget
{
    private readonly List<string> _stopped = new();

    public WindowsUpdateCacheTarget() : base(
        "windowsupdate", "Windows Update cache",
        "Update files Windows has already downloaded (SoftwareDistribution\\Download). Safe to delete: Windows downloads " +
        "again anything it still needs. Useful if updates are stuck or you need disk space. It doesn't speed up games.",
        () => new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download") })
    { }

    protected override void Before()
    {
        foreach (var name in new[] { "wuauserv", "bits" })
        {
            try
            {
                using var sc = new ServiceController(name);
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    _stopped.Add(name);
                }
            }
            catch { /* if it can't be stopped, locked files are simply skipped */ }
        }
    }

    protected override void After()
    {
        foreach (var name in _stopped)
        {
            try { using var sc = new ServiceController(name); sc.Start(); } catch { }
        }
        _stopped.Clear();
    }
}

/// <summary>Recycle Bin, via the shell API (sizes for all drives).</summary>
public sealed class RecycleBinTarget : CleanTarget
{
    public override string Id => "recyclebin";
    public override string Name => "Recycle Bin";
    public override string Description =>
        "Permanently deletes everything in the Recycle Bin on all drives. You can't get these files back afterwards.";

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO info);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint flags);

    private const uint SHERB_NOCONFIRMATION = 0x1, SHERB_NOPROGRESSUI = 0x2, SHERB_NOSOUND = 0x4;

    public override ScanResult Scan()
    {
        var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
        return SHQueryRecycleBin(null, ref info) == 0 ? new ScanResult(info.i64Size, (int)info.i64NumItems) : new ScanResult(0, 0);
    }

    public override CleanResult Clean()
    {
        var before = Scan();
        if (before.Files == 0) return new CleanResult(0, 0, 0, "The Recycle Bin is already empty.");
        // PCOpti already asked the user, so the shell's own confirmation is suppressed.
        var hr = SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
        return hr == 0
            ? new CleanResult(before.Bytes, before.Files, 0)
            : new CleanResult(0, 0, before.Files, $"Windows couldn't empty the Recycle Bin (error 0x{hr:X8}).");
    }
}

public static class CleanTargets
{
    private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static string ProgramData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    private static string WinDir => Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    public static IReadOnlyList<CleanTarget> Create() => new CleanTarget[]
    {
        new FolderCleanTarget("temp", "Temporary files",
            "Leftover files in your Temp folder and the Windows Temp folder. Files changed in the last 24 hours and " +
            "files still in use are skipped, so running apps aren't affected. This frees disk space; it doesn't make games faster.",
            () => new[] { Path.GetTempPath(), Path.Combine(WinDir, "Temp") },
            minAge: TimeSpan.FromHours(24)),

        new WindowsUpdateCacheTarget(),

        new FolderCleanTarget("shadercache", "Shader caches (NVIDIA, AMD, DirectX)",
            "Compiled shaders saved by your graphics driver and DirectX. Deleting them can fix glitches caused by a " +
            "corrupt cache, and frees space. Be aware: games rebuild these, so the first launch of each game " +
            "afterwards may load slower or stutter briefly. This does NOT improve performance.",
            () => new[]
            {
                Path.Combine(Local, "D3DSCache"),
                Path.Combine(Local, "NVIDIA", "DXCache"),
                Path.Combine(Local, "NVIDIA", "GLCache"),
                Path.Combine(ProgramData, "NVIDIA Corporation", "NV_Cache"),
                Path.Combine(Local, "AMD", "DxCache"),
                Path.Combine(Local, "AMD", "DxcCache"),
                Path.Combine(Local, "AMD", "GLCache"),
                Path.Combine(Local, "AMD", "VkCache"),
            }),

        new FolderCleanTarget("thumbnails", "Thumbnail cache",
            "The thumbnail previews Explorer keeps for pictures and videos. Windows rebuilds them when you browse " +
            "folders. Files Explorer is using are skipped, so some may stay until you restart.",
            () => new[] { Path.Combine(Local, "Microsoft", "Windows", "Explorer") },
            pattern: "thumbcache_*.db"),

        new RecycleBinTarget(),
    };
}
