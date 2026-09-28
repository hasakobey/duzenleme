using System.Windows;
using Duzenleme.Core;
using Duzenleme.Desktop;

namespace Duzenleme.Widgets;

/// <summary>
/// "Geri Dönüşüm Kutusu'nu boşalt…" (Geri Dönüşüm Kutusu widget'ı, bölmedeki ve kutudaki kutu öğesi): önce sayı ve boyut arka
/// planda okunur, sonra açık onay istenir (Enter/Esc "Vazgeç"). Uygulamadaki tek geri alınamaz iş: hiçbir yoldan tek
/// tıkla yapılmaz.
/// </summary>
internal static class RecycleBinActions
{
    private static bool _busy;

    public static async void EmptyWithConfirm()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var info = await RecycleBin.QueryAsync();
            if (info is { IsEmpty: true })
            {
                AppHost.Tray?.Notify(L.T("Geri Dönüşüm Kutusu zaten boş"), L.T("Silinecek bir şey yok."));
                return;
            }
            var message = info is { } bin
                ? L.F("{0} ({1}) kalıcı olarak silinecek. Bu geri alınamaz.", L.P(bin.Items, "{0} öğe"), MeasureText.Bytes(bin.Bytes))
                : L.T("Geri Dönüşüm Kutusu'ndaki her şey kalıcı olarak silinecek. Bu geri alınamaz.");
            if (!Views.Confirm.Ask(null, L.T("Geri Dönüşüm Kutusu boşaltılsın mı?"), message, L.T("Boşalt"))) return;
            await RecycleBin.EmptyAsync();
        }
        catch (Exception ex)
        {
            DebugLog.Write($"Geri Dönüşüm Kutusu boşaltılamadı: {ex}");
            MessageBox.Show(L.F("Geri Dönüşüm Kutusu boşaltılamadı: {0}", ex.Message), AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _busy = false;
        }
    }
}
