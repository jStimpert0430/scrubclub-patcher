using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scrubclub.Patching;
using Xunit;

public sealed class PatcherTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "patcher-test-" + Guid.NewGuid().ToString("N"));
    public PatcherTests() { Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, "valheim.exe"), "test marker"); }
    public void Dispose() => Directory.Delete(root, true);
    static (Release Release, byte[] Payload) Package(string version = "1.0.0", params (string Path, string Text, bool Preserve)[] files)
    {
        if (files.Length == 0) files = [("BepInEx/plugins/test.dll", "v1", false)];
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
            foreach (var f in files) { using var stream = zip.CreateEntry(f.Path).Open(); stream.Write(Encoding.UTF8.GetBytes(f.Text)); }
        var payload = memory.ToArray();
        return (new Release(1, version, "windows", "https://example.com/test.zip", Releases.Hash(payload), payload.Length,
            files.Select(f => new ReleaseFile(f.Path, Releases.Hash(Encoding.UTF8.GetBytes(f.Text)), Encoding.UTF8.GetByteCount(f.Text), f.Preserve)).ToArray()), payload);
    }
    string Target(string path = "BepInEx/plugins/test.dll") => Path.Combine(root, path);
    [Fact] public void InstallIsIdempotentAndBacksUpReplacements()
    {
        var first = Package(); Installer.Install(root, first.Release, first.Payload);
        Assert.Equal("v1", File.ReadAllText(Target()));
        Assert.Equal(0, Installer.Install(root, first.Release, first.Payload).Changed);
        var next = Package("1.1.0", ("BepInEx/plugins/test.dll", "v2", false));
        var result = Installer.Install(root, next.Release, next.Payload);
        Assert.Equal("v2", File.ReadAllText(Target()));
        Assert.Equal("v1", File.ReadAllText(Path.Combine(result.BackupDirectory, "0.bak")));
    }
    [Fact] public void ConfigAndUnmanagedFilesArePreserved()
    {
        Directory.CreateDirectory(Target("BepInEx/config"));
        File.WriteAllText(Target("BepInEx/config/test.cfg"), "custom");
        File.WriteAllText(Target("BepInEx/unmanaged.txt"), "untouched");
        var p = Package("1.0.0", ("BepInEx/config/test.cfg", "default", true));
        Assert.Equal(1, Installer.Install(root, p.Release, p.Payload).Preserved);
        Assert.Equal("custom", File.ReadAllText(Target("BepInEx/config/test.cfg")));
        Assert.Equal("untouched", File.ReadAllText(Target("BepInEx/unmanaged.txt")));
    }
    [Fact] public void FailedUpdateRollsBackFilesAndState()
    {
        var p = Package(); Installer.Install(root, p.Release, p.Payload);
        var next = Package("1.1.0", ("BepInEx/plugins/test.dll", "v2", false), ("BepInEx/plugins/new.dll", "new", false));
        Assert.Throws<IOException>(() => Installer.Install(root, next.Release, next.Payload, _ => throw new IOException("Simulated disk failure")));
        Assert.Equal("v1", File.ReadAllText(Target())); Assert.False(File.Exists(Target("BepInEx/plugins/new.dll")));
        Assert.Equal(0, Installer.Install(root, p.Release, p.Payload).Changed);
    }
    [Fact] public void RetiredManagedFilesRemovedButEditedOnesBlockUpdate()
    {
        var p = Package(); Installer.Install(root, p.Release, p.Payload);
        var next = Package("1.1.0", ("BepInEx/plugins/new.dll", "new", false));
        File.WriteAllText(Target(), "user edit");
        Assert.Throws<IOException>(() => Installer.Install(root, next.Release, next.Payload));
        Assert.False(File.Exists(Target("BepInEx/plugins/new.dll")));
        File.WriteAllText(Target(), "v1"); Installer.Install(root, next.Release, next.Payload);
        Assert.False(File.Exists(Target()));
    }
    [Theory]
    [InlineData("../valheim.exe")][InlineData("BepInEx/../../escape")][InlineData("BepInEx/x:ads")]
    [InlineData("BepInEx/CON.dll")][InlineData("BepInEx/dir /x")][InlineData("BepInEx/a\\b")]
    [InlineData("valheim_Data/game.dll")][InlineData("/BepInEx/a")]
    public void UnsafePathsRejected(string path) => Assert.Throws<InvalidDataException>(() => Releases.SafePath(path));
    [Fact] public void CorruptArchiveDoesNotChangeInstall()
    {
        var p = Package(); p.Payload[0] ^= 1;
        Assert.Throws<InvalidDataException>(() => Installer.Install(root, p.Release, p.Payload));
        Assert.False(Directory.Exists(Target("BepInEx")));
    }
    [Fact] public void SignedFileHashMustMatchArchiveContents()
    {
        var p = Package(); var r = p.Release with { Files = [p.Release.Files[0] with { Sha256 = new string('0', 64) }] };
        Assert.Throws<InvalidDataException>(() => Installer.Install(root, r, p.Payload));
        Assert.False(File.Exists(Target()));
    }
    [Fact] public void UnexpectedZipEntryRejected()
    {
        var p = Package("1.0.0", ("BepInEx/plugins/evil.dll", "v1", false));
        var r = p.Release with { Files = [p.Release.Files[0] with { Path = "BepInEx/plugins/test.dll" }] };
        Assert.Throws<InvalidDataException>(() => Installer.Install(root, r, p.Payload));
    }
    [Fact] public void CaseCollisionsRejected()
    {
        var p = Package("1.0.0", ("BepInEx/plugins/Test.dll", "x", false), ("BepInEx/plugins/test.dll", "x", false));
        Assert.Throws<InvalidDataException>(() => Installer.Install(root, p.Release, p.Payload));
    }
    [Fact] public void SymlinkCannotRedirectInstall()
    {
        if (OperatingSystem.IsWindows()) return;
        var outside = Path.Combine(root, "outside"); Directory.CreateDirectory(outside);
        Directory.CreateSymbolicLink(Target("BepInEx"), outside);
        var p = Package(); Assert.Throws<IOException>(() => Installer.Install(root, p.Release, p.Payload));
        Assert.Empty(Directory.GetFileSystemEntries(outside));
    }
    [Fact] public void BadSignatureAndWrongPlatformRejected()
    {
        using var key = RSA.Create(2048); var p = Package(); var bytes = JsonSerializer.SerializeToUtf8Bytes(p.Release);
        var signed = new SignedRelease(Convert.ToBase64String(bytes), Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)));
        var envelope = JsonSerializer.SerializeToUtf8Bytes(signed);
        Assert.Equal("1.0.0", Releases.Verify(envelope, key.ExportSubjectPublicKeyInfoPem(), "windows").Version);
        Assert.Throws<InvalidDataException>(() => Releases.Verify(envelope, key.ExportSubjectPublicKeyInfoPem(), "linux"));
        using var wrongKey = RSA.Create(2048);
        Assert.Throws<InvalidDataException>(() => Releases.Verify(envelope, wrongKey.ExportSubjectPublicKeyInfoPem(), "windows"));
    }
    [Fact] public void DowngradeRejected()
    {
        var p = Package("2.0.0"); Installer.Install(root, p.Release, p.Payload); var old = Package();
        Assert.Throws<IOException>(() => Installer.Install(root, old.Release, old.Payload));
    }
    [Fact] public void InterruptedUpdateJournalIsRecoveredOnNextRun()
    {
        var p = Package(); Installer.Install(root, p.Release, p.Payload);
        var meta = Path.Combine(root, ".scrubclub-patcher"); var pending = Path.Combine(meta, "pending"); Directory.CreateDirectory(pending);
        File.Copy(Target(), Path.Combine(pending, "0.bak"));
        File.Copy(Path.Combine(meta, "installed.json"), Path.Combine(pending, "state.bak"));
        File.WriteAllBytes(Path.Combine(pending, "journal.json"), JsonSerializer.SerializeToUtf8Bytes(new Journal([new("BepInEx/plugins/test.dll", true), new("BepInEx/plugins/new.dll", false)], true)));
        File.WriteAllText(Target(), "interrupted update"); File.WriteAllText(Target("BepInEx/plugins/new.dll"), "new");
        Assert.Equal(0, Installer.Install(root, p.Release, p.Payload).Changed);
        Assert.Equal("v1", File.ReadAllText(Target())); Assert.False(File.Exists(Target("BepInEx/plugins/new.dll")));
        Assert.False(Directory.Exists(pending));
    }
    [Fact] public void ConcurrentInstallationIsRefused()
    {
        var p = Package(); Installer.Install(root, p.Release, p.Payload);
        using var held = new FileStream(Path.Combine(root, ".scrubclub-patcher", "lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Throws<IOException>(() => Installer.Install(root, p.Release, p.Payload));
    }
    [Fact] public void NativeLinuxLauncherGetsExecutablePermission()
    {
        if (OperatingSystem.IsWindows()) return;
        File.WriteAllText(Path.Combine(root, "valheim.x86_64"), "test marker");
        var p = Package("1.0.0", ("start_game_bepinex.sh", "#!/bin/sh\n", false));
        Installer.Install(root, p.Release with { Platform = "linux" }, p.Payload);
        Assert.True((File.GetUnixFileMode(Path.Combine(root, "start_game_bepinex.sh")) & UnixFileMode.UserExecute) != 0);
    }
    [Theory][InlineData("http://example.com/mod.zip")][InlineData("https://user:secret@example.com/mod.zip")][InlineData("file:///tmp/mod.zip")]
    public void InsecureUrlsRejected(string url) => Assert.Throws<InvalidDataException>(() => Releases.Https(url));
}
