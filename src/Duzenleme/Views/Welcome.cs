using System.Windows;

namespace Duzenleme.Views;

/// <summary>Karşılamayı açar. GEÇİCİ: Paket C WelcomeWindow'u bağlayana dek ilk açılışı tamamlanmış sayar.</summary>
internal static class Welcome
{
    public static void Show(bool rerun)
    {
        if (rerun) return;
        AppHost.Settings.FirstRunDone = true;
        AppHost.SaveSettings();
        (Application.Current as App)?.ShowMainWindow();
    }
}
