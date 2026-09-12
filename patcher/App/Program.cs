using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Scrubclub.Patching;

if (args.Length == 0 || args[0] == "--gui") return LauncherUi.Run(args.Skip(1).ToArray());
bool interactive = args.Length == 0;
try
{
    if (args.Contains("--help"))
    {
        Console.WriteLine("Scrubclub Patcher\nDouble-click and follow the prompts, or:\nScrubclubPatcher --game-dir PATH --manifest-url HTTPS_URL --yes\nOffline: --manifest-file SIGNED_JSON --payload-file ZIP --game-dir PATH --yes\nAll releases must match the embedded signing key. Close Valheim before patching.");
        return 0;
    }
    var options = new Dictionary<string, string>();
    bool yes = false;
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--yes") { yes = true; continue; }
        if (args[i] is not ("--game-dir" or "--manifest-url" or "--manifest-file" or "--payload-file") || i + 1 == args.Length)
            throw new ArgumentException("Unknown or incomplete option: " + args[i]);
        options.Add(args[i], args[++i]);
    }
    string? Option(string key) => options.GetValueOrDefault(key);
    Console.WriteLine("Scrubclub Patcher — Blue Depot\nClose Valheim before continuing. Saves and characters are not modified.");
    var platform = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsLinux() ? "linux" : throw new PlatformNotSupportedException("Windows and Linux are supported.");
    var game = Option("--game-dir") ?? Discover(platform);
    if (interactive)
    {
        Console.Write($"Valheim folder{(game == null ? "" : " [" + game + "]")}: ");
        var answer = Console.ReadLine()?.Trim().Trim('"'); if (!string.IsNullOrEmpty(answer)) game = answer;
    }
    if (string.IsNullOrWhiteSpace(game)) throw new ArgumentException("Supply --game-dir or run without arguments to select your Valheim folder.");
    var manifestUrl = Option("--manifest-url");
    if (manifestUrl == null && Option("--manifest-file") == null)
    {
        var config = Path.Combine(AppContext.BaseDirectory, "patcher-settings.json");
        if (File.Exists(config))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(config));
            if (json.RootElement.TryGetProperty(platform + "ManifestUrl", out var value)) manifestUrl = value.GetString();
        }
        manifestUrl ??= "https://github.com/jStimpert0430/scrubclub-patcher/releases/latest/download/release-" + platform + ".json";
    }
    using var keyStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ScrubclubPatcher.release-public.pem") ?? throw new IOException("Missing embedded release key.");
    using var keyReader = new StreamReader(keyStream);
    Console.WriteLine("Checking signed release…");
    var envelope = Option("--manifest-file") is { } localManifest ? File.ReadAllBytes(localManifest) : await Downloads.Get(manifestUrl ?? throw new ArgumentException("A manifest URL is required."), 2 * 1024 * 1024);
    var release = Releases.Verify(envelope, keyReader.ReadToEnd(), platform);
    Console.WriteLine($"Release {release.Version} ({platform}): {release.Files.Length} files, {release.PayloadSize / 1024:N0} KiB.\nTarget: {Path.GetFullPath(game)}\nExisting mod files in this release will be backed up before replacement. Existing configuration is preserved.");
    if (!yes)
    {
        Console.Write("Install/update? [y/N] ");
        if (!string.Equals(Console.ReadLine()?.Trim(), "y", StringComparison.OrdinalIgnoreCase)) return 0;
    }
    CheckGameClosed();
    Console.WriteLine("Downloading and verifying files…");
    var payload = Option("--payload-file") is { } localPayload ? File.ReadAllBytes(localPayload) : await Downloads.Get(release.PayloadUrl, Releases.MaxPayload);
    CheckGameClosed();
    var result = Installer.Install(game, release, payload);
    Console.WriteLine($"Done. {result.Changed} files changed; {result.Preserved} existing configuration files preserved.");
    if (result.BackupDirectory.Length > 0) Console.WriteLine("Backups: " + result.BackupDirectory);
    Console.WriteLine(platform == "windows" ? "Launch Valheim from Steam as usual." : "Launch ./start_game_bepinex.sh from the Valheim folder to load mods (native Linux build).");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine("Update failed: " + error.Message);
    return 1;
}
finally
{
    if (interactive) { Console.Write("Press Enter to close…"); Console.ReadLine(); }
}

static void CheckGameClosed()
{
    foreach (var process in Process.GetProcesses())
        using (process)
            if (process.ProcessName.Equals("valheim", StringComparison.OrdinalIgnoreCase) || process.ProcessName.Equals("valheim.x86_64", StringComparison.OrdinalIgnoreCase))
                throw new IOException("Valheim is running. Save and close it before patching.");
}

static string? Discover(string platform)
{
    var roots = new List<string>();
    if (OperatingSystem.IsWindows())
    {
        using var steam = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        if (steam?.GetValue("SteamPath") is string location) roots.Add(location);
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
    }
    else
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        roots.Add(Path.Combine(home, ".local/share/Steam"));
        roots.Add(Path.Combine(home, ".var/app/com.valvesoftware.Steam/data/Steam"));
    }
    foreach (var root in roots.ToArray())
    {
        var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
            foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\"")) roots.Add(match.Groups[1].Value.Replace(@"\\", @"\"));
    }
    return roots.Select(root => Path.Combine(root, "steamapps", "common", "Valheim"))
        .FirstOrDefault(path => File.Exists(Path.Combine(path, platform == "windows" ? "valheim.exe" : "valheim.x86_64")));
}
