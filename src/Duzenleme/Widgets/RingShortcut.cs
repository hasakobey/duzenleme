using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// "Tuşla imlecin yanında açılsın": halka kısayolunu açıp kapama akışının tek yeri (Ayarlar > Halka menü ve widget menüsü).
/// Store: kısayol varsayılan olarak kapalı ve atanmamıştır, açılırken ne yaptığı anlatılır. Açma
/// <see cref="RingHotkey.TurnOn"/> ile (öneri → kayıt → yalnızca halkanın kaydı başarısızsa sıradaki öneri); kapama
/// Set(Ring, "") + Apply. Her değişiklikten sonra açılır halka ayara uyar (<see cref="Views.RingPopup.Sync"/>: açıksa boşta
/// önceden kurulur, kapalıysa bütünüyle kapanır).
/// </summary>
internal static class RingShortcut
{
    /// <summary>Halka kısayolu atanmış mı?</summary>
    public static bool IsOn => !string.IsNullOrEmpty(AppHost.Settings.Hotkeys.Ring);

    /// <summary>Atanmış halka kısayolu (yoksa null).</summary>
    public static string? Current => IsOn ? AppHost.Settings.Hotkeys.Ring : null;

    /// <summary>Halka kısayolunun kaydı başarısız olduysa nedeni.</summary>
    public static string? Failure =>
        IsOn && AppHost.Hotkeys?.Failures.TryGetValue(HotkeyAction.Ring, out var failure) == true ? failure : null;

    /// <summary>Kısayolu açar: önerileri sırayla dener; hiçbiri kaydedilemezse halka kapalı kalır (sonuçta neden).</summary>
    public static RingHotkey.TurnOnResult TurnOn()
    {
        var result = RingHotkey.TurnOn(AppHost.Settings.Hotkeys, Apply);
        AppHost.SaveSettings();
        Views.RingPopup.Sync();
        return result;
    }

    /// <summary>Kısayolu kapatır (kaydı kalkar, açılır halka bütünüyle kapanır).</summary>
    public static void TurnOff()
    {
        AppHost.Settings.Hotkeys.Set(HotkeyAction.Ring, "");
        Apply(AppHost.Settings.Hotkeys);
        AppHost.SaveSettings();
        Views.RingPopup.Sync();
    }

    private static IReadOnlyDictionary<HotkeyAction, string> Apply(HotkeySettings settings)
    {
        if (AppHost.Hotkeys is not { } hotkeys) return new Dictionary<HotkeyAction, string>();
        hotkeys.Apply(settings);
        return hotkeys.Failures;
    }

    /// <summary>Kısayol açık ve bu halkayı mı açıyor (widget menüsündeki seçenek yalnızca o halkada işaretli)?</summary>
    public static bool Opens(WidgetConfig config) =>
        IsOn && RingTargets.Opens(Launchers.All(AppHost.Settings), AppHost.Settings.RingWidgetId, config);

    /// <summary>
    /// Widget menüsündeki seçenek: kapalıysa açar (bu halka hedef olur) ve ne yaptığını bildirimde anlatır (matris 2b); açık
    /// ve bu halkayı açıyorsa kapatır; açık ama başka halkayı açıyorsa bu halkayı hedef yapar.
    /// </summary>
    public static void ToggleFromMenu(WidgetConfig config)
    {
        if (Opens(config))
        {
            TurnOff();
            Views.Notice.Show(L.T("Halka menü kısayolu kapatıldı."), Views.NoticeKind.Info);
            return;
        }
        AppHost.Settings.RingWidgetId = config.Id;
        if (Current is { } current)
        {
            // Kısayol zaten açık, başka bir halkayı açıyordu: artık bunu açar (açılır halka boşta yeniden kurulur).
            AppHost.SaveSettings();
            Views.Notice.Show(L.F("{0} artık bu halkayı imlecin yanında açar.", current), Views.NoticeKind.Info);
            return;
        }
        var result = TurnOn();
        if (result.Hotkey is { } hotkey)
            Views.Notice.Show(TurnedOnText(hotkey), Views.NoticeKind.Info, L.T("Ayarları aç"), OpenSettings,
                L.T("Kısayolu değiştirmek için buraya tıkla."));
        else
            Views.Notice.Show(NoShortcutText(result.Failure), Views.NoticeKind.Warning, L.T("Ayarları aç"), OpenSettings,
                L.T("Kısayolu seçmek için buraya tıkla."));
    }

    /// <summary>
    /// Kısayol açılınca ne yaptığının açıklaması (Store matrisi 2b): widget menüsünden ya da Ayarlar'daki kısayol listesinden
    /// açılınca bildirimde gösterilir (Halka menü bölümünde aynı açıklama anahtarın altında yazar).
    /// </summary>
    public static string TurnedOnText(string hotkey) =>
        L.F("Halka menü artık {0} ile imlecin yanında açılır. Tıkla ya da 1–9'a bas; Esc kapatır. {1} klavyeni izlemez: yalnızca bu kısayol Windows'a kaydedilir.",
            hotkey, AppInfo.Name);

    /// <summary>Hiçbir öneri kaydedilemedi: kullanıcı kendi birleşimini seçsin.</summary>
    public static string NoShortcutText(string? failure) => failure is null
        ? L.T("Halka menü için boş bir kısayol bulunamadı. Ayarlar'daki Klavye kısayolları bölümünde kendin bir birleşim seç.")
        : L.F("Halka menü için önerilen kısayollar kaydedilemedi ({0}). Ayarlar'daki Klavye kısayolları bölümünde kendin bir birleşim seç.", failure);

    private static void OpenSettings() => (System.Windows.Application.Current as App)?.ShowPage(typeof(Views.SettingsPage));

    /// <summary>
    /// Halka menü widget'ı ekler (Ayarlar'daki düğme, "henüz halka menün yok" bildirimi): gizli masaüstü ya da göz atma biter,
    /// widget <paramref name="near"/> noktasının ekranına eklenir.
    /// </summary>
    public static WidgetConfig? AddRing(NativeMethods.POINT? near)
    {
        AppHost.EnsureWidgetsShown();
        var choice = Views.WidgetCatalog.Tools.FirstOrDefault(c => c.Key == "Ring");
        return choice is null ? null : Views.WidgetCatalog.Invoke(choice, near);
    }
}
