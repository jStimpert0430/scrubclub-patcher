using Scrubclub.Patching;
using Xunit;
namespace Scrubclub.Tests;

public sealed class LauncherTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "launcher-test-" + Guid.NewGuid().ToString("N"));
    public LauncherTests() { Directory.CreateDirectory(root); }
    public void Dispose() => Directory.Delete(root, true);
    [Fact] public void CharacterDiscoveryExcludesBackupsAndKeepsStorageDistinct()
    {
        var local = Path.Combine(root, "local"); var cloud = Path.Combine(root, "cloud");
        Directory.CreateDirectory(local); Directory.CreateDirectory(cloud);
        foreach (var name in new[] { "Viking.fch", "Viking_backup_auto-20260912.fch", "Viking.fch.old" }) File.WriteAllText(Path.Combine(local, name), "");
        File.WriteAllText(Path.Combine(cloud, "Viking.fch"), "");
        var result = Launcher.ScanCharacters([(local, "Local"), (local, "Local"), (cloud, "Cloud")]);
        Assert.Equal(2, result.Count); Assert.Contains(result, c => c.Id == "Local:Viking"); Assert.Contains(result, c => c.Id == "Cloud:Viking");
    }
    [Theory][InlineData("example.org", "example.org:2456")][InlineData("192.0.2.1:2457", "192.0.2.1:2457")][InlineData("[::1]", "[::1]:2456")]
    public void ServerAddressNormalizesPort(string input, string expected) => Assert.Equal(expected, Launcher.NormalizeServer(input));
    [Theory][InlineData("")][InlineData("https://example.com")][InlineData("user@example.com")][InlineData("example.com:0")][InlineData("example.com; rm x")][InlineData("example.com/path")]
    public void UnsafeOrInvalidServerRejected(string value) => Assert.Throws<ArgumentException>(() => Launcher.NormalizeServer(value));
    [Fact] public void WindowsLaunchPassesCharacterAndServerAsSeparateArguments()
    {
        File.WriteAllText(Path.Combine(root, "valheim.exe"), "");
        var bridge = Path.Combine(root, "BepInEx/plugins/ScrubclubLauncher"); Directory.CreateDirectory(bridge); File.WriteAllText(Path.Combine(bridge, "ScrubclubLauncherBridge.dll"), "");
        var save = Path.Combine(root, "A name with spaces.fch"); File.WriteAllText(save, "");
        var settings = new LauncherSettings { GameDirectory = root, ServerAddress = "example.org", JoinServer = true };
        var start = Launcher.BuildLaunch(settings, new("A name with spaces", "Cloud", save), "windows");
        Assert.False(start.UseShellExecute);
        Assert.Equal(new[] { "+connect", "example.org:2456", "-scrubclub-character", "A name with spaces", "-scrubclub-source", "Cloud" }, start.ArgumentList);
        Assert.Equal("892970", start.Environment["SteamAppId"]);
        Assert.DoesNotContain(start.ArgumentList, a => a.Contains("password", StringComparison.OrdinalIgnoreCase));
    }
    [Fact] public void LinuxLaunchUsesModLoaderAndCustomSavesWithoutServer()
    {
        File.WriteAllText(Path.Combine(root, "valheim.x86_64"), ""); File.WriteAllText(Path.Combine(root, "start_game_bepinex.sh"), "");
        var settings = new LauncherSettings { GameDirectory = root, SaveDirectory = root, JoinServer = false };
        var start = Launcher.BuildLaunch(settings, null, "linux");
        Assert.EndsWith("start_game_bepinex.sh", start.FileName); Assert.Equal(new[] { "-savedir", root }, start.ArgumentList);
    }
    [Fact] public void MissingCharacterBridgeBlocksMisleadingSelection()
    {
        File.WriteAllText(Path.Combine(root, "valheim.exe"), ""); var save = Path.Combine(root, "viking.fch"); File.WriteAllText(save, "");
        Assert.Throws<IOException>(() => Launcher.BuildLaunch(new() { GameDirectory = root, JoinServer = false }, new("viking", "Local", save), "windows"));
    }
}
