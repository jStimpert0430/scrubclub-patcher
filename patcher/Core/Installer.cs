using System.IO.Compression;
using System.Text.Json;

namespace Scrubclub.Patching;

public sealed record Installed(string Version, ReleaseFile[] Files);
public sealed record BackupEntry(string Path, bool Existed);
public sealed record Journal(BackupEntry[] Entries, bool HadState);
public sealed record InstallResult(int Changed, int Preserved, string BackupDirectory);

public static class Installer
{
    const string Meta = ".scrubclub-patcher";
    static string Internal(string root, string path) => Releases.Destination(root, Meta + "/" + path, true);
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static InstallResult Install(string root, Release release, byte[] payload, Action<int>? afterWrite = null)
    {
        Releases.Validate(release, release.Platform);
        root = Path.GetFullPath(root);
        var marker = release.Platform == "windows" ? "valheim.exe" : "valheim.x86_64";
        if (!File.Exists(Path.Combine(root, marker))) throw new IOException("Select the Valheim game folder containing " + marker + ".");
        if (payload.LongLength != release.PayloadSize || Releases.Hash(payload) != release.PayloadSha256)
            throw new InvalidDataException("Payload checksum mismatch. Nothing was installed.");
        // Validate the complete archive before touching the game directory. Never extract ZIP paths directly.
        var contents = ReadPayload(release, payload);
        Directory.CreateDirectory(Internal(root, ""));
        using var installLock = new FileStream(Internal(root, "lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Recover(root);
        var statePath = Internal(root, "installed.json");
        var previous = File.Exists(statePath) ? JsonSerializer.Deserialize<Installed>(File.ReadAllBytes(statePath)) : null;
        if (previous != null)
        {
            if (Version.Parse(release.Version) < Version.Parse(previous.Version))
                throw new IOException("Refusing an older release than the installed version.");
            foreach (var f in previous.Files) Releases.SafePath(f.Path);
        }
        var changed = new List<(string Path, byte[]? Data)>();
        var owned = new List<ReleaseFile>();
        int preserved = 0;
        foreach (var file in release.Files)
        {
            var target = Releases.Destination(root, file.Path);
            if (Directory.Exists(target)) throw new IOException("A directory occupies a release file path: " + file.Path);
            if (file.PreserveExisting && File.Exists(target)) { preserved++; continue; }
            if (!file.PreserveExisting) owned.Add(file);
            if (!File.Exists(target) || Releases.HashFile(target) != file.Sha256)
                changed.Add((file.Path, contents[file.Path]));
        }
        var currentPaths = release.Files.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var old in previous?.Files ?? [])
        {
            if (currentPaths.Contains(old.Path)) continue;
            var target = Releases.Destination(root, old.Path);
            if (File.Exists(target))
            {
                if (Releases.HashFile(target) == old.Sha256) changed.Add((old.Path, null));
                else throw new IOException("A retired managed file was edited; move it out of the game folder before updating: " + old.Path);
            }
        }
        if (changed.Count == 0 && previous?.Version == release.Version) return new(0, preserved, "");
        var pending = Internal(root, "pending");
        Directory.CreateDirectory(pending);
        var entries = changed.Select(c => new BackupEntry(c.Path, File.Exists(Releases.Destination(root, c.Path)))).ToArray();
        for (int i = 0; i < entries.Length; i++)
            if (entries[i].Existed) File.Copy(Releases.Destination(root, entries[i].Path), Internal(root, $"pending/{i}.bak"), true);
        bool hadState = File.Exists(statePath);
        if (hadState) File.Copy(statePath, Internal(root, "pending/state.bak"), true);
        // Once this durable journal exists, the next run can restore an interrupted update.
        AtomicWrite(Internal(root, "pending/journal.json"), JsonSerializer.SerializeToUtf8Bytes(new Journal(entries, hadState), Json));
        try
        {
            for (int i = 0; i < changed.Count; i++)
            {
                var target = Releases.Destination(root, changed[i].Path);
                if (changed[i].Data is { } bytes)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    AtomicWrite(target, bytes);
                    if (!OperatingSystem.IsWindows() && changed[i].Path == "start_game_bepinex.sh")
                        File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
                else File.Delete(target);
                afterWrite?.Invoke(i);
            }
            AtomicWrite(statePath, JsonSerializer.SerializeToUtf8Bytes(new Installed(release.Version, owned.ToArray()), Json));
            // Renaming the journal directory is the commit point. Backups remain for manual restoration.
            var archive = Internal(root, "backup-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + "-" + Guid.NewGuid().ToString("N"));
            Directory.Move(pending, archive);
            return new(changed.Count, preserved, archive);
        }
        catch { Recover(root); throw; }
    }

    static Dictionary<string, byte[]> ReadPayload(Release release, byte[] payload)
    {
        using var stream = new MemoryStream(payload, false);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        if (zip.Entries.Count != release.Files.Length) throw new InvalidDataException("ZIP does not match release file list.");
        var expected = release.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in zip.Entries)
        {
            Releases.SafePath(entry.FullName);
            if (!expected.TryGetValue(entry.FullName, out var file) || files.ContainsKey(entry.FullName) || entry.Length != file.Size)
                throw new InvalidDataException("Unexpected or duplicate ZIP entry.");
            using var input = entry.Open();
            var bytes = new byte[checked((int)file.Size)];
            input.ReadExactly(bytes);
            if (input.ReadByte() != -1 || Releases.Hash(bytes) != file.Sha256) throw new InvalidDataException("File checksum mismatch: " + file.Path);
            files.Add(entry.FullName, bytes);
        }
        return files;
    }

    static void Recover(string root)
    {
        var journalPath = Internal(root, "pending/journal.json");
        if (!File.Exists(journalPath)) return;
        var journal = JsonSerializer.Deserialize<Journal>(File.ReadAllBytes(journalPath)) ?? throw new IOException("Invalid recovery journal.");
        // Preflight every backup and target before recovering any file.
        for (int i = 0; i < journal.Entries.Length; i++)
        {
            Releases.Destination(root, journal.Entries[i].Path);
            if (journal.Entries[i].Existed && !File.Exists(Internal(root, $"pending/{i}.bak"))) throw new IOException("Missing recovery backup.");
        }
        if (journal.HadState && !File.Exists(Internal(root, "pending/state.bak"))) throw new IOException("Missing state backup.");
        for (int i = 0; i < journal.Entries.Length; i++)
        {
            var e = journal.Entries[i]; var target = Releases.Destination(root, e.Path);
            if (e.Existed) AtomicWrite(target, File.ReadAllBytes(Internal(root, $"pending/{i}.bak")));
            else File.Delete(target);
        }
        var state = Internal(root, "installed.json");
        if (journal.HadState) AtomicWrite(state, File.ReadAllBytes(Internal(root, "pending/state.bak")));
        else File.Delete(state);
        Directory.Move(Internal(root, "pending"), Internal(root, "recovered-" + Guid.NewGuid().ToString("N")));
    }

    static void AtomicWrite(string path, byte[] bytes)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(bytes); file.Flush(true); }
            if (!OperatingSystem.IsWindows() && File.Exists(path)) File.SetUnixFileMode(temp, File.GetUnixFileMode(path));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
