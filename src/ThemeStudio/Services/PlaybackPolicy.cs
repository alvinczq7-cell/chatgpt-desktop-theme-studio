using System.Runtime.InteropServices;

namespace ThemeStudio.Services;

public static class PlaybackPolicy
{
    private const uint MonitorDefaultToNearest = 2;
    private const uint SpiGetClientAreaAnimation = 0x1042;

    public static bool ShouldPause(nint studioWindowHandle) =>
        IsOnBattery() || IsReducedMotionEnabled() || HasFullscreenForegroundWindow(studioWindowHandle);

    private static bool IsReducedMotionEnabled()
    {
        // Windows exposes the accessibility preference as whether client-area animations are enabled.
        // If the query fails, do not force a pause based on an unknown preference.
        return SystemParametersInfo(SpiGetClientAreaAnimation, 0, out var animationsEnabled, 0) && !animationsEnabled;
    }

    private static bool IsOnBattery()
    {
        if (!GetSystemPowerStatus(out var status)) return false;
        return status.AcLineStatus == 0 && status.BatteryFlag is not (128 or 255);
    }

    private static bool HasFullscreenForegroundWindow(nint studioWindowHandle)
    {
        var foreground = GetForegroundWindow();
        if (foreground == 0 || foreground == studioWindowHandle) return false;
        var monitor = MonitorFromWindow(foreground, MonitorDefaultToNearest);
        if (monitor == 0) return false;
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info) || !GetWindowRect(foreground, out var rect)) return false;
        return Math.Abs(rect.Left - info.Monitor.Left) <= 2 &&
               Math.Abs(rect.Top - info.Monitor.Top) <= 2 &&
               Math.Abs(rect.Right - info.Monitor.Right) <= 2 &&
               Math.Abs(rect.Bottom - info.Monitor.Bottom) <= 2;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint uiAction,
        uint uiParam,
        [MarshalAs(UnmanagedType.Bool)] out bool pvParam,
        uint fWinIni);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint window, out Rect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }
}
