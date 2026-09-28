namespace Duzenleme.Core;

/// <summary>
/// Hareket kuralları (2.1 P7). Uygulamada yalnızca kısa saydamlık geçişleri (solma) vardır: boyut ya da konum oynamaz,
/// fareyle üstüne gelmek hiçbir şeyi oynatmaz. Geçişler yalnızca Windows'un "Animasyon efektleri" açıkken ve uygulamadaki
/// "Animasyonlar" ayarı kapatılmadıysa oynar. Widget'lar yazılımla çizilen katmanlı pencerelerdir (her karede bütün kart
/// yeniden çizilir): onların solması kısa, seyrek karelidir ve aynı anda çok widget solmaz. Uygulaması <c>Views.Motion</c>.
/// </summary>
public static class MotionPolicy
{
    /// <summary>Pencere, şerit ve sayfa solması (ms).</summary>
    public const int FadeMs = 150;

    /// <summary>Yeni widget'ın vurgu ışımasının sönmesi (ms).</summary>
    public const int GlowFadeMs = 200;

    /// <summary>Hiçbir geçiş bundan uzun sürmez (ms).</summary>
    public const int MaxMs = 200;

    /// <summary>Widget pencerelerinin solmasında saniyedeki kare (katmanlı pencere her karede bütünüyle yeniden çizilir).</summary>
    public const int WidgetFrameRate = 30;

    /// <summary>Aynı anda en çok bu kadar widget solar; daha çoğu (masaüstünü gizle/göz at) solmadan, hemen gizlenip gösterilir.</summary>
    public const int MaxFadedWindows = 8;

    /// <summary>Geçişler oynasın mı: Windows'un "Animasyon efektleri" açık ve uygulamada kapatılmamış.</summary>
    public static bool Enabled(bool windowsAnimations, bool appOff) => windowsAnimations && !appOff;

    /// <summary>Sağ tık menüsü solarak açılsın mı (Windows'un menü animasyonu da açık olmalı; kayma hiç kullanılmaz).</summary>
    public static bool MenuFade(bool enabled, bool windowsMenuAnimation) => enabled && windowsMenuAnimation;

    /// <summary>İstenen süre, sınır içinde (0..<see cref="MaxMs"/>).</summary>
    public static int Duration(int milliseconds) => Math.Clamp(milliseconds, 0, MaxMs);

    /// <summary>Bu kadar widget penceresi birlikte gizlenip gösterilirken solsun mu?</summary>
    public static bool FadeWindows(bool enabled, int count) => enabled && count is > 0 and <= MaxFadedWindows;
}
