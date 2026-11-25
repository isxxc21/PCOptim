using System.Runtime.InteropServices;

namespace PCOpti.Services;

/// <summary>
/// Tells Windows that settings changed so they take effect without a sign-out where possible.
/// Abstracted so unit tests don't change the test machine's settings.
/// </summary>
public interface ISystemNotifier
{
    /// <summary>Applies mouse acceleration values live (same values as HKCU\Control Panel\Mouse).</summary>
    void SetMouseAcceleration(int threshold1, int threshold2, int speed);
    /// <summary>Broadcasts WM_SETTINGCHANGE so Explorer and apps re-read settings.</summary>
    void BroadcastSettingChange(string area);
}

public sealed class Win32SystemNotifier : ISystemNotifier
{
    private const uint SPI_SETMOUSE = 0x0004;
    private const uint SPIF_SENDCHANGE = 0x02;
    private const int WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;
    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, int[] vparam, uint winIni);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam, string lParam,
        uint flags, uint timeout, out IntPtr result);

    public void SetMouseAcceleration(int threshold1, int threshold2, int speed)
    {
        // The tweak already wrote the registry; this just applies the values to the live session.
        SystemParametersInfo(SPI_SETMOUSE, 0, new[] { threshold1, threshold2, speed }, SPIF_SENDCHANGE);
    }

    public void BroadcastSettingChange(string area)
    {
        SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, area, SMTO_ABORTIFHUNG, 2000, out _);
    }
}
