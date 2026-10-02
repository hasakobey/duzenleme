using System.Windows;
using Duzenleme.Widgets;

namespace Duzenleme.Views;

/// <summary>
/// Windows'un masaüstü simgeleri için seçim (kalıcı değil: ayarlarda <c>FencesReplaceIcons</c> olarak durur). 2.2: iki seçenek;
/// "Kutuya koyduğum simgeler masaüstünden kalksın" ayrı bir anahtardır (<see cref="DesktopModes.SetBoxLeaves"/>).
/// </summary>
internal enum IconMode
{
    /// <summary>Windows'un simgeleri masaüstünde görünür.</summary>
    ShowAll,

    /// <summary>Simgeler yalnızca bölmelerde; Windows'un simgeleri gizlenir. AppSettings.FencesReplaceIcons.</summary>
    FencesOnly,
}

/// <summary>
/// "Windows masaüstü simgeleri" seçimi ve "Kutuya koyduğum simgeler masaüstünden kalksın" anahtarı: Ayarlar, karşılama ve
/// "Widget ekle"deki "Masaüstümü bölmelere ayır" aynı metinleri ve aynı davranışı buradan alır. İki seçim birbirinden
/// bağımsızdır: bölme kipinde kutudaki simgeler dosyaya dokunulmadan bölmelerden kalkar, bölmeler kapanınca (anahtar açıksa)
/// masaüstünde görünen kutu simgelerinin kalkması sorulur (<see cref="AfterFencesOff"/>). Dosya taşıyan adımlar sorulur;
/// geri koyma sorulmaz, bildirimdeki "Geri al" ile geri alınır.
/// </summary>
internal static class DesktopModes
{
    public static IconMode Current => AppHost.Settings.FencesReplaceIcons ? IconMode.FencesOnly : IconMode.ShowAll;

    /// <summary>Seçim kutularındaki sıra.</summary>
    public static readonly IconMode[] Choices = [IconMode.ShowAll, IconMode.FencesOnly];

    public static string Label(IconMode mode) => mode switch
    {
        IconMode.FencesOnly => L.T("Yalnızca bölmelerde göster"),
        _ => L.T("Hepsi masaüstünde görünsün"),
    };

    public static string Description(IconMode mode) => mode switch
    {
        IconMode.FencesOnly => L.T(
            "Masaüstündeki her şey bölmelerde toplanır; Windows'un kendi simgeleri gizlenir. Dosyalarına dokunulmaz; kapatınca simgeler geri gelir."),
        _ => L.T("Windows'un masaüstü simgeleri her zamanki gibi görünür; bölmeler dosyalara dokunmadan kısayol gösterir."),
    };

    /// <summary>"Kutuya koyduğum simgeler masaüstünden kalksın" anahtarının açıklaması (şu anki duruma göre).</summary>
    public static string BoxLeavesDescription()
    {
        if (!BoxMover.On) return L.T("Kutudaki ve halkadaki simgeler masaüstünde de görünür.");
        if (BoxMover.HidesFromFences)
            return L.T("Kutuya ya da halkaya koyduğun simge bölmelerde görünmez; çıkarınca geri gelir. Dosyalara dokunulmaz.");
        var text = L.F("Kutuya ya da halkaya koyduğun masaüstü simgesi masaüstünden kalkar; çıkarınca geri gelir. Silinmez, {0} klasöründe durur.",
            BoxMover.Root);
        if (Core.BoxPlan.IsOneDrivePath(AppHost.DesktopDirectory))
            text += " " + L.T("Masaüstün OneDrive'da: kalkan simgeler öbür bilgisayarlarında da masaüstünden kalkar.");
        return text;
    }

    /// <summary>"Yeni widget'ların yeri" seçenekleri (Ayarlar).</summary>
    public static string PlaceLabel(Core.PlaceMode mode) => mode switch
    {
        Core.PlaceMode.Center => L.T("Etkin ekranın ortasına"),
        Core.PlaceMode.Corner => L.T("Köşeye (türüne göre)"),
        _ => L.T("İmlecin yanına"),
    };

    /// <summary>Ayarı yazar; açık sayfalar SettingsChanged ile güncellenir.</summary>
    public static void SetPlacement(Core.PlaceMode mode)
    {
        AppHost.Settings.NewWidgetPlacement = Core.PlaceModes.ToSetting(mode);
        AppHost.SaveSettings();
    }

    /// <summary>
    /// Simge seçimini uygular. Bölmeler açılınca kutulardaki öğelere dokunulmaz (bölmeler onları göstermez); kapanınca
    /// "Kutuya koyduğum simgeler masaüstünden kalksın" açıksa masaüstünde görünen kutu simgelerinin kalkması sorulur.
    /// near: bölmeler eklenecekse hangi ekrana. owner: soru penceresinin sahibi (yoksa ekranın ortasında, en üstte).
    /// announce: sonucu bildirimle söyle ("Geri al"lı); karşılama kendi özetini verir, false geçer (sorular yine sorulur).
    /// allStarters: bölmeler açılırken bütün başlangıç bölmeleri kurulsun ("Masaüstümü bölmelere ayır"; bölmeler zaten
    /// açıksa da eksikleri eklenir). trayHint: pencere gizliyken tepsi balonundaki "Geri al" ipucu.
    /// </summary>
    public static void Set(IconMode mode, NativeMethods.POINT? near, Window? owner, bool announce = true, bool allStarters = false,
        string? trayHint = null)
    {
        var current = Current;
        if (mode == current)
        {
            // "Masaüstümü bölmelere ayır" bölmeler zaten açıkken: kip değişmez, eksik başlangıç bölmeleri eklenir.
            if (mode == IconMode.FencesOnly && allStarters) DesktopFences.TurnOnWithNotice(allStarters: true, near, trayHint);
            return;
        }

        switch (mode)
        {
            case IconMode.FencesOnly:
                // Kutu anahtarı değişmez: bölme kipinde kutudaki simgeler dosyaya dokunulmadan bölmelerden kalkar.
                if (announce) DesktopFences.TurnOnWithNotice(allStarters, near, trayHint);
                else DesktopFences.TurnOn(allStarters, near);
                break;

            default:
                if (!AppHost.Settings.FencesReplaceIcons) return;
                DesktopFences.TurnOff();
                if (announce) Notice.Show(L.T("Masaüstü simgeleri yeniden gösteriliyor. Bölmelerin yerinde duruyor."), NoticeKind.Info);
                AfterFencesOff(owner, near, automatic: false);
                break;
        }
    }

    /// <summary>
    /// "Kutuya koyduğum simgeler masaüstünden kalksın" anahtarı (Ayarlar). Açınca: Windows simgeleri görünürken kutularda
    /// masaüstünde de duran simgeler varsa kalkmaları sorulur (herkesin masaüstündekiler ayrı izinle); bölme kipinde soru
    /// yok (dosyaya dokunulmaz). Kapatınca: taşınanların hepsi masaüstüne döner (kutularda kalırlar); bildirimdeki "Geri al"
    /// anahtarı yeniden açar ve onları sormadan yeniden kutulara taşır.
    /// </summary>
    public static void SetBoxLeaves(bool on, NativeMethods.POINT? near, Window? owner)
    {
        if (on == BoxMover.On)
        {
            if (on) BoxMover.SetOn(true); // "soruldu" yazılsın
            return;
        }
        BoxMover.SetOn(on);
        if (on)
        {
            if (BoxMover.MovesFiles) ClaimExisting(owner, near, automatic: false);
            return;
        }
        if (BoxMover.MovedCount > 0)
            BoxMover.ReturnAll(undo: paths =>
            {
                BoxMover.SetOn(true);
                BoxMover.ReclaimPaths(paths);
            });
    }

    /// <summary>
    /// Bölmeler masaüstünü yönetmeyi bıraktı (Ayarlar, bölmeleri "Geri al", son bölme kaldırıldı): anahtar açıksa kutularda
    /// masaüstünde de görünen simgelerin kalkması sorulur. automatic: kullanıcı bu soruyu beklemiyor (bildirim şeridinde
    /// "Kaldır" / "Şimdi değil"); değilse küçük onay penceresi.
    /// </summary>
    public static void AfterFencesOff(Window? owner, NativeMethods.POINT? near, bool automatic)
    {
        if (!BoxMover.MovesFiles) return;
        ClaimExisting(owner, near, automatic);
    }

    /// <summary>Kutularda masaüstünde de duran simgeler kalksın mı (sayılar masaüstünün anlık görüntüsünden)?</summary>
    private static void ClaimExisting(Window? owner, NativeMethods.POINT? near, bool automatic)
    {
        var counts = BoxMover.ClaimCounts();
        if (counts.Movable.Count == 0) return;
        var text = L.P(counts.Movable.Count,
            "Kutularında ve halkalarında masaüstünde de duran {0} simge var. Masaüstünden kalkar, {1} klasöründe durur ve kutularda görünmeye devam eder.",
            BoxMover.Root);
        if (counts.PublicOnly.Count > 0)
            text += " " + L.P(counts.PublicOnly.Count, "Herkesin masaüstündeki {0} simge kalır; onlar için ayrı izin gerekir.");
        var existing = BoxMover.DesktopItemsInBoxes();
        if (automatic)
        {
            Notice.Ask(text, NoticeKind.Info,
                [new NoticeChoice(L.T("Kaldır"), () => BoxMover.ClaimMany(existing)), new NoticeChoice(L.T("Şimdi değil"), () => { })],
                L.T("Seçmek için tıkla."));
            return;
        }
        if (Confirm.Ask(owner, L.T("Kutudaki simgeler de masaüstünden kalksın mı?"), text, L.T("Kaldır"), danger: false,
                cancelText: L.T("Şimdi değil"), near: near))
            BoxMover.ClaimMany(existing);
    }

    /// <summary>
    /// "Herkesin masaüstündeki simgeler de kalksın" (Ayarlar). Açarken kutularda herkesin masaüstünde duran simgeler varsa
    /// sorulur ve hepsi tek yönetici onayıyla taşınır; ayar yalnızca taşıma başarılı olunca (ya da taşınacak bir şey yoksa)
    /// açılır. Kapatmak yalnızca ayarı yazar (taşınmış olanlar kutudan çıkınca yine geri döner). done: ayarın son hali.
    /// </summary>
    public static void SetBoxPublic(bool on, Window? owner, Action? done = null)
    {
        var settings = AppHost.Settings;
        if (!on)
        {
            settings.BoxIncludesPublicDesktop = false;
            AppHost.SaveSettings();
            done?.Invoke();
            return;
        }
        settings.BoxPublicAsked = true;
        var items = BoxMover.PublicItemsInBoxes();
        if (items.Count == 0)
        {
            settings.BoxIncludesPublicDesktop = true;
            AppHost.SaveSettings();
            done?.Invoke();
            return;
        }
        var count = items.SelectMany(i => i.Paths).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (!Confirm.Ask(owner, L.T("Herkesin masaüstündeki simgeler de kalksın mı?"),
                L.P(count, "Kutularında herkesin masaüstünde duran {0} simge var. Kaldırırsam bu bilgisayardaki herkesin masaüstünden kalkar; Windows bir kez yönetici izni ister."),
                L.T("Kaldır"), danger: false))
        {
            AppHost.SaveSettings();
            done?.Invoke();
            return;
        }
        AppHost.SaveSettings();
        BoxMover.ClaimMany(items, includePublic: true, publicMoved: moved =>
        {
            if (moved > 0)
            {
                settings.BoxIncludesPublicDesktop = true;
                AppHost.SaveSettings();
            }
            done?.Invoke();
        });
    }
}
