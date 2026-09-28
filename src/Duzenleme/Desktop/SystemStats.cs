using System.IO;
using System.Runtime.InteropServices;
using Duzenleme.Core;

namespace Duzenleme.Desktop;

/// <summary>Bellek: kullanılan ve toplam (bayt).</summary>
public readonly record struct MemoryInfo(ulong Used, ulong Total)
{
    public double Fraction => Total == 0 ? 0 : (double)Used / Total;
}

/// <summary>Pil: yüzde (bilinmiyorsa null), şarjda mı. Pil yoksa hiç döndürülmez.</summary>
public readonly record struct BatteryInfo(int? Percent, bool Charging, bool PluggedIn);

/// <summary>Sürücü: boş ve toplam alan (bayt).</summary>
public readonly record struct DiskInfo(string Root, ulong Free, ulong Total)
{
    public double UsedFraction => Total == 0 ? 0 : 1 - (double)Free / Total;
}

/// <summary>
/// Sistem durumu widget'ının ölçümleri: doğrudan Win32 (GetSystemTimes, GlobalMemoryStatusEx, GetSystemPowerStatus,
/// GetDiskFreeSpaceEx). Her biri mikrosaniyeler sürer; WMI ya da performans sayacı yok (ilk kullanımları yavaş, yükleri ağır).
/// Değerler yalnızca gösterilir: saklanmaz, gönderilmez. Disk sorgusu arka planda yapılmalı (uyuyan disk uyanabilir).
/// </summary>
public static class SystemStats
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME { public uint Low, High; public readonly ulong Value => ((ulong)High << 32) | Low; }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public int BatteryLifeTime, BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetDiskFreeSpaceEx(string root, out ulong freeForUser, out ulong total, out ulong totalFree);

    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();

    public static CpuTimes? Cpu() =>
        GetSystemTimes(out var idle, out var kernel, out var user) ? new CpuTimes(idle.Value, kernel.Value, user.Value) : null;

    public static MemoryInfo? Memory()
    {
        var status = new MEMORYSTATUSEX { Length = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        return GlobalMemoryStatusEx(ref status) ? new MemoryInfo(status.TotalPhys - status.AvailPhys, status.TotalPhys) : null;
    }

    /// <summary>Pil durumu; masaüstü bilgisayarda (pil yok) ya da okunamazsa null.</summary>
    public static BatteryInfo? Battery()
    {
        const byte NoBattery = 128, Unknown = 255, ChargingFlag = 8;
        if (!GetSystemPowerStatus(out var s) || s.BatteryFlag == Unknown || (s.BatteryFlag & NoBattery) != 0) return null;
        return new BatteryInfo(s.BatteryLifePercent <= 100 ? s.BatteryLifePercent : null, (s.BatteryFlag & ChargingFlag) != 0, s.ACLineStatus == 1);
    }

    /// <summary>Sürücünün boş alanı; okunamazsa null. Arka planda çağır.</summary>
    public static DiskInfo? Disk(string root) =>
        GetDiskFreeSpaceEx(root, out var free, out var total, out _) ? new DiskInfo(root, free, total) : null;

    public static TimeSpan Uptime => TimeSpan.FromMilliseconds(GetTickCount64());

    /// <summary>Windows'un kurulu olduğu sürücü ("C:\").</summary>
    public static string SystemDrive => Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) is { Length: > 0 } root ? root : @"C:\";

    /// <summary>Sabit sürücüler (çıkarılabilir, ağ ve CD değil). Arka planda çağır.</summary>
    public static List<string> FixedDrives()
    {
        try
        {
            return DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed).Select(d => d.RootDirectory.FullName).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return [SystemDrive]; }
    }
}
