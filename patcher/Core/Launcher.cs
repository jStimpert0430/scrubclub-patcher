using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Scrubclub.Patching;

public sealed record CharacterChoice(string Name, string Source, string Path)
{
    public string Id => Source + ":" + Name;
    public string Label => Name + (Source == "Cloud" ? "  (Steam Cloud)" : "  (Local)");
}
public sealed class LauncherSettings
{
    public string GameDirectory { get; set; } = "";
    public string ServerAddress { get; set; } = "108.88.196.104:2456";
    public string SaveDirectory { get; set; } = "";
    public string CharacterId { get; set; } = "";
    public bool JoinServer { get; set; } = true;
    public static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScrubclubLauncher", "settings.json");
    public static LauncherSettings Load()
    {
        try { return JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(SettingsPath)) ?? new(); }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var temp = SettingsPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, SettingsPath, true);
    }
}

public static class Launcher
{
    public static string Platform => OperatingSystem.IsWindows() ? "windows" : "linux";
    public static string ManifestUrl => "https://github.com/jStimpert0430/scrubclub-patcher/releases/latest/download/release-" + Platform + ".json";
    public static string GameMarker(string platform) => platform == "windows" ? "valheim.exe" : "valheim.x86_64";
    public static void CheckGameClosed()
    {
        foreach (var p in Process.GetProcesses())
            using (p)
                if (p.ProcessName.Equals("valheim", StringComparison.OrdinalIgnoreCase) || p.ProcessName.Equals("valheim.x86_64", StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Save and close Valheim before updating or launching another instance.");
    }
    public static List<string> SteamRoots()
    {
        var roots = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (key?.GetValue("SteamPath") is string path) roots.Add(path);
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            roots.Add(Path.Combine(home, ".local/share/Steam"));
            roots.Add(Path.Combine(home, ".var/app/com.valvesoftware.Steam/data/Steam"));
        }
        return roots.Distinct().Where(Directory.Exists).ToList();
    }
    public static string? DiscoverGame()
    {
        var roots = SteamRoots();
        foreach (var root in roots.ToArray())
        {
            var file = Path.Combine(root, "steamapps/libraryfolders.vdf");
            if (File.Exists(file))
                foreach (Match m in Regex.Matches(File.ReadAllText(file), "\"path\"\\s+\"([^\"]+)\"")) roots.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
        }
        return roots.Select(r => Path.Combine(r, "steamapps/common/Valheim")).FirstOrDefault(p => File.Exists(Path.Combine(p, GameMarker(Platform))));
    }
    public static string DefaultSaveDirectory => OperatingSystem.IsWindows()
        ? Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "../LocalLow/IronGate/Valheim"))
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config/unity3d/IronGate/Valheim");

    public static IReadOnlyList<CharacterChoice> DiscoverCharacters(string saveDirectory)
    {
        var folders = new List<(string Path, string Source)>();
        var saves = string.IsNullOrWhiteSpace(saveDirectory) ? DefaultSaveDirectory : saveDirectory;
        folders.Add((Path.Combine(saves, "characters_local"), "Local"));
        folders.Add((Path.Combine(saves, "characters"), "Local"));
        foreach (var steam in SteamRoots())
        {
            var accounts = Path.Combine(steam, "userdata");
            if (!Directory.Exists(accounts)) continue;
            var active = ActiveAccount(steam);
            var candidates = Directory.GetDirectories(accounts).Where(p => uint.TryParse(Path.GetFileName(p), out _)).ToArray();
            // Never offer another signed-in person's character when several accounts share this PC.
            if (active != null) candidates = candidates.Where(p => Path.GetFileName(p) == active).ToArray();
            else if (candidates.Length != 1) continue;
            foreach (var account in candidates) folders.Add((Path.Combine(account, "892970/remote/characters"), "Cloud"));
        }
        return ScanCharacters(folders);
    }
    public static string? ActiveAccount(string steam)
    {
        if (OperatingSystem.IsWindows())
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
            if (key?.GetValue("ActiveUser") is int id && id != 0) return unchecked((uint)id).ToString();
        }
        var file = Path.Combine(steam, "config/loginusers.vdf");
        if (!File.Exists(file)) return null;
        foreach (Match block in Regex.Matches(File.ReadAllText(file), "\"(\\d{17})\"\\s*\\{([^}]+)\\}"))
            if (Regex.IsMatch(block.Groups[2].Value, "\"MostRecent\"\\s*\"1\"", RegexOptions.IgnoreCase) && ulong.TryParse(block.Groups[1].Value, out var id) && id >= 76561197960265728UL)
                return (id - 76561197960265728UL).ToString();
        return null;
    }
    public static IReadOnlyList<CharacterChoice> ScanCharacters(IEnumerable<(string Path,string Source)> folders)
    {
        var results = new List<CharacterChoice>();
        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder.Path)) continue;
            foreach (var file in Directory.EnumerateFiles(folder.Path, "*.fch"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (name.Contains("_backup", StringComparison.OrdinalIgnoreCase)) continue;
                results.Add(new(name, folder.Source, file));
            }
        }
        return results.DistinctBy(c => c.Id).OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.Source).ToArray();
    }
    public static string NormalizeServer(string address)
    {
        address = address.Trim();
        if (address.Length == 0 || address.Any(char.IsWhiteSpace) || address.Contains('/') || address.Contains('@') || address.Contains('?') || address.Contains('#'))
            throw new ArgumentException("Enter the server DNS name or IP, optionally followed by :2456.");
        if (!Uri.TryCreate("tcp://" + address, UriKind.Absolute, out var uri) || uri.HostNameType == UriHostNameType.Unknown || uri.UserInfo.Length != 0 || (uri.Port != -1 && uri.Port is <1 or >65535))
            throw new ArgumentException("Invalid server address or port.");
        return uri.Port == -1 ? address + ":2456" : address;
    }
    public static ProcessStartInfo BuildLaunch(LauncherSettings settings, CharacterChoice? character, string platform)
    {
        var game = Path.GetFullPath(settings.GameDirectory);
        if (!File.Exists(Path.Combine(game, GameMarker(platform)))) throw new IOException("Choose your Valheim installation folder first.");
        var executable = platform == "windows" ? "valheim.exe" : "start_game_bepinex.sh";
        if (!File.Exists(Path.Combine(game, executable))) throw new IOException("Click Update to install the mod loader first.");
        var start = new ProcessStartInfo(Path.Combine(game, executable)) { WorkingDirectory = game, UseShellExecute = false };
        start.Environment["SteamAppId"] = "892970";
        start.Environment["SteamGameId"] = "892970";
        if (!string.IsNullOrWhiteSpace(settings.SaveDirectory)) { start.ArgumentList.Add("-savedir"); start.ArgumentList.Add(Path.GetFullPath(settings.SaveDirectory)); }
        if (settings.JoinServer) { start.ArgumentList.Add("+connect"); start.ArgumentList.Add(NormalizeServer(settings.ServerAddress)); }
        if (character != null)
        {
            if (!File.Exists(character.Path)) throw new IOException("That character is no longer available. Refresh the list.");
            if (!File.Exists(Path.Combine(game, "BepInEx/plugins/ScrubclubLauncher/ScrubclubLauncherBridge.dll"))) throw new IOException("Click Update to install character selection support first.");
            start.ArgumentList.Add("-scrubclub-character"); start.ArgumentList.Add(character.Name);
            start.ArgumentList.Add("-scrubclub-source"); start.ArgumentList.Add(character.Source);
        }
        return start;
    }
}
