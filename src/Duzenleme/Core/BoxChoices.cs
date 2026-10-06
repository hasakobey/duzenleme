using System.IO;

namespace Duzenleme.Core;

/// <summary>Kutu ya da halka öğesinin "masaüstünden kalksın" açısından durumu (öğe menüsü; yalnızca çalışma zamanı).</summary>
public enum BoxItemState
{
    /// <summary>Kutu bir bağlantı gösterir (uygulama, masaüstünde değil, bulunamadı, korunan, kuralın klasörü…).</summary>
    Link,

    /// <summary>Masaüstünden kalkmış, NestDesk klasöründe: kaldırınca masaüstüne geri döner.</summary>
    Moved,

    /// <summary>Öğe masaüstünde ve taşınabilir: "Masaüstünden kaldır" (seçenek kapalıyken de, tek tek).</summary>
    OnDesktop,

    /// <summary>Herkesin masaüstünde ve ona izin verilmedi; tek seferlik yönetici onayıyla kalkabilir.</summary>
    PublicNeedsAdmin,
}

/// <summary>Öğe menüsündeki kutu satırının işi.</summary>
public enum BoxMenuAction
{
    /// <summary>"Kutudan kaldır" / "Halkadan kaldır" (Delete): taşınmışsa masaüstüne döner.</summary>
    Remove,

    /// <summary>"Masaüstünden kaldır": masaüstündeki öğe masaüstünden kalkar, kutuda durur.</summary>
    LeaveDesktop,

    /// <summary>"Masaüstünden kaldır (yönetici izni)": herkesin masaüstündeki öğe tek seferlik yönetici onayıyla kalkar.</summary>
    LeaveDesktopAsAdmin,

    /// <summary>"Masaüstüne geri koy": masaüstünden kalkmış öğe masaüstüne döner, kutuda kalır.</summary>
    ReturnToDesktop,
}

/// <summary>Öğe menüsünün kutu satırı: işi, başlığı ve ipucu.</summary>
public sealed record BoxMenuRow(BoxMenuAction Action, string Label, string? ToolTip);

/// <summary>Kutuya bırakılınca ne olur (bırakma kaplamasının metni için; yalnızca çalışma zamanı).</summary>
public enum BoxDropEffect
{
    /// <summary>Bırakılanlar yalnızca bağlantı olur (seçenek kapalı ya da masaüstünden bir şey yok).</summary>
    None,

    /// <summary>Masaüstündeki dosya NestDesk klasörüne taşınır ("masaüstünden kalkar").</summary>
    Moves,

    /// <summary>Bölmeler masaüstünü yönetiyor: dosyaya dokunulmaz, bölmede görünmez ("bölmeden kalkar").</summary>
    Hides,
}

/// <summary>
/// "Widget'lara koyduğum öğeler masaüstünden kalksın": soruların, öğe menüsünün ve bırakma kaplamasının saf kararları. Diske
/// dokunmaz (öznitelikleri çağıran verir: arayüzde masaüstünün anlık görüntüsünden ya da eklerken arka planda okunanlardan).
/// Testlenir.
/// </summary>
public static class BoxChoices
{
    /// <summary>
    /// Taşıma ya da geri koyma neden olmadı (bildirimdeki parantez içi): her zaman sabit, çevrilmiş bir neden. İstisnanın iletisi
    /// (yol, kullanıcı adı, Windows'un dilindeki metin) kullanıcıya gösterilmez; hata günlüğe yazılır.
    /// </summary>
    public static string FailureText(BoxFailure failure) => failure switch
    {
        BoxFailure.Locked => L.T("kullanımda"),
        BoxFailure.Denied => L.T("izin yok"),
        BoxFailure.Declined => L.T("izin verilmedi"),
        BoxFailure.Unavailable => L.T("klasöre şu an ulaşılamıyor"),
        _ => L.T("beklenmeyen bir dosya hatası"),
    };

    /// <summary>Seçenek soruldu mu? Açıksa sorulmuş sayılır (2.1'den açık gelen ya da Ayarlar'dan açılan).</summary>
    public static bool Asked(AppSettings settings) => settings.BoxLeaveAsked || settings.BoxItemsLeaveDesktop;

    private static BoxLinkReason DecideOnDesktop(string path, Func<string, FileAttributes?> attributes, BoxContext context, bool includePublic)
    {
        var desktops = context.PublicDesktops.Prepend(context.UserDesktop).ToList();
        var onDesktop = !TileItemPath.IsShellObject(path) && BoxPlan.IsDirectlyIn(path, desktops);
        return BoxPlan.Decide(path, onDesktop ? attributes(path) : null, context.UserDesktop, context.PublicDesktops, includePublic, context.Kept);
    }

    /// <summary>
    /// İlk kez sorulacak mı ("Masaüstündeki simge de kalksın mı?"): seçenek kapalı ve sorulmadı, eklenenlerden en az biri
    /// masaüstünde ve taşınabilir (ortak masaüstü izni sayılmaz; klasör, korunan ve masaüstünde olmayan hariç).
    /// </summary>
    public static bool AskSwitch(bool asked, bool on, IEnumerable<string> added, Func<string, FileAttributes?> attributes, BoxContext context) =>
        !asked && !on && added.Any(p => DecideOnDesktop(p, attributes, context, includePublic: true) == BoxLinkReason.None);

    /// <summary>
    /// Herkesin masaüstündeki öğeler: taşınabilecekken yalnızca ayrı izin yüzünden kalanlar (bu eklemeden). Soru
    /// (<see cref="AskPublic"/>) ve Store sürümünün bildirimi bunlara bakar.
    /// </summary>
    public static List<string> PublicOnly(IEnumerable<string> added, Func<string, FileAttributes?> attributes, BoxContext context) =>
        added.Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(p => DecideOnDesktop(p, attributes, context, includePublic: false) == BoxLinkReason.PublicDesktop &&
                        DecideOnDesktop(p, attributes, context, includePublic: true) == BoxLinkReason.None)
            .ToList();

    /// <summary>
    /// Herkesin masaüstü sorusu sorulacak mı: dosyalar taşınıyor (bölme kipi değil), daha önce sorulmadı, izin yok, Store
    /// sürümü değil ve eklenenlerde böyle bir öğe var. Store sürümü bunun yerine bir kez bildirim gösterir.
    /// </summary>
    public static bool AskPublic(bool movesFiles, bool publicAsked, bool includePublic, bool packaged, IReadOnlyCollection<string> publicOnly) =>
        movesFiles && !publicAsked && !includePublic && !packaged && publicOnly.Count > 0;

    /// <summary>
    /// Öğenin durumu: bulunamayan öğe hep bağlantıdır (geri koyma satırı yok); taşınmış öğe <see cref="BoxItemState.Moved"/>;
    /// masaüstündeki taşınabilir öğe <see cref="BoxItemState.OnDesktop"/> (anahtar kapalıyken de: tek tek kaldırılabilir); izin
    /// verilmemiş herkesin masaüstü öğesi Store sürümünde değilse <see cref="BoxItemState.PublicNeedsAdmin"/>.
    /// </summary>
    /// <param name="canMove">Dosyalar elle taşınabilir mi: bölmeler masaüstünü yönetirken ve anahtar açıkken öğe zaten
    /// bölmelerde görünmez (taşıma satırı yok).</param>
    /// <param name="decision">Öğe için <see cref="BoxPlan.Decide"/> (ayardaki ortak masaüstü izniyle).</param>
    public static BoxItemState StateOf(bool moved, bool missing, bool canMove, BoxLinkReason decision, bool packaged)
    {
        if (missing) return BoxItemState.Link;
        if (moved) return BoxItemState.Moved;
        if (!canMove) return BoxItemState.Link;
        return decision switch
        {
            BoxLinkReason.None => BoxItemState.OnDesktop,
            BoxLinkReason.PublicDesktop when !packaged => BoxItemState.PublicNeedsAdmin,
            _ => BoxItemState.Link,
        };
    }

    /// <summary>
    /// Öğe menüsünün kutu satırları (kutu ve halka aynı): her öğede tek kaldırma fiili ("Kutudan kaldır" / "Halkadan
    /// kaldır"); masaüstündeki taşınabilir öğede "Masaüstünden kaldır", masaüstünden kalkmış öğede "Masaüstüne geri koy"
    /// (öğe kutuda kalır).
    /// </summary>
    /// <param name="heldElsewhere">Öğe başka bir kutuda, halkada ya da sekmede de duruyor (kaldırınca masaüstüne dönmez).</param>
    public static List<BoxMenuRow> MenuRows(BoxItemState state, bool ring, bool heldElsewhere)
    {
        var removeTip = state switch
        {
            BoxItemState.Moved when heldElsewhere => L.T("Başka bir kutuda ya da halkada da duruyor; yalnızca buradan kalkar."),
            BoxItemState.Moved => L.T("Simge masaüstüne geri döner."),
            _ => ring
                ? L.T("Yalnızca halkadan çıkar; dosyaya dokunulmaz.")
                : L.T("Yalnızca kısayol kutudan çıkar; dosyaya dokunulmaz.\nGeri almak için: kutuya sağ tık → Geri al"),
        };
        var rows = new List<BoxMenuRow> { new(BoxMenuAction.Remove, ring ? L.T("Halkadan kaldır") : L.T("Kutudan kaldır"), removeTip) };
        switch (state)
        {
            case BoxItemState.OnDesktop:
                rows.Add(new BoxMenuRow(BoxMenuAction.LeaveDesktop, L.T("Masaüstünden kaldır"),
                    ring ? L.T("Simge masaüstünden kalkar, halkada durur.") : L.T("Simge masaüstünden kalkar, kutuda durur.")));
                break;
            case BoxItemState.Moved:
                rows.Add(new BoxMenuRow(BoxMenuAction.ReturnToDesktop, L.T("Masaüstüne geri koy"),
                    ring ? L.T("Simge masaüstüne döner, halkada da kalır.") : L.T("Simge masaüstüne döner, kutuda da kalır.")));
                break;
            case BoxItemState.PublicNeedsAdmin:
                rows.Add(new BoxMenuRow(BoxMenuAction.LeaveDesktopAsAdmin, L.T("Masaüstünden kaldır (yönetici izni)"),
                    L.T("Bu bilgisayardaki herkesin masaüstünden kalkar; Windows bir kez yönetici izni ister.")));
                break;
        }
        return rows;
    }

    /// <summary>
    /// Bırakılacak yollar kutuya girince masaüstünden kalkar mı (kaplamanın metni)? Seçenek açıksa ve en az biri masaüstündeyse:
    /// bölmeler masaüstünü yönetiyorsa <see cref="BoxDropEffect.Hides"/>, değilse (izin verilen masaüstündeyse)
    /// <see cref="BoxDropEffect.Moves"/>. Yalnızca metin karşılaştırır (sürükleme sırasında diske dokunmaz).
    /// </summary>
    /// <param name="kept">Masaüstünde kalacak mı (kuralın klasörü, halka komutunun yolu). Verilmezse context.Kept.</param>
    public static BoxDropEffect DropEffect(IEnumerable<string> paths, bool on, bool fencesManage, BoxContext context,
        Func<string, bool>? kept = null)
    {
        if (!on) return BoxDropEffect.None;
        var all = context.PublicDesktops.Prepend(context.UserDesktop).ToList();
        var movable = context.IncludePublic ? all : [context.UserDesktop];
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || TileItemPath.IsShellObject(path)) continue;
            if (fencesManage && BoxPlan.IsDirectlyIn(path, all)) return BoxDropEffect.Hides;
            if (!fencesManage && BoxPlan.IsDirectlyIn(path, movable) && (kept ?? context.Kept)?.Invoke(path) != true) return BoxDropEffect.Moves;
        }
        return BoxDropEffect.None;
    }
}
