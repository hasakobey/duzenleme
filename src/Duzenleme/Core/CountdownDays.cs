namespace Duzenleme.Core;

/// <summary>Geri sayımın günü: kalan gün (geçtiyse eksi) ve sayılan tarih (her yıl yinelenende bu yılki ya da gelecek yılki).</summary>
public readonly record struct CountdownDay(int Days, DateTime Date);

/// <summary>Geri sayım hesabı (saf; testlenir). Yalnızca tarih: saat ve saat dilimi önemsizdir, gece yarısı değişir.</summary>
public static class CountdownDays
{
    public static CountdownDay For(DateTime target, DateTime today, bool yearly)
    {
        var day = target.Date;
        today = today.Date;
        if (yearly)
        {
            day = OnYear(day, today.Year);
            if (day < today) day = OnYear(target.Date, today.Year + 1);
        }
        return new CountdownDay((day - today).Days, day);
    }

    /// <summary>Aynı gün ve ay, verilen yılda (29 Şubat artık olmayan yılda 28 Şubat).</summary>
    private static DateTime OnYear(DateTime date, int year) =>
        new(year, date.Month, Math.Min(date.Day, DateTime.DaysInMonth(year, date.Month)));
}
