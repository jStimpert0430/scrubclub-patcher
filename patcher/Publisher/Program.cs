using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Scrubclub.Patching;

if (args.Length != 6)
{
    Console.Error.WriteLine("Usage: Publisher STAGING_DIRECTORY OUTPUT_DIRECTORY VERSION PLATFORM HTTPS_RELEASE_BASE_URL PRIVATE_KEY_PEM");
    return 1;
}
var source = Path.GetFullPath(args[0]); var output = Path.GetFullPath(args[1]);
var version = args[2]; var platform = args[3];
Releases.Https(args[4]);
var paths = Directory.GetFiles(source, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal).ToArray();
var files = paths.Select(p => new ReleaseFile(Path.GetRelativePath(source, p).Replace('\\', '/'), Releases.HashFile(p), new FileInfo(p).Length,
    Path.GetRelativePath(source, p).Replace('\\', '/').StartsWith("BepInEx/config/", StringComparison.Ordinal))).ToArray();
foreach (var f in files) Releases.Destination(source, f.Path);
using var memory = new MemoryStream();
using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, true))
    for (int i = 0; i < paths.Length; i++)
    {
        var entry = archive.CreateEntry(files[i].Path, CompressionLevel.Optimal);
        entry.LastWriteTime = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var stream = entry.Open(); using var input = File.OpenRead(paths[i]); input.CopyTo(stream);
    }
var payload = memory.ToArray();
var filename = $"scrubclub-{version}-{platform}.zip";
var release = new Release(1, version, platform, args[4].TrimEnd('/') + "/" + filename, Releases.Hash(payload), payload.LongLength, files);
Releases.Validate(release, platform);
var manifest = JsonSerializer.SerializeToUtf8Bytes(release);
using var rsa = RSA.Create(); rsa.ImportFromPem(File.ReadAllText(args[5]));
var signature = rsa.SignData(manifest, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
Directory.CreateDirectory(output);
File.WriteAllBytes(Path.Combine(output, filename), payload);
File.WriteAllBytes(Path.Combine(output, $"release-{platform}.json"), JsonSerializer.SerializeToUtf8Bytes(new SignedRelease(Convert.ToBase64String(manifest), Convert.ToBase64String(signature))));
Console.WriteLine($"Prepared {platform} {version}: {files.Length} files, {payload.Length} bytes. Upload ZIP and release-{platform}.json together.");
return 0;
