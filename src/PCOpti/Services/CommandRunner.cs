using System.Diagnostics;
using System.Text;

namespace PCOpti.Services;

public sealed record CommandResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Success => ExitCode == 0;
    /// <summary>Best text to show the user when the command failed.</summary>
    public string ErrorText => string.IsNullOrWhiteSpace(StdErr) ? StdOut.Trim() : StdErr.Trim();
}

/// <summary>Runs external tools (powercfg, sc, netsh, PowerShell). Abstracted for unit tests.</summary>
public interface ICommandRunner
{
    CommandResult Run(string fileName, string arguments, int timeoutMs = 60_000);
    CommandResult RunPowerShell(string script, int timeoutMs = 120_000);
}

/// <summary>Runs commands hidden, captures output, and kills them if they hang.</summary>
public sealed class CommandRunner : ICommandRunner
{
    public CommandResult Run(string fileName, string arguments, int timeoutMs = 60_000)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var p = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (stdout) stdout.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (stderr) stderr.AppendLine(e.Data); };

        try
        {
            p.Start();
        }
        catch (Exception ex)
        {
            return new CommandResult(-1, "", $"Could not start {fileName}: {ex.Message}");
        }

        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        if (!p.WaitForExit(timeoutMs))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* already gone */ }
            return new CommandResult(-2, stdout.ToString(), $"{Path.GetFileName(fileName)} timed out after {timeoutMs / 1000}s.");
        }
        p.WaitForExit(); // flush async output handlers
        return new CommandResult(p.ExitCode, stdout.ToString(), stderr.ToString());
    }

    public CommandResult RunPowerShell(string script, int timeoutMs = 120_000)
    {
        // -EncodedCommand avoids every quoting problem. Output is forced to UTF-8 so non-English names survive.
        var full = "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $ProgressPreference = 'SilentlyContinue'; $ErrorActionPreference = 'Stop';\n" + script;
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(full));
        var exe = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        return RunUtf8(exe, $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}", timeoutMs);
    }

    private CommandResult RunUtf8(string exe, string args, int timeoutMs)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        using var p = new Process { StartInfo = psi };
        try { p.Start(); }
        catch (Exception ex) { return new CommandResult(-1, "", $"Could not start PowerShell: {ex.Message}"); }

        var outTask = p.StandardOutput.ReadToEndAsync();
        var errTask = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(timeoutMs))
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            return new CommandResult(-2, "", $"PowerShell timed out after {timeoutMs / 1000}s.");
        }
        return new CommandResult(p.ExitCode, outTask.Result, errTask.Result);
    }
}
