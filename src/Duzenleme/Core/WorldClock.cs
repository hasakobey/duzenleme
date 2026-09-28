namespace Duzenleme.Core;

/// <summary>Bir saat dilimindeki an: yerel saat, buradakine göre gün farkı (-1 dün, +1 yarın) ve saat farkı (dakika).</summary>
public readonly record struct ZoneTime(DateTime Time, int DayDelta, int OffsetMinutes);

/// <summary>Dünya saatinin hesabı (saf; testlenir). Yaz saati <see cref="TimeZoneInfo"/>'dan gelir; :30 ve :45 dilimler doğru.</summary>
public static class WorldClock
{
    public static ZoneTime At(DateTime utcNow, TimeZoneInfo here, TimeZoneInfo zone)
    {
        var utc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var there = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, here);
        var offset = (int)Math.Round((zone.GetUtcOffset(utc) - here.GetUtcOffset(utc)).TotalMinutes);
        return new ZoneTime(there, (there.Date - local.Date).Days, offset);
    }

    /// <summary>"+6 sa", "−3 sa", "+2 sa 30 dk", "−45 dk", aynıysa "aynı saat".</summary>
    public static string OffsetText(int minutes)
    {
        if (minutes == 0) return L.T("aynı saat");
        var sign = minutes > 0 ? "+" : "−";
        var abs = Math.Abs(minutes);
        int hours = abs / 60, rest = abs % 60;
        if (rest == 0) return sign + L.F("{0} sa", hours);
        return hours == 0 ? sign + L.F("{0} dk", rest) : sign + L.F("{0} sa {1} dk", hours, rest);
    }

    /// <summary>"Yarın", "Dün" ya da aynı günse boş.</summary>
    public static string DayText(int delta) => delta switch
    {
        > 0 => L.T("Yarın"),
        < 0 => L.T("Dün"),
        _ => "",
    };

    /// <summary>24 saatlik mi: widget'ın ayarı (<see cref="WidgetConfig.Clock12Hour"/>), yoksa Windows'un bölge ayarı.</summary>
    public static bool Uses24Hour(bool? twelveHourSetting) => twelveHourSetting is { } twelve ? !twelve : L.Uses24Hour;

    /// <summary>"15:07" ya da "3:07" (12 saatlikte ÖÖ/ÖS ayrı gösterilir: <see cref="Designator"/>).</summary>
    public static string Time(DateTime time, bool h24, System.Globalization.CultureInfo culture) =>
        time.ToString(h24 ? "HH:mm" : "h:mm", culture);

    /// <summary>12 saatlikte "ÖÖ"/"ÖS" (İngilizcede "AM"/"PM"); kültürde yoksa AM/PM.</summary>
    public static string Designator(DateTime time, System.Globalization.CultureInfo culture)
    {
        var text = time.Hour < 12 ? culture.DateTimeFormat.AMDesignator : culture.DateTimeFormat.PMDesignator;
        return string.IsNullOrEmpty(text) ? (time.Hour < 12 ? "AM" : "PM") : text;
    }

    /// <summary>Saat dilimi bulunamazsa (bu Windows'ta yok, kayıt bozuk) null.</summary>
    public static TimeZoneInfo? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or System.Security.SecurityException) { return null; }
    }
}

/// <summary>Seçilebilir şehirler: iki dilde ad ve Windows saat dilimi kimliği. Veri: çeviri tablosundan geçmez.</summary>
public sealed record WorldCity(string Tr, string En, string ZoneId)
{
    public string Name => L.Current == Lang.Tr ? Tr : En;
}

public static class WorldCities
{
    /// <summary>Sık seçilen şehirler (Windows kimlikleriyle; IANA değil). Aynı dilimde birden çok şehir olabilir.</summary>
    public static IReadOnlyList<WorldCity> All { get; } =
    [
        new("İstanbul", "Istanbul", "Turkey Standard Time"),                     // l10n: çevrilmez
        new("Ankara", "Ankara", "Turkey Standard Time"),
        new("Londra", "London", "GMT Standard Time"),
        new("Dublin", "Dublin", "GMT Standard Time"),
        new("Lizbon", "Lisbon", "GMT Standard Time"),
        new("Paris", "Paris", "Romance Standard Time"),
        new("Madrid", "Madrid", "Romance Standard Time"),
        new("Brüksel", "Brussels", "Romance Standard Time"),                      // l10n: çevrilmez
        new("Berlin", "Berlin", "W. Europe Standard Time"),
        new("Amsterdam", "Amsterdam", "W. Europe Standard Time"),
        new("Roma", "Rome", "W. Europe Standard Time"),
        new("Viyana", "Vienna", "W. Europe Standard Time"),
        new("Zürih", "Zurich", "W. Europe Standard Time"),                       // l10n: çevrilmez
        new("Stockholm", "Stockholm", "W. Europe Standard Time"),
        new("Varşova", "Warsaw", "Central European Standard Time"),               // l10n: çevrilmez
        new("Atina", "Athens", "GTB Standard Time"),
        new("Bükreş", "Bucharest", "GTB Standard Time"),                          // l10n: çevrilmez
        new("Kiev", "Kyiv", "FLE Standard Time"),
        new("Helsinki", "Helsinki", "FLE Standard Time"),
        new("Moskova", "Moscow", "Russian Standard Time"),
        new("Kahire", "Cairo", "Egypt Standard Time"),
        new("Johannesburg", "Johannesburg", "South Africa Standard Time"),
        new("Lagos", "Lagos", "W. Central Africa Standard Time"),
        new("Nairobi", "Nairobi", "E. Africa Standard Time"),
        new("Riyad", "Riyadh", "Arab Standard Time"),
        new("Doha", "Doha", "Arab Standard Time"),
        new("Dubai", "Dubai", "Arabian Standard Time"),
        new("Bakü", "Baku", "Azerbaijan Standard Time"),                          // l10n: çevrilmez
        new("Tiflis", "Tbilisi", "Georgian Standard Time"),
        new("Tahran", "Tehran", "Iran Standard Time"),
        new("Taşkent", "Tashkent", "West Asia Standard Time"),                    // l10n: çevrilmez
        new("Karaçi", "Karachi", "Pakistan Standard Time"),                       // l10n: çevrilmez
        new("Yeni Delhi", "New Delhi", "India Standard Time"),
        new("Mumbai", "Mumbai", "India Standard Time"),
        new("Katmandu", "Kathmandu", "Nepal Standard Time"),
        new("Dakka", "Dhaka", "Bangladesh Standard Time"),
        new("Bangkok", "Bangkok", "SE Asia Standard Time"),
        new("Cakarta", "Jakarta", "SE Asia Standard Time"),
        new("Singapur", "Singapore", "Singapore Standard Time"),
        new("Kuala Lumpur", "Kuala Lumpur", "Singapore Standard Time"),
        new("Hong Kong", "Hong Kong", "China Standard Time"),
        new("Pekin", "Beijing", "China Standard Time"),
        new("Şanghay", "Shanghai", "China Standard Time"),                        // l10n: çevrilmez
        new("Seul", "Seoul", "Korea Standard Time"),
        new("Tokyo", "Tokyo", "Tokyo Standard Time"),
        new("Sidney", "Sydney", "AUS Eastern Standard Time"),
        new("Melbourne", "Melbourne", "AUS Eastern Standard Time"),
        new("Auckland", "Auckland", "New Zealand Standard Time"),
        new("Honolulu", "Honolulu", "Hawaiian Standard Time"),
        new("Los Angeles", "Los Angeles", "Pacific Standard Time"),
        new("San Francisco", "San Francisco", "Pacific Standard Time"),
        new("Vancouver", "Vancouver", "Pacific Standard Time"),
        new("Denver", "Denver", "Mountain Standard Time"),
        new("Chicago", "Chicago", "Central Standard Time"),
        new("Meksiko", "Mexico City", "Central Standard Time (Mexico)"),
        new("New York", "New York", "Eastern Standard Time"),
        new("Toronto", "Toronto", "Eastern Standard Time"),
        new("Bogota", "Bogotá", "SA Pacific Standard Time"),
        new("São Paulo", "São Paulo", "E. South America Standard Time"),
        new("Buenos Aires", "Buenos Aires", "Argentina Standard Time"),
        new("Reykjavik", "Reykjavik", "Greenwich Standard Time"),
    ];

    /// <summary>Kimliğin tablodaki ilk şehri (satır adı verilmediyse onun adı görünür); yoksa null.</summary>
    public static WorldCity? ForZone(string zoneId) =>
        All.FirstOrDefault(c => string.Equals(c.ZoneId, zoneId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Satırda görünen ad: kullanıcının verdiği ad, yoksa şehir tablosundaki ad, yoksa Windows'un dilim adı (ör. "(UTC+05:30) …").
    /// </summary>
    public static string Label(WorldZone zone, TimeZoneInfo? info)
    {
        if (!string.IsNullOrWhiteSpace(zone.Label)) return zone.Label.Trim();
        if (ForZone(zone.Id) is { } city) return city.Name;
        return info?.DisplayName ?? zone.Id;
    }

    /// <summary>Arama: şehir adında (iki dilde, Türkçe harf duyarsız) geçenler.</summary>
    public static IEnumerable<WorldCity> Search(string query)
    {
        var q = FolderName.Fold(query.Trim());
        return q.Length == 0
            ? All
            : All.Where(c => FolderName.Fold(c.Tr).Contains(q, StringComparison.Ordinal) || FolderName.Fold(c.En).Contains(q, StringComparison.Ordinal));
    }
}
