using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Duzenleme.Widgets;

/// <summary>
/// Simge seçicideki Fluent simgeleri (widget başlığı, kısayol kutusu öğesi, "Yeni bölme…"). Hepsi WPF-UI 4.3'te var ve
/// dolu biçimleriyle BMP aralığında (U+FFFF üstünü SymbolIcon çizemez); <c>IconCatalogTests</c> denetler. Emoji yok: WPF
/// emojiyi tek renkli çizer, Fluent simgelerinin yanında bozuk görünür. Adlar ekran okuyucu ve ipucu içindir.
/// </summary>
internal static class IconCatalog
{
    /// <summary>Simge ve arayüz dilinde adı (gösterirken <c>L.Dyn</c>).</summary>
    public static IReadOnlyList<(SymbolRegular Symbol, string Label)> All =>
    [
        (SymbolRegular.Folder24, L.N("Klasör")),
        (SymbolRegular.FolderOpen24, L.N("Açık klasör")),
        (SymbolRegular.Document24, L.N("Belge")),
        (SymbolRegular.DocumentPdf24, L.N("PDF")),
        (SymbolRegular.DocumentMultiple24, L.N("Dosyalar")),
        (SymbolRegular.Image24, L.N("Resim")),
        (SymbolRegular.ImageMultiple24, L.N("Resimler")),
        (SymbolRegular.MusicNote224, L.N("Müzik")),
        (SymbolRegular.Video24, L.N("Video")),
        (SymbolRegular.Archive24, L.N("Arşiv")),
        (SymbolRegular.Box24, L.N("Kutu")),
        (SymbolRegular.ArrowDownload24, L.N("İndirilenler")),
        (SymbolRegular.Desktop24, L.N("Masaüstü")),
        (SymbolRegular.Cloud24, L.N("Bulut")),
        (SymbolRegular.Apps24, L.N("Uygulamalar")),
        (SymbolRegular.AppsAddIn24, L.N("Uygulama rafı")),
        (SymbolRegular.Briefcase24, L.N("İş")),
        (SymbolRegular.Code24, L.N("Kod")),
        (SymbolRegular.WindowConsole20, L.N("Komut satırı")),
        (SymbolRegular.Database24, L.N("Veritabanı")),
        (SymbolRegular.ChartMultiple24, L.N("Grafik")),
        (SymbolRegular.Calculator24, L.N("Hesap makinesi")),
        (SymbolRegular.Mail24, L.N("Posta")),
        (SymbolRegular.Chat24, L.N("Sohbet")),
        (SymbolRegular.CalendarLtr24, L.N("Takvim")),
        (SymbolRegular.Notebook24, L.N("Defter")),
        (SymbolRegular.TaskListLtr24, L.N("Görevler")),
        (SymbolRegular.Settings24, L.N("Ayarlar")),
        (SymbolRegular.Toolbox24, L.N("Araç kutusu")),
        (SymbolRegular.Globe24, L.N("Web")),
        (SymbolRegular.Games24, L.N("Oyunlar")),
        (SymbolRegular.PuzzlePiece24, L.N("Eklenti")),
        (SymbolRegular.Home24, L.N("Ev")),
        (SymbolRegular.Building24, L.N("Ofis")),
        (SymbolRegular.Star24, L.N("Yıldız")),
        (SymbolRegular.Heart24, L.N("Kalp")),
        (SymbolRegular.Rocket24, L.N("Roket")),
        (SymbolRegular.Trophy24, L.N("Kupa")),
        (SymbolRegular.Flag24, L.N("Bayrak")),
        (SymbolRegular.Pin24, L.N("Raptiye")),
        (SymbolRegular.Tag24, L.N("Etiket")),
        (SymbolRegular.Bookmark24, L.N("Yer işareti")),
        (SymbolRegular.Lightbulb24, L.N("Fikir")),
        (SymbolRegular.Book24, L.N("Kitap")),
        (SymbolRegular.HatGraduation24, L.N("Okul")),
        (SymbolRegular.Money24, L.N("Para")),
        (SymbolRegular.Wallet24, L.N("Cüzdan")),
        (SymbolRegular.Cart24, L.N("Alışveriş")),
        (SymbolRegular.Receipt24, L.N("Fatura")),
        (SymbolRegular.Gift24, L.N("Hediye")),
        (SymbolRegular.Camera24, L.N("Kamera")),
        (SymbolRegular.PaintBrush24, L.N("Tasarım")),
        (SymbolRegular.Headphones24, L.N("Kulaklık")),
        (SymbolRegular.People24, L.N("Kişiler")),
        (SymbolRegular.LockClosed24, L.N("Kilit")),
        (SymbolRegular.Shield24, L.N("Güvenlik")),
        (SymbolRegular.Sparkle24, L.N("Pırıltı")),
        (SymbolRegular.LeafThree24, L.N("Doğa")),
        (SymbolRegular.AnimalDog24, L.N("Evcil hayvan")),
        (SymbolRegular.DrinkCoffee24, L.N("Kahve")),
        (SymbolRegular.Food24, L.N("Yemek")),
        (SymbolRegular.Airplane24, L.N("Seyahat")),
        (SymbolRegular.VehicleCar24, L.N("Araba")),
        (SymbolRegular.Dumbbell24, L.N("Spor")),
    ];

    /// <summary>Simgenin adı (katalogda yoksa simgenin kendi adı).</summary>
    public static string LabelOf(SymbolRegular symbol) =>
        All.FirstOrDefault(i => i.Symbol == symbol) is { Label: { } label } ? L.Dyn(label) : symbol.ToString();
}
