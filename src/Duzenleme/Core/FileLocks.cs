using System.IO;

namespace Duzenleme.Core;

/// <summary>Dosyayı başka bir programın o an kullanması (geçici; yeniden denenir, hata günlüğüne hata olarak yazılmaz).</summary>
public static class FileLocks
{
    /// <summary>ERROR_SHARING_VIOLATION (32) ya da ERROR_LOCK_VIOLATION (33) olan G/Ç hatası mı?</summary>
    public static bool IsSharingViolation(Exception ex) => ex is IOException && (ex.HResult & 0xFFFF) is 32 or 33;
}
