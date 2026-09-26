using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duzenleme.Core;

/// <summary>JSON dosyalarını atomik olarak (önce geçici dosyaya) yazar; bozuk dosyada varsayılana döner.</summary>
public static class JsonFile
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        // Elle düzenlenmiş dosyalarda "12" gibi metin sayılar da kabul edilsin.
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals | JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>
    /// Dosyayı okur. Yoksa varsayılanı döner; içerik bozuksa yedekleyip varsayılanı döner.
    /// Dosya geçici olarak okunamıyorsa (başka süreç kilitlemiş, ağ gecikmesi) birkaç kez dener ve sonunda hata fırlatır:
    /// varsayılana dönüp sonra kaydetmek kullanıcının gerçek ayarlarını ezerdi.
    /// </summary>
    public static T Load<T>(string path, Func<T> fallback)
    {
        if (!File.Exists(path)) return fallback();

        string text;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                text = File.ReadAllText(path);
                break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < 6)
            {
                Thread.Sleep(150 * attempt);
            }
        }

        try
        {
            return JsonSerializer.Deserialize<T>(text, Options) ?? fallback();
        }
        catch (JsonException)
        {
            // Okunamayan dosya varsayılanla ezilmeden önce yedeklensin; kullanıcının ayarları kaybolmasın.
            try { File.Copy(path, $"{path}.bozuk-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: true); }
            catch (IOException) { }
            return fallback();
        }
    }

    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
        File.Move(tmp, path, overwrite: true);
    }
}
