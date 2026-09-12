using System;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;

namespace Scrubclub.LauncherBridge;

[BepInPlugin("scrubclub.launcher.bridge", "Scrubclub Launcher", "0.2.0")]
public sealed class Plugin : BaseUnityPlugin
{
    static string selectedName, selectedSource;
    static bool consumed;
    static BepInEx.Logging.ManualLogSource log;
    void Awake()
    {
        log = Logger;
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "-scrubclub-character") selectedName = args[++i];
            else if (args[i] == "-scrubclub-source") selectedSource = args[++i];
        }
        if (string.IsNullOrEmpty(selectedName) || (selectedSource != "Local" && selectedSource != "Cloud")) return;
        new Harmony("scrubclub.launcher.bridge").PatchAll();
    }

    [HarmonyPatch(typeof(FejdStartup), "ShowCharacterSelection")]
    static class SelectFromLauncher
    {
        static void Postfix(FejdStartup __instance, List<PlayerProfile> ___m_profiles, ref int ___m_profileIndex, ServerJoinData ___m_queuedJoinServer)
        {
            if (consumed) return;
            consumed = true; // Never select again after returning from a world or failed connection.
            if (___m_profiles == null) return;
            var index = ___m_profiles.FindIndex(p => p.GetFilename() == selectedName && p.m_fileSource.ToString() == selectedSource);
            if (index < 0)
            {
                log.LogWarning("Launcher character unavailable in the active Steam account; choose a character in-game.");
                return;
            }
            ___m_profileIndex = index;
            AccessTools.Method(typeof(FejdStartup), "UpdateCharacterList").Invoke(__instance, null);
            log.LogInfo("Selected launcher character from " + selectedSource + " storage.");
            // Preserve Valheim's own EULA, privileges, password prompt and join handling.
            if (___m_queuedJoinServer.IsValid) __instance.OnCharacterStart();
        }
    }
}
