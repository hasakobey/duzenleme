using System.Globalization;

namespace Duzenleme.Core;

/// <summary>
/// Aylık takvimin ızgarası (saf; testlenir). Her ay sabit 6 satır × 7 sütundur: ay değişince widget'ın boyu oynamaz,
/// komşusunun üstüne binmez. İlk hücre, ayın 1'inden önceki (ya da o günkü) haftanın ilk günüdür.
/// </summary>
public static class MonthGrid
{
    public const int Rows = 6, Columns = 7, Cells = Rows * Columns;

    /// <summary>Izgaranın ilk günü: ayın 1'i ya da ondan önceki en yakın <paramref name="firstDay"/>.</summary>
    public static DateTime FirstCell(int year, int month, DayOfWeek firstDay)
    {
        var first = new DateTime(year, month, 1);
        var offset = ((int)first.DayOfWeek - (int)firstDay + 7) % 7;
        return first.AddDays(-offset);
    }

    /// <summary>42 gün (6 hafta), soldan sağa, yukarıdan aşağı.</summary>
    public static IReadOnlyList<DateTime> Days(int year, int month, DayOfWeek firstDay)
    {
        var start = FirstCell(year, month, firstDay);
        var days = new DateTime[Cells];
        for (var i = 0; i < Cells; i++) days[i] = start.AddDays(i);
        return days;
    }

    /// <summary>Sütun başlıklarının günleri, haftanın ilk gününden başlayarak.</summary>
    public static IReadOnlyList<DayOfWeek> WeekdayOrder(DayOfWeek firstDay) =>
        Enumerable.Range(0, Columns).Select(i => (DayOfWeek)(((int)firstDay + i) % 7)).ToArray();

    /// <summary>
    /// Satırın ISO-8601 hafta numarası: satırdaki Pazartesi'nin haftası (Pazar ile başlayan takvimde de Pazartesi'ye bakılır).
    /// </summary>
    public static int WeekNumber(DateTime rowStart, DayOfWeek firstDay)
    {
        var toMonday = ((int)DayOfWeek.Monday - (int)firstDay + 7) % 7;
        return ISOWeek.GetWeekOfYear(rowStart.AddDays(toMonday));
    }

    /// <summary>Hafta sonu mu (Cumartesi, Pazar)?</summary>
    public static bool IsWeekend(DateTime day) => day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>
    /// Haftanın ilk günü: ayar (0 = Pazar, 1 = Pazartesi…) ya da yoksa kültürün (Türkçede Pazartesi, en-US'de Pazar).
    /// </summary>
    public static DayOfWeek FirstDay(int? setting, CultureInfo culture) =>
        setting is >= 0 and <= 6 ? (DayOfWeek)setting.Value : culture.DateTimeFormat.FirstDayOfWeek;

    /// <summary>Bir ay ileri/geri (ayın 1'i).</summary>
    public static DateTime AddMonths(DateTime month, int delta) => new DateTime(month.Year, month.Month, 1).AddMonths(delta);
}
