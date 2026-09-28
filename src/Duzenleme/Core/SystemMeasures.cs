namespace Duzenleme.Core;

/// <summary>GetSystemTimes'ın üç sayacı (100 ns). Kernel, boşta geçen süreyi de içerir.</summary>
public readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User);

/// <summary>İşlemci kullanımı iki ölçüm arasındaki farktan (saf; testlenir). WMI ya da performans sayacı kullanılmaz.</summary>
public static class CpuUsage
{
    // Bir ölçüm aralığında bundan büyük fark sayaçların sıfırlandığını gösterir (ör. uyku sonrası sürücü); o ölçüm atlanır.
    private const ulong Implausible = 1UL << 62;

    /// <summary>Yüzde 0–100; aralıkta hiç süre geçmediyse ya da sayaçlar geri gittiyse null (önceki değer gösterilmeye devam eder).</summary>
    public static double? Percent(CpuTimes previous, CpuTimes current)
    {
        ulong idle = unchecked(current.Idle - previous.Idle);
        ulong kernel = unchecked(current.Kernel - previous.Kernel);
        ulong user = unchecked(current.User - previous.User);
        if (idle >= Implausible || kernel >= Implausible || user >= Implausible) return null;
        var total = kernel + user;
        if (total == 0) return null;
        var busy = total > idle ? total - idle : 0;
        return Math.Clamp(busy * 100.0 / total, 0, 100);
    }
}

/// <summary>Bayt ve süre metinleri (arayüz dilinde; Geri Dönüşüm Kutusu, sistem durumu).</summary>
public static class MeasureText
{
    private static readonly string[] Units = ["KB", "MB", "GB", "TB", "PB"];

    /// <summary>"812 bayt", "4,5 KB", "44 MB", "1,2 GB": 10'dan küçükse bir ondalık, değilse tam sayı (1024 tabanı, Windows gibi).</summary>
    public static string Bytes(long bytes)
    {
        if (bytes < 1024) return L.P(Math.Max(0, bytes), "{0} bayt");
        double value = bytes;
        var unit = -1;
        do
        {
            value /= 1024;
            unit++;
        } while (value >= 1024 && unit < Units.Length - 1);
        var number = value < 10 ? Math.Floor(value * 10) / 10 : Math.Floor(value);
        return number.ToString(value < 10 ? "0.#" : "0", L.Culture) + " " + Units[unit];
    }

    /// <summary>Açık kalma süresi: "3 gün 4 sa", "5 sa 12 dk", "12 dk".</summary>
    public static string Uptime(TimeSpan span)
    {
        if (span.TotalDays >= 1) return L.F("{0} gün {1} sa", (int)span.TotalDays, span.Hours);
        if (span.TotalHours >= 1) return L.F("{0} sa {1} dk", (int)span.TotalHours, span.Minutes);
        return L.F("{0} dk", Math.Max(0, (int)span.TotalMinutes));
    }
}
