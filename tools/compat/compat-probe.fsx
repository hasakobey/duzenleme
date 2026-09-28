// Sürüm çıkarmadan önce geri uyum denetimi: yeni sürümün yazdığı ayar dosyası → ÖNCEKİ sürümün DLL'i (ör. 2.0.0'ın
// Duzenleme.dll'i, dist\*-portable.zip ya da *-tasinabilir.zip içinden): yükler mi, .bozuk yapar mı (bütün ayarlar
// silinir!), neyi görür, kaydedince ne kaybolur; sonra eski sürümün yazdığı dosya yeniden yeni sürümde açılır (geri dönüş ve
// yeniden yükseltme). Bütün WidgetSeeds tohumlarından bir dosya üretir; test örneklerinin settings.json'ları da verilebilir.
// C# konsol yoklaması Smart App Control'e takılıyor; dotnet fsi bellekte derlediği için takılmıyor. Kullanım:
//   dotnet fsi tools\compat\compat-probe.fsx <eski Duzenleme.dll> <yeni NestDesk.dll> <çalışma klasörü> [ek settings.json ...]
// Beklenen: her dosyada ".bozuk=0" ve widget sayısı aynı; kaybolanlar yalnızca yeni sürümün alanları.
open System
open System.IO
open System.Collections
open System.Reflection
open System.Text.Json
open System.Linq.Expressions

let args = fsi.CommandLineArgs
let old = Assembly.LoadFrom(Path.GetFullPath args.[1])
let neu = Assembly.LoadFrom(Path.GetFullPath args.[2])
let work = Path.GetFullPath args.[3]
Directory.CreateDirectory work |> ignore
printfn "2.0: %s %O   2.1: %s %O" (old.GetName().Name) (old.GetName().Version) (neu.GetName().Name) (neu.GetName().Version)

let jsonOps (asm: Assembly) =
    let settingsType = asm.GetType("Duzenleme.Core.AppSettings", true)
    let jsonFile = asm.GetType("Duzenleme.Core.JsonFile", true)
    let load = jsonFile.GetMethod("Load").MakeGenericMethod([| settingsType |])
    let save = jsonFile.GetMethod("Save").MakeGenericMethod([| settingsType |])
    let fallbackType = typedefof<Func<_>>.MakeGenericType([| settingsType |])
    let fallback = Expression.Lambda(fallbackType, Expression.New(settingsType)).Compile()
    settingsType, (fun (path: string) -> load.Invoke(null, [| box path; box fallback |])), (fun (path: string) (s: obj) -> save.Invoke(null, [| box path; s |]) |> ignore)

let newType, newLoad, newSave = jsonOps neu
let oldType, oldLoad, oldSave = jsonOps old

// ---- 2.1 tohumlarından ayar dosyası (bütün yeni widget'lar + küçük eklemeler) ----
let seeds = neu.GetType("Duzenleme.Core.WidgetSeeds", true)
let keys = seeds.GetProperty("Keys").GetValue(null) :?> seq<string> |> List.ofSeq
let create = seeds.GetMethod("Create")
let portal = seeds.GetMethod("Portal")
let settings = Activator.CreateInstance newType
newType.GetProperty("Paused").SetValue(settings, true)
newType.GetProperty("FirstRunDone").SetValue(settings, true)
let widgets = newType.GetProperty("Widgets").GetValue(settings) :?> IList
for k in keys do widgets.Add(create.Invoke(null, [| box k |])) |> ignore
widgets.Add(portal.Invoke(null, [| box @"C:\Users\x\Downloads"; box "Downloads"; box "İndirilenler" |])) |> ignore
let set (w: obj) (name: string) (value: obj) = w.GetType().GetProperty(name).SetValue(w, value)
let byKey (i: int) = widgets.[i]
// Küçük eklemeler: 12 saat, bitenler alta + büyük yazı, elle sıralı bölme, çalışan zamanlayıcı.
set (byKey 0) "Clock12Hour" (box (Nullable true))
set (byKey 3) "ChecklistDoneLast" (box true)
set (byKey 3) "LabelSize" (Enum.Parse(neu.GetType("Duzenleme.Core.LabelSize", true), "Large"))
set (byKey 3) "NoteText" (box "☐ ekmek\r\n☑ süt")
let last = byKey (widgets.Count - 1)
set last "SortBy" (box "manual")
set last "ItemOrder" (box (Collections.Generic.List<string>([ @"C:\Users\x\Downloads\b.pdf"; @"C:\Users\x\Downloads\a.zip" ])))
let timerIndex = keys |> List.findIndex ((=) "Timer")
let timerState = (byKey timerIndex).GetType().GetProperty("Timer").GetValue(byKey timerIndex)
timerState.GetType().GetProperty("EndsUtc").SetValue(timerState, box (Nullable(DateTime.UtcNow.AddMinutes(5.0))))
let generated = Path.Combine(work, "p6-seeds.json")
newSave generated settings

let files = generated :: (args |> Array.skip 4 |> List.ofArray)

let props (e: JsonElement) = e.EnumerateObject() |> Seq.map (fun p -> p.Name) |> Set.ofSeq

for file in files do
    let name = Path.GetFileNameWithoutExtension file
    let dir = Path.Combine(work, "run-" + name)
    if Directory.Exists dir then Directory.Delete(dir, true)
    Directory.CreateDirectory dir |> ignore
    let path = Path.Combine(dir, "settings.json")
    File.Copy(file, path)
    use source = JsonDocument.Parse(File.ReadAllText path)
    let srcWidgets = source.RootElement.GetProperty("Widgets")
    printfn "\n=== %s: %d widget" name (srcWidgets.GetArrayLength())
    let loaded = oldLoad path
    let bozuk = Directory.GetFiles(dir, "settings.json.bozuk-*").Length
    let oldWidgets = oldType.GetProperty("Widgets").GetValue(loaded) :?> IList
    printfn "2.0 yükledi: widget=%d Paused=%O .bozuk=%d" oldWidgets.Count (oldType.GetProperty("Paused").GetValue(loaded)) bozuk
    let resaved = Path.Combine(dir, "resaved-by-20.json")
    oldSave resaved loaded
    use back = JsonDocument.Parse(File.ReadAllText resaved)
    let backWidgets = back.RootElement.GetProperty("Widgets")
    for i in 0 .. srcWidgets.GetArrayLength() - 1 do
        let s = srcWidgets.[i]
        let b = backWidgets.[i]
        let variant = match s.TryGetProperty "Variant" with | true, v when v.ValueKind = JsonValueKind.String -> v.GetString() | _ -> "-"
        let lost = Set.difference (props s) (props b) |> Set.toList
        let title = match b.TryGetProperty "Title" with | true, t when t.ValueKind = JsonValueKind.String -> t.GetString() | _ -> ""
        let items = match b.TryGetProperty "Tabs" with | true, t when t.GetArrayLength() > 0 -> t.[0].GetProperty("Items").GetRawText() | _ -> ""
        printfn "  [%2d] %-9s variant=%-9s → 2.0: Kind=%-8s Folder=%s Title=%s %s | kaybolan: %s"
            i (s.GetProperty("Kind").GetString()) variant (b.GetProperty("Kind").GetString())
            (match b.TryGetProperty "FolderName" with | true, f when f.ValueKind = JsonValueKind.String -> f.GetString() | _ -> "-")
            title items (String.Join(", ", lost))
    // Geri dönüş sonrası yeniden 2.1: 2.0'ın yazdığı dosya 2.1'de sorunsuz açılır mı?
    let again = Path.Combine(dir, "again")
    Directory.CreateDirectory again |> ignore
    File.Copy(resaved, Path.Combine(again, "settings.json"))
    let reloaded = newLoad (Path.Combine(again, "settings.json"))
    let reWidgets = newType.GetProperty("Widgets").GetValue(reloaded) :?> IList
    printfn "2.0 → 2.1 yeniden: widget=%d .bozuk=%d" reWidgets.Count (Directory.GetFiles(again, "settings.json.bozuk-*").Length)
