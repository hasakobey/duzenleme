using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duzenleme.Core;

/// <summary>
/// Kalıcı bir enum'da tanınmayan (daha yeni sürümün yazdığı) ad okunursa kullanılacak değer. Yoksa enum'un 0 değeri.
/// Örn. <see cref="DesktopFilter"/>: bilinmeyen süzgeç "Tümü" olur, hiçbir masaüstü öğesi görünmez kalmaz.
/// </summary>
[AttributeUsage(AttributeTargets.Enum)]
public sealed class EnumFallbackAttribute(object value) : Attribute
{
    public object Value { get; } = value;
}

/// <summary>
/// <see cref="JsonStringEnumConverter"/> gibi davranır (adla yazar; adı büyük/küçük harf duyarsız ya da sayı olarak okur)
/// ama tanınmayan adı hata saymaz: yedek değeri (<see cref="EnumFallbackAttribute"/>, yoksa 0) verir ve
/// <see cref="JsonFile.Log"/>'a yazar. 2.0'da tek bir yeni enum üyesi (ör. <c>"Kind": "Timer"</c>) JsonException'a ve
/// bütün ayarların <c>.bozuk-*</c> olmasına yol açıyordu; 2.1'den sonraki sürümler 2.1 kullanıcısının ayarlarını böyle silemez.
/// </summary>
public sealed class TolerantEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(TolerantEnumConverter<>).MakeGenericType(typeToConvert))!;
}

internal sealed class TolerantEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    private static readonly bool IsFlags = typeof(T).IsDefined(typeof(FlagsAttribute), inherit: false);
    private static readonly T Fallback = FallbackValue();
    private static readonly ulong KnownBits = Enum.GetValues<T>().Aggregate(0UL, (bits, v) => bits | ToBits(v));

    private static T FallbackValue()
    {
        var attribute = (EnumFallbackAttribute?)Attribute.GetCustomAttribute(typeof(T), typeof(EnumFallbackAttribute));
        return attribute?.Value is T value ? value : default;
    }

    private static ulong ToBits(T value) => unchecked((ulong)Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return Parse(reader.GetString() ?? "");
            case JsonTokenType.Number:
                // JsonStringEnumConverter da sayı kabul eder; tanımsız sayı (elle yazılmış "Kind": 7) yedek değer olur.
                return reader.TryGetInt64(out var number) ? FromNumber(number, number.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    : Unknown(reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture));
            default:
                // null ya da nesne: tür gerçekten yanlış (bozuk dosya); eskisi gibi hata (JsonFile yedeğe döner).
                throw new JsonException($"{typeof(T).Name} beklenirken {reader.TokenType} okundu.");
        }
    }

    public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Parse(reader.GetString() ?? "");

    private static T Parse(string text)
    {
        var trimmed = text.Trim();
        if (long.TryParse(trimmed, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var number))
            return FromNumber(number, text);
        // Enum.TryParse virgüllü listeyi de okur; bayrak (Flags) olmayan enum'da bu da tanınmayan addır.
        if ((IsFlags || !trimmed.Contains(',')) && Enum.TryParse<T>(trimmed, ignoreCase: true, out var value) && IsKnown(value))
            return value;
        return Unknown(text);
    }

    private static T FromNumber(long number, string text)
    {
        var value = (T)Enum.ToObject(typeof(T), number);
        return IsKnown(value) ? value : Unknown(text);
    }

    private static bool IsKnown(T value) => IsFlags ? (ToBits(value) & ~KnownBits) == 0 : Enum.IsDefined(value);

    private static T Unknown(string text)
    {
        JsonFile.Log?.Invoke($"ayarlar: {typeof(T).Name} için tanınmayan değer \"{text}\" → {Fallback}");
        return Fallback;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        // Tanımlı değer ad olarak (bayraklarda "A, B"), tanımsız değer sayı olarak: JsonStringEnumConverter'ın yazdığıyla aynı.
        if (IsKnown(value)) writer.WriteStringValue(value.ToString());
        else writer.WriteNumberValue(Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.ToString());
}
