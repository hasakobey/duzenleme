using System.Text;
using Duzenleme.Core;

namespace Duzenleme.Tests;

public sealed class DurableFileTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "duzenleme-durable-" + Guid.NewGuid().ToString("N"))).FullName;

    private string PathOf(string name) => Path.Combine(_dir, name);

    private static byte[] Text(string s) => Encoding.UTF8.GetBytes(s);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void WriteAtomic_replaces_and_keeps_previous_version_as_bak()
    {
        var path = PathOf("settings.json");
        DurableFile.WriteAtomic(path, Text("bir"));
        DurableFile.WriteAtomic(path, Text("iki"));

        Assert.Equal("iki", File.ReadAllText(path));
        Assert.Equal("bir", File.ReadAllText(path + ".bak"));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Queued_writes_coalesce_and_the_latest_wins()
    {
        var path = PathOf("settings.json");
        using var file = new DurableFile(path);
        for (var i = 0; i < 50; i++) file.Enqueue(Text($"sürüm {i}"));

        Assert.True(file.Flush(TimeSpan.FromSeconds(5)));
        Assert.Equal("sürüm 49", File.ReadAllText(path));
        Assert.InRange(file.WriteCount, 1, 50);
        Assert.False(file.HasPending);
    }

    [Fact]
    public void WriteNow_supersedes_older_queued_content()
    {
        var path = PathOf("settings.json");
        using var file = new DurableFile(path);
        file.Enqueue(Text("eski"));

        Assert.True(file.WriteNow(Text("yeni"), TimeSpan.FromSeconds(2)));
        Assert.True(file.Flush(TimeSpan.FromSeconds(2)));
        Thread.Sleep(100); // arka plandaki yazıcı eskiyle üstüne yazmamalı
        Assert.Equal("yeni", File.ReadAllText(path));
    }

    [Fact]
    public void Sharing_violation_is_retried_until_the_file_is_released()
    {
        var path = PathOf("settings.json");
        File.WriteAllText(path, "ilk");
        using var file = new DurableFile(path);
        var failed = false;
        file.Failed += _ => failed = true;

        // Virüs tarayıcısı gibi: dosyayı silme/yer değiştirme paylaşımı olmadan tutuyor.
        var holder = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        file.Enqueue(Text("ikinci"));
        Thread.Sleep(250);
        Assert.Equal("ilk", File.ReadAllText(path));
        holder.Dispose();

        Assert.True(file.Flush(TimeSpan.FromSeconds(5)));
        Assert.Equal("ikinci", File.ReadAllText(path));
        Assert.False(failed);
    }

    [Fact]
    public void Long_lock_reports_failure_once_keeps_content_and_flush_retries()
    {
        var path = PathOf("settings.json");
        File.WriteAllText(path, "ilk");
        using var file = new DurableFile(path);
        var failures = 0;
        file.Failed += _ => Interlocked.Increment(ref failures);

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            file.Enqueue(Text("kaybolmasın"));
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (Volatile.Read(ref failures) == 0 && DateTime.UtcNow < deadline) Thread.Sleep(50);
            Assert.Equal(1, Volatile.Read(ref failures));
            Assert.True(file.HasPending);
        }

        // Kilit kalkınca çıkıştaki gibi hemen yazılır (sonraki denemeyi beklemeden).
        Assert.True(file.Flush(TimeSpan.FromSeconds(3)));
        Assert.Equal("kaybolmasın", File.ReadAllText(path));
    }

    [Fact]
    public void Unwritable_location_never_throws_to_the_caller()
    {
        var blocker = PathOf("dosya");
        File.WriteAllText(blocker, "x");
        // Klasör yerine dosya var: yazma hep başarısız olur ama çağırana hata fırlatılmaz.
        using var file = new DurableFile(Path.Combine(blocker, "settings.json"));
        file.Enqueue(Text("x"));

        Assert.False(file.WriteNow(Text("y"), TimeSpan.FromMilliseconds(300)));
        Assert.False(file.Flush(TimeSpan.FromMilliseconds(300)));
    }

    [Fact]
    public void Load_falls_back_to_bak_when_main_file_is_corrupt()
    {
        var path = PathOf("settings.json");
        DurableFile.WriteAtomic(path, JsonFile.Serialize(new AppSettings { Paused = true, FirstRunDone = true }));
        DurableFile.WriteAtomic(path, Text("{ yarım"));

        var loaded = JsonFile.Load(path, () => new AppSettings());

        Assert.True(loaded.Paused);
        Assert.True(loaded.FirstRunDone);
        Assert.Single(Directory.GetFiles(_dir, "settings.json.bozuk-*"));
    }

    [Fact]
    public void Load_treats_an_empty_main_file_as_corrupt()
    {
        var path = PathOf("settings.json");
        File.WriteAllText(path + ".bak", """{ "Paused": true }""");
        File.WriteAllText(path, "");

        Assert.True(JsonFile.Load(path, () => new AppSettings()).Paused);
    }

    [Fact]
    public void Load_recovers_an_interrupted_replace_from_tmp()
    {
        var path = PathOf("settings.json");
        // Yer değiştirme yarıda kaldı: eski hâl .bak'ta, yenisi .tmp'de bütün, asıl dosya yok.
        File.WriteAllText(path + ".bak", """{ "Paused": false }""");
        File.WriteAllText(path + ".tmp", """{ "Paused": true }""");

        Assert.True(JsonFile.Load(path, () => new AppSettings()).Paused);
    }

    [Fact]
    public void Load_starts_fresh_when_only_a_bak_is_left()
    {
        var path = PathOf("settings.json");
        // Kullanıcı ayarları bilerek silmiş olabilir: yedekten geri getirilmez.
        File.WriteAllText(path + ".bak", """{ "Paused": true }""");

        Assert.False(JsonFile.Load(path, () => new AppSettings()).Paused);
    }
}
