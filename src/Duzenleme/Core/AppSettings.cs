namespace Duzenleme.Core;

public enum WidgetKind { Clock, Date, Fence }

public enum WidgetStyle { Dark, Light, Glass }

public enum AppTheme { System, Dark, Light }

public sealed class WidgetConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public WidgetKind Kind { get; set; }
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public double Width { get; set; } = double.NaN;
    public double Height { get; set; } = double.NaN;
    public WidgetStyle Style { get; set; } = WidgetStyle.Glass;
    public bool Locked { get; set; }

    /// <summary>Saat: saniyeyi göster.</summary>
    public bool ShowSeconds { get; set; }

    /// <summary>Bölme: gösterilecek masaüstü klasörünün adı.</summary>
    public string? FolderName { get; set; }
}

public sealed class AppSettings
{
    public List<Rule> Rules { get; set; } = Rule.Defaults();

    /// <summary>Hedef klasör masaüstünde yoksa oluşturulsun mu? Varsayılan: hayır, kullanıcının yapısına uyulur.</summary>
    public bool CreateMissingFolders { get; set; }

    public bool Paused { get; set; }
    public bool ShowNotifications { get; set; } = true;
    public AppTheme Theme { get; set; } = AppTheme.System;
    public bool FirstRunDone { get; set; }
    public List<WidgetConfig> Widgets { get; set; } = [];
}
