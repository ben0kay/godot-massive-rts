// Reads bounded, validated JSON and publishes flushed saves with a last-known-good backup.
using System;
using System.IO;
using System.Text.Json;

namespace MassiveRts;

internal static class AtomicJsonFile
{
    private const int MaxBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions Format = new() { WriteIndented = true };

    // =========================================================
    // Recover from the backup only after independently validating its contents.
    public static T Load<T>(string path, Func<T, bool> valid, Func<T> create, out bool recovered) where T : class
    {
        recovered = false;
        if (TryRead(path, valid, out T value, out bool primaryExists)) return value;
        if (TryRead(path + ".bak", valid, out value, out bool backupExists))
        {
            recovered = true;
            return value;
        }

        if (!primaryExists && !backupExists) return create();
        throw new IOException($"Unable to read {Path.GetFileName(path)} or its backup. Existing files were preserved.");
    }

    // =========================================================
    // Keep the existing backup when the primary fails validation.
    public static void Save<T>(string path, T value, Func<T, bool> valid) where T : class
    {
        if (!valid(value)) throw new InvalidDataException("The save data is invalid or exceeds its entry limit.");
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, Format);
        if (bytes.Length > MaxBytes) throw new IOException("The save exceeds its size limit.");

        // Recheck disk state before writing so an unreadable primary and backup stay intact.
        bool validPrimary = TryRead(path, valid, out _, out bool primaryExists);
        if (!validPrimary)
        {
            bool validBackup = TryRead(path + ".bak", valid, out _, out bool backupExists);
            if (!validBackup && (primaryExists || backupExists))
                throw new IOException($"Unable to replace {Path.GetFileName(path)}. Existing files were preserved.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            if (validPrimary) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path, overwrite: primaryExists);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    // =========================================================
    // Distinguish missing files from damaged or inaccessible files.
    private static bool TryRead<T>(string path, Func<T, bool> valid, out T value, out bool exists) where T : class
    {
        value = null;
        exists = true;
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length is <= 0 or > MaxBytes) return false;
            byte[] bytes = new byte[(int)stream.Length];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1) return false;
            value = JsonSerializer.Deserialize<T>(bytes, Format);
            return value != null && valid(value);
        }
        catch (FileNotFoundException) { exists = false; }
        catch (DirectoryNotFoundException) { exists = false; }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (JsonException) { }
        return false;
    }
}
