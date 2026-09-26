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

    public static T Load<T>(string path, Func<T> fallback)
    {
        try
        {
            if (!File.Exists(path)) return fallback();
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? fallback();
        }
        catch (JsonException)
        {
            // Okunamayan dosya varsayılanla ezilmeden önce yedeklensin; kullanıcının ayarları kaybolmasın.
            try { File.Copy(path, $"{path}.bozuk-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: true); }
            catch (IOException) { }
            return fallback();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
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
