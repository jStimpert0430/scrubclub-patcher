using System.Numerics;
using System.Reflection;
using System.Diagnostics;
using System.Text.Json;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using Scrubclub.Patching;

internal sealed class LauncherUi
{
    sealed record Activity(string Text, float Progress = 0, bool Busy = false, bool Error = false);
    volatile Activity activity = new("Checking for updates...", 0, true);
    volatile Release? available;
    readonly LauncherSettings settings;
    IReadOnlyList<CharacterChoice> characters = [];
    string game, server, saves, characterId;
    bool joinServer;
    string installed = "Not installed";
    readonly string publicKey;
    readonly CancellationTokenSource cancellation = new();
    Task? work;
    volatile bool writingFiles;
    bool showSettings;
    readonly bool smoke;
    readonly bool isolated;
    ImFontPtr headingFont;
    readonly int closeAfterFrames;
    int frames;
    static readonly Vector4 Teal = new(.31f, .83f, .73f, 1), Muted = new(.55f, .63f, .7f, 1), Gold = new(.91f, .73f, .42f, 1);

    LauncherUi(string[] args)
    {
        smoke = args.Contains("--smoke");
        isolated = args.Contains("--game-dir");
        closeAfterFrames = smoke ? 600 : 0;
        settings = smoke || isolated ? new LauncherSettings() : LauncherSettings.Load();
        game = settings.GameDirectory; if (game.Length == 0) game = Launcher.DiscoverGame() ?? "";
        int gameOption = Array.IndexOf(args, "--game-dir");
        if (gameOption >= 0 && gameOption + 1 < args.Length) game = args[gameOption + 1];
        server = settings.ServerAddress; saves = settings.SaveDirectory; characterId = settings.CharacterId; joinServer = settings.JoinServer;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ScrubclubPatcher.release-public.pem")!;
        using var reader = new StreamReader(stream); publicKey = reader.ReadToEnd();
        RefreshCharacters(); ReadInstalled();
    }
    public static int Run(string[] args)
    {
        try { new LauncherUi(args).RunWindow(); return 0; }
        catch (Exception e)
        {
            var directory = Path.GetDirectoryName(LauncherSettings.SettingsPath)!;
            Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, "launcher-error.log"), e.ToString());
            Console.Error.WriteLine("Launcher could not open: " + e.Message);
            if (OperatingSystem.IsWindows()) MessageBoxW(IntPtr.Zero, "The launcher could not open.\n" + e.Message + "\n\nDetails: " + directory, "Scrubclub Launcher", 0x10);
            return 1;
        }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int MessageBoxW(IntPtr window, string message, string caption, uint type);

    void RunWindow()
    {
        var options = WindowOptions.Default;
        options.Title = "Scrubclub Launcher";
        options.Size = new Vector2D<int>(1000, 660);
        options.FramesPerSecond = 60; options.UpdatesPerSecond = 60;
        options.VSync = true;
        using var window = Window.Create(options);
        GL? gl = null; IInputContext? input = null; ImGuiController? controller = null;
        window.Load += () =>
        {
            gl = window.CreateOpenGL(); input = window.CreateInput();
            controller = new ImGuiController(gl, window, input, () =>
            {
                var io = ImGui.GetIO();
                unsafe { io.NativePtr->IniFilename = null; }
                var font = Path.Combine(AppContext.BaseDirectory, "assets/Inter-Regular.ttf");
                if (File.Exists(font)) { io.Fonts.AddFontFromFileTTF(font, 18); headingFont = io.Fonts.AddFontFromFileTTF(font, 36); }
                else io.FontGlobalScale = 1.35f;
                io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
            });
            Style();
            if (smoke) activity = new("Preview - no update or game launch performed");
            else CheckRelease();
        };
        window.FramebufferResize += size => gl?.Viewport(size);
        window.Render += delta =>
        {
            controller!.Update((float)delta);
            gl!.ClearColor(.045f, .065f, .09f, 1);
            gl.Clear(ClearBufferMask.ColorBufferBit);
            Draw(); controller.Render();
            if (closeAfterFrames > 0 && ++frames >= closeAfterFrames) window.Close();
        };
        window.Closing += () =>
        {
            if (writingFiles) { window.IsClosing = false; return; }
            cancellation.Cancel();
            // OpenGL resources must be released while the window's context still exists.
            controller?.Dispose(); controller = null;
            input?.Dispose(); input = null;
            gl?.Dispose(); gl = null;
        };
        try { window.Run(); }
        finally
        {
            cancellation.Cancel();
            try { work?.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
            controller?.Dispose(); input?.Dispose(); gl?.Dispose(); cancellation.Dispose();
        }
    }
    static void Style()
    {
        ImGui.StyleColorsDark(); var s = ImGui.GetStyle();
        s.WindowPadding = new(28, 24); s.FramePadding = new(12, 10); s.ItemSpacing = new(12, 12);
        s.WindowRounding = 0; s.FrameRounding = 5; s.ChildRounding = 8; s.PopupRounding = 6; s.GrabRounding = 4;
        s.WindowBorderSize = 0; s.FrameBorderSize = 1;
        s.Colors[(int)ImGuiCol.WindowBg] = new(.045f, .065f, .09f, 1);
        s.Colors[(int)ImGuiCol.ChildBg] = new(.068f, .093f, .124f, 1);
        s.Colors[(int)ImGuiCol.FrameBg] = new(.095f, .128f, .166f, 1);
        s.Colors[(int)ImGuiCol.Border] = new(.17f, .23f, .28f, 1);
        s.Colors[(int)ImGuiCol.Button] = new(.12f, .22f, .26f, 1);
        s.Colors[(int)ImGuiCol.ButtonHovered] = new(.17f, .34f, .38f, 1);
        s.Colors[(int)ImGuiCol.ButtonActive] = new(.12f, .43f, .41f, 1);
        s.Colors[(int)ImGuiCol.Header] = new(.12f, .29f, .31f, 1);
        s.Colors[(int)ImGuiCol.CheckMark] = Teal;
        s.Colors[(int)ImGuiCol.PlotHistogram] = Teal;
        s.Colors[(int)ImGuiCol.Text] = new(.9f, .93f, .95f, 1);
    }
    unsafe void Draw()
    {
        var size = ImGui.GetIO().DisplaySize;
        ImGui.SetNextWindowPos(Vector2.Zero); ImGui.SetNextWindowSize(size);
        ImGui.Begin("Scrubclub", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings);
        ImGui.TextColored(Teal, "V A L H E I M   /   C O M M U N I T Y  L A U N C H E R");
        if (headingFont.NativePtr != null) ImGui.PushFont(headingFont);
        ImGui.TextUnformatted("SCRUBCLUB");
        if (headingFont.NativePtr != null) ImGui.PopFont();
        ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
        float contentHeight = Math.Max(230, size.Y - 290);
        float leftWidth = Math.Max(240, (size.X - 68) * .46f);
        ImGui.BeginChild("Release", new Vector2(leftWidth, contentHeight), ImGuiChildFlags.Border);
        ImGui.TextColored(Gold, "SERVER MODS");
        ImGui.TextColored(Muted, "Installed: " + installed);
        ImGui.TextColored(Muted, "Available: " + (available?.Version ?? (smoke ? "Checking when connected" : "Checking...")));
        ImGui.Separator();
        var mods = available?.Files.Where(f => f.Path.StartsWith("BepInEx/plugins/") && f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Path.Split('/').Length > 3 ? f.Path.Split('/')[2] : Path.GetFileNameWithoutExtension(f.Path))
            .Distinct().OrderBy(s => s).ToArray();
        if (mods != null)
            foreach (var mod in mods) { ImGui.TextColored(Teal, "+"); ImGui.SameLine(); ImGui.TextUnformatted(mod); }
        else ImGui.TextWrapped("Your server's mod collection appears here after checking the signed release.");
        ImGui.Spacing(); ImGui.TextColored(Muted, "Updates include new mods automatically.");
        ImGui.TextWrapped("Game saves stay yours. Existing configuration is preserved and replaced files are backed up.");
        ImGui.EndChild(); ImGui.SameLine();
        ImGui.BeginChild("Character", new Vector2(0, contentHeight), ImGuiChildFlags.Border);
        ImGui.TextColored(Gold, "YOUR VIKING");
        var selected = characters.FirstOrDefault(c => c.Id == characterId);
        ImGui.BeginDisabled(activity.Busy);
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##character", selected?.Label ?? "Choose in game"))
        {
            if (ImGui.Selectable("Choose in game", selected == null)) { characterId = ""; Save(); }
            foreach (var c in characters)
                if (ImGui.Selectable(c.Label, c.Id == characterId)) { characterId = c.Id; Save(); }
            ImGui.EndCombo();
        }
        if (ImGui.SmallButton("Refresh characters")) RefreshCharacters();
        if (characters.Count == 0) ImGui.TextWrapped("No saved characters found. You can create or choose one inside Valheim.");
        ImGui.Spacing(); ImGui.Separator();
        if (ImGui.Checkbox("Join scrubclub", ref joinServer)) Save();
        ImGui.TextColored(Muted, joinServer ? "The server password is entered in Valheim." : "Launch to the menu for a local world.");
        ImGui.Spacing();
        if (showSettings) { ImGui.SetNextItemOpen(true); showSettings = false; }
        if (ImGui.CollapsingHeader("Installation & connection"))
        {
            ImGui.TextUnformatted("Valheim folder"); ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##game", ref game, 2048)) { ReadInstalled(); }
            if (ImGui.IsItemDeactivatedAfterEdit()) Save();
            if (ImGui.SmallButton("Find Steam installation")) { game = Launcher.DiscoverGame() ?? ""; ReadInstalled(); Save(); }
            ImGui.TextUnformatted("Server address"); ImGui.SetNextItemWidth(-1); ImGui.InputText("##server", ref server, 512);
            if (ImGui.IsItemDeactivatedAfterEdit()) Save();
            ImGui.TextUnformatted("Custom saves folder (optional)"); ImGui.SetNextItemWidth(-1); ImGui.InputText("##saves", ref saves, 2048);
            if (ImGui.IsItemDeactivatedAfterEdit()) { RefreshCharacters(); Save(); }
        }
        ImGui.EndDisabled(); ImGui.EndChild();
        ImGui.SetCursorPos(new Vector2(28, Math.Max(ImGui.GetCursorPosY() + 8, size.Y - 103)));
        var state = activity;
        ImGui.TextColored(state.Error ? new Vector4(1, .52f, .48f, 1) : Muted, state.Text);
        ImGui.SetCursorPos(new Vector2(28, size.Y - 66));
        ImGui.BeginDisabled(state.Busy);
        if (ImGui.Button("Update", new Vector2(128, 42))) StartUpdate();
        ImGui.EndDisabled(); ImGui.SameLine();
        ImGui.ProgressBar(state.Progress, new Vector2(Math.Max(100, size.X - 386), 42), state.Progress > 0 ? $"{state.Progress:P0}" : "Ready");
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.16f, .43f, .38f, 1));
        ImGui.BeginDisabled(state.Busy || smoke);
        if (ImGui.Button("Launch game", new Vector2(178, 42))) LaunchGame();
        ImGui.EndDisabled(); ImGui.PopStyleColor();
        ImGui.End();
    }
    void RefreshCharacters()
    {
        try { characters = Launcher.DiscoverCharacters(saves); }
        catch (Exception e) { activity = new("Could not read character list: " + e.Message, Error: true); }
    }
    void ReadInstalled()
    {
        try { installed = JsonSerializer.Deserialize<Installed>(File.ReadAllText(Path.Combine(game, ".scrubclub-patcher/installed.json")))?.Version ?? "Not installed"; }
        catch { installed = "Not installed"; }
    }
    void Save()
    {
        if (smoke) return;
        settings.GameDirectory = game; settings.ServerAddress = server; settings.SaveDirectory = saves; settings.CharacterId = characterId; settings.JoinServer = joinServer;
        if (isolated) return;
        try { settings.Save(); } catch (Exception e) { activity = new("Could not save launcher settings: " + e.Message, Error: true); }
    }
    void CheckRelease()
    {
        work = Task.Run(async () =>
        {
            try
            {
                available = Releases.Verify(await Downloads.Get(Launcher.ManifestUrl, 2 * 1024 * 1024, cancellation.Token), publicKey, Launcher.Platform);
                activity = new(installed == available.Version ? "Up to date. Ready to play." : "Release " + available.Version + " is ready to install.", installed == available.Version ? 1 : 0);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { activity = new("Update check failed: " + e.Message, Error: true); }
        });
    }
    void StartUpdate()
    {
        if (activity.Busy || smoke) return;
        var target = game;
        if (!File.Exists(Path.Combine(target, Launcher.GameMarker(Launcher.Platform))))
        { showSettings = true; activity = new("Choose your Valheim folder under Installation & connection.", Error: true); return; }
        Save(); activity = new("Checking signed release...", 0, true);
        work = Task.Run(async () =>
        {
            try
            {
                Launcher.CheckGameClosed();
                var release = Releases.Verify(await Downloads.Get(Launcher.ManifestUrl, 2 * 1024 * 1024, cancellation.Token), publicKey, Launcher.Platform);
                available = release;
                var payload = await Downloads.Get(release.PayloadUrl, Releases.MaxPayload, cancellation.Token,
                    (count, total) => activity = new($"Downloading mods: {count / 1024:N0} / {release.PayloadSize / 1024:N0} KiB", .05f + .7f * count / release.PayloadSize, true));
                cancellation.Token.ThrowIfCancellationRequested(); Launcher.CheckGameClosed();
                activity = new("Verifying files and making backups...", .78f, true);
                writingFiles = true;
                var result = Installer.Install(target, release, payload, progress: (done, total) => activity = new($"Installing files: {done} / {total}", .8f + .19f * done / total, true));
                installed = release.Version; activity = new($"Ready to play. {result.Changed} files updated.", 1);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { activity = new("Update failed: " + e.Message, Error: true); }
            finally { writingFiles = false; }
        });
    }
    void LaunchGame()
    {
        try
        {
            Launcher.CheckGameClosed(); Save();
            var start = Launcher.BuildLaunch(settings, characters.FirstOrDefault(c => c.Id == characterId), Launcher.Platform);
            Process.Start(start); activity = new("Valheim is starting. Steam must be running.", 1);
        }
        catch (Exception e) { showSettings = true; activity = new("Could not launch: " + e.Message, Error: true); }
    }
}
