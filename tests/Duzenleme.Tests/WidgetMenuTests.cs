using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Duzenleme.Core;
using Duzenleme.Localization;
using Duzenleme.Widgets;

namespace Duzenleme.Tests;

/// <summary>
/// Kolay NestDesk sağ tık menüsü: "Düzenle…" (kalın) · widget'ın en çok iki işi · ayraç · [Kilidi aç] · "Kaldır"; en çok beş
/// satır, alt menü yok. Her widget türünün gerçek görünüm sınıfı menü için (<see cref="WidgetMenu.ForEditor"/> false)
/// doldurulur: görünüm XAML'si kurulmadan, yalnızca ayarıyla oluşturulur (menünün işleri yalnızca ayarı ve birkaç alanı okur).
/// </summary>
public class WidgetMenuTests
{
    [Fact]
    public void Skeleton_is_edit_then_actions_then_unlock_and_remove()
    {
        EditorFakes.OnSta(() =>
        {
            var calls = new List<string>();
            var menu = new ContextMenu();
            WidgetWindow.BuildMenu(menu, [], locked: false, () => calls.Add("edit"), () => calls.Add("unlock"), () => calls.Add("remove"));
            Assert.Equal(["Düzenle…", "-", "Kaldır"], Rows(menu));
            Assert.Equal(FontWeights.SemiBold, ((MenuItem)menu.Items[0]).FontWeight);

            menu = new ContextMenu();
            object[] actions = [Menus.Item("Başlat", () => { }), Menus.Item("Sıfırla", () => { })];
            WidgetWindow.BuildMenu(menu, actions, locked: true, () => calls.Add("edit"), () => calls.Add("unlock"), () => calls.Add("remove"));
            Assert.Equal(["Düzenle…", "Başlat", "Sıfırla", "-", "Kilidi aç", "Kaldır"], Rows(menu));
            foreach (var item in menu.Items.OfType<MenuItem>()) item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
            Assert.Equal(["edit", "unlock", "remove"], calls);

            // Fazlası ve alt menü yine de girmez (ayarlar Düzenle panelinde).
            menu = new ContextMenu();
            var submenu = new MenuItem { Header = "Görünüm", Items = { Menus.Item("Cam", () => { }) } };
            WidgetWindow.BuildMenu(menu, [submenu, Menus.Item("A", () => { }), Menus.Item("B", () => { }), Menus.Item("C", () => { })],
                locked: false, () => { }, () => { }, () => { });
            Assert.Equal(["Düzenle…", "A", "B", "-", "Kaldır"], Rows(menu));
        });
    }

    public static IEnumerable<object[]> Kinds =>
        new[]
        {
            "fence-files", "fence-all", "fence-folder", "launcher", "launcher-undo", "ring", "note", "checklist", "clock", "date", "countdown",
            "calendar", "timer", "pomodoro", "stopwatch", "world", "system", "recycle",
        }.Select(kind => new object[] { kind });

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Every_widget_menu_has_at_most_five_rows_and_no_submenus(string kind)
    {
        EditorFakes.OnSta(() =>
        {
            foreach (var locked in new[] { false, true })
            {
                var (view, expected) = Create(kind);
                var own = new WidgetMenu();
                view.AddMenuItems(own);
                // Menü için panelin öğeleri kurulmaz.
                Assert.Empty(own.Settings);
                Assert.Empty(own.More);
                Assert.True(own.Primary.Count <= WidgetMenu.MaxActions, kind);
                Assert.All(own.Primary, item => Assert.Empty(Assert.IsAssignableFrom<MenuItem>(item).Items));

                var menu = new ContextMenu();
                WidgetWindow.BuildMenu(menu, own.Primary, locked, () => { }, () => { }, () => { });
                var rows = Rows(menu).Where(r => r != "-").ToList();
                Assert.True(rows.Count <= 5, $"{kind}: {string.Join(" | ", rows)}");
                Assert.Equal(["Düzenle…", .. expected, .. locked ? new[] { "Kilidi aç" } : [], "Kaldır"], rows);
                Assert.DoesNotContain(menu.Items.OfType<MenuItem>(), item => item.Items.Count > 0);
            }
        });
    }

    [Fact]
    public void Menu_rows_are_translated()
    {
        EditorFakes.OnSta(() =>
        {
            using var _ = L.Use(Lang.En);
            var menu = new ContextMenu();
            WidgetWindow.BuildMenu(menu, [], locked: true, () => { }, () => { }, () => { });
            Assert.Equal(["Edit…", "-", "Unlock", "Remove"], Rows(menu));
            var (recycle, _) = Create("recycle");
            var own = new WidgetMenu();
            recycle.AddMenuItems(own);
            Assert.Equal(["Open", "Empty…"], own.Primary.OfType<MenuItem>().Select(i => (string)i.Header));
        });
    }

    // ---------------------------------------------------------------- yardımcılar

    private static List<string> Rows(ItemsControl menu) =>
        menu.Items.Cast<object>().Select(i => i is MenuItem m ? MenuRowsLabel(m) : "-").ToList();

    private static string MenuRowsLabel(MenuItem item) => (string)item.Header;

    /// <summary>Türün görünümü (XAML'siz) ve menüde beklenen işleri.</summary>
    private static (IWidgetView View, string[] Actions) Create(string kind)
    {
        const string app = @"C:\Uygulamalar\Hesap.exe";
        switch (kind)
        {
            case "fence-files":
                return (View<FenceView>(new WidgetConfig { Kind = WidgetKind.Fence, Filter = DesktopFilter.Files }), ["Bu bölmede ara"]);
            case "fence-all":
                return (View<FenceView>(new WidgetConfig { Kind = WidgetKind.Fence, Filter = DesktopFilter.All }), ["Bu bölmede ara", "Yeni klasör"]);
            case "fence-folder":
                return (View<FenceView>(new WidgetConfig { Kind = WidgetKind.Fence, FolderName = "PDF" }, ("_folderPath", @"C:\Masaüstü\PDF")),
                    ["Klasörü aç", "Bu bölmede ara"]);
            case "launcher":
            case "launcher-undo":
            {
                var config = new WidgetConfig { Kind = WidgetKind.Launcher, Tabs = [new LauncherTab { Name = "Uygulamalar" }] };
                var edits = new LauncherEdits(config, () => { });
                if (kind == "launcher") return (View<LauncherView>(config, ("_edits", edits)), ["Uygulama ekle…"]);
                typeof(LauncherEdits).GetProperty(nameof(LauncherEdits.LastRemoved))!
                    .SetValue(edits, new LauncherEdits.Removed(config.Tabs[0], 0, LauncherEntry.Of(app), null));
                return (View<LauncherView>(config, ("_edits", edits)),
                    [$"Geri al: \"{Menus.Literal(TileItem.DisplayName(app))}\" listeye dönsün", "Uygulama ekle…"]);
            }
            case "ring":
                return (View<RingWidgetView>(new WidgetConfig { Kind = WidgetKind.Launcher, Variant = WidgetVariants.Ring }),
                    ["Uygulama ekle…", "Tuşla imlecin yanında açılsın"]);
            case "note":
                return (View<NoteView>(new WidgetConfig { Kind = WidgetKind.Note }), ["Listeye çevir"]);
            case "checklist":
                return (View<NoteView>(new WidgetConfig { Kind = WidgetKind.Note, NoteChecklist = true }), ["Düz nota çevir"]);
            case "clock":
                return (View<ClockView>(new WidgetConfig { Kind = WidgetKind.Clock }), []);
            case "date":
                return (View<DateView>(new WidgetConfig { Kind = WidgetKind.Date }), []);
            case "countdown":
                return (View<CountdownView>(new WidgetConfig { Kind = WidgetKind.Date, Variant = WidgetVariants.Countdown }), []);
            case "calendar":
                return (View<CalendarView>(new WidgetConfig { Kind = WidgetKind.Date, Variant = WidgetVariants.Month }), ["Bugüne dön"]);
            case "timer":
            case "pomodoro":
            case "stopwatch":
                var mode = kind switch { "pomodoro" => TimerModes.Pomodoro, "stopwatch" => TimerModes.Stopwatch, _ => TimerModes.Countdown };
                return (View<TimerView>(new WidgetConfig
                {
                    Kind = WidgetKind.Clock, Variant = WidgetVariants.Timer, Timer = new TimerState { Mode = mode },
                }), ["Başlat", "Sıfırla"]);
            case "world":
                return (View<WorldClockView>(new WidgetConfig { Kind = WidgetKind.Clock, Variant = WidgetVariants.World }), ["Şehir ekle…"]);
            case "system":
                return (View<SystemStatusView>(new WidgetConfig { Kind = WidgetKind.Clock, Variant = WidgetVariants.System }),
                    ["Görev Yöneticisi'ni aç"]);
            case "recycle":
                return (View<RecycleBinView>(new WidgetConfig { Kind = WidgetKind.Launcher, Variant = WidgetVariants.Recycle }), ["Aç", "Boşalt…"]);
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    /// <summary>
    /// Görünüm, kurucusu çalışmadan (XAML ve pencere yok): ayarı ve verilen alanları yazılır; alan başlatıcıları çalışmadığından
    /// boş listeler kurulur (notun maddeleri).
    /// </summary>
    private static T View<T>(WidgetConfig config, params (string Field, object? Value)[] fields) where T : IWidgetView
    {
        var view = RuntimeHelpers.GetUninitializedObject(typeof(T));
        foreach (var field in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            if (field.GetValue(view) is null && field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                field.SetValue(view, Activator.CreateInstance(field.FieldType));
        Field(typeof(T), "_config").SetValue(view, config);
        foreach (var (name, value) in fields) Field(typeof(T), name).SetValue(view, value);
        return (T)view;
    }

    private static FieldInfo Field(Type type, string name) =>
        type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingFieldException(type.Name, name);
}
