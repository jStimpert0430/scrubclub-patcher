using System;
using System.Globalization;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using TofuFog.Core;
using UnityEngine;

namespace TofuFog;
[BepInPlugin("scrubclub.tofufog", "TofuFog", "0.1.0")]
public sealed class Plugin : BaseUnityPlugin
{
    internal static ConfigEntry<float> Atmosphere, Ground, Mistlands;
    private Harmony harmony;
    private static float originalFog, appliedFog;
    private static bool wroteFog;
    private static bool wisplightKnown, inMistlands;

    private static void RefreshProgression(EnvMan env)
    {
        var player = Player.m_localPlayer;
        // Native known-item history persists across sessions; do not unlock the server
        // globally or write a new character flag. Missing game data keeps mist vanilla.
        var item = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab("Demister") : null;
        var drop = item ? item.GetComponent<ItemDrop>() : null;
        bool known = player && drop && player.IsMaterialKnown(drop.m_itemData.m_shared.m_name);
        bool mist = env && ((env.GetCurrentBiome() & Heightmap.Biome.Mistlands) != 0
            || (env.GetCurrentEnvironment()?.m_name ?? "").IndexOf("mistlands", StringComparison.OrdinalIgnoreCase) >= 0);
        if (known == wisplightKnown && mist == inMistlands) return;
        wisplightKnown = known; inMistlands = mist;
        FogParticles.RefreshAll();
    }

    private void Awake()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Atmosphere = Setting("AtmosphericDensity", "Distance fog at all times of day and in all weather.");
        Ground = Setting("GroundFogOpacity", "Drifting ground fog and distant fog banks.");
        Mistlands = Setting("MistlandsOpacity", "Mistlands mist opacity, unlocked only after this character obtains a Wisplight. Native clearing unchanged.");
        harmony = new Harmony("scrubclub.tofufog"); harmony.PatchAll();
        new Terminal.ConsoleCommand("tofufog", "Show fog settings, or set all multipliers: tofufog 0.5 (half), 0.25 (quarter), 1 (vanilla)", args =>
        {
            if (args.Args.Length == 2)
            {
                if (!float.TryParse(args.Args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                    || float.IsNaN(value) || value < 0 || value > 1)
                { args.Context.AddString("Use a number from 0 to 1."); return; }
                Atmosphere.Value = Ground.Value = Mistlands.Value = value;
            }
            args.Context.AddString($"TofuFog: atmosphere {Atmosphere.Value:0.##}, ground {Ground.Value:0.##}, Mistlands {Mistlands.Value:0.##} ({(wisplightKnown ? "unlocked" : "locked: obtain a Wisplight")}). Existing particles fade naturally.");
        });
        Logger.LogInfo("Visibility relief active: atmospheric fog, ground fog and Mistlands default to 25%. Rain, snow, lighting and weather gameplay unchanged.");
    }
    private ConfigEntry<float> Setting(string key, string description)
    {
        var result = Config.Bind("Visibility", key, .25f, new ConfigDescription(description + " 0 = invisible, 0.5 = half, 1 = vanilla. Applies locally.", new AcceptableValueRange<float>(0, 1)));
        result.SettingChanged += (_, __) => FogParticles.RefreshAll();
        return result;
    }
    internal static float Factor(Layer layer) => FogRules.GatedMultiplier(
        layer == Layer.Mistlands ? Mistlands.Value : Ground.Value,
        layer == Layer.Mistlands, inMistlands, wisplightKnown);
    private void OnDestroy()
    {
        harmony?.UnpatchSelf(); FogParticles.RestoreAll();
        if (wroteFog && RenderSettings.fogDensity == appliedFog) RenderSettings.fogDensity = originalFog;
    }

    [HarmonyPatch(typeof(EnvMan), "SetEnv")]
    private static class AtmosphericFog
    {
        private static void Prefix(EnvMan __instance, out bool __state)
        {
            RefreshProgression(__instance);
            __state = Utils.GetMainCamera() != null;
        }
        private static void Postfix(bool __state)
        {
            if (!__state) return; // Vanilla exits without assigning fog when no camera exists.
            originalFog = RenderSettings.fogDensity;
            appliedFog = FogRules.Scale(originalFog, FogRules.GatedMultiplier(Atmosphere.Value, false, inMistlands, wisplightKnown));
            RenderSettings.fogDensity = appliedFog; wroteFog = true;
        }
    }
    [HarmonyPatch(typeof(EnvMan), "SetParticleArrayEnabled")]
    private static class EnvironmentParticles
    {
        private static void Prefix(GameObject[] psystems)
        {
            if (psystems == null) return;
            foreach (var root in psystems)
            {
                if (!root) continue;
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    string path = ps.name;
                    for (var parent = ps.transform.parent; parent && parent != root.transform.parent; parent = parent.parent) path = parent.name + "/" + path;
                    var layer = FogRules.EnvironmentLayer(path);
                    if (layer != Layer.None) FogParticles.Track(ps, layer);
                }
            }
        }
    }
    [HarmonyPatch(typeof(ParticleMist), "Awake")]
    private static class MistlandsParticles
    {
        private static void Postfix(ParticleMist __instance) => FogParticles.Track(__instance.GetComponent<ParticleSystem>(), Layer.Mistlands);
    }
    [HarmonyPatch(typeof(MistEmitter), "PlaceOne")]
    private static class GroundParticles
    {
        private static void Prefix(MistEmitter __instance) => FogParticles.Track(__instance.m_psystem, Layer.Ground);
    }
    [HarmonyPatch(typeof(DistantFogEmitter), "PlaceOne")]
    private static class DistantParticles
    {
        private static void Prefix(DistantFogEmitter __instance)
        {
            if (__instance.m_psystems == null) return;
            foreach (var ps in __instance.m_psystems) FogParticles.Track(ps, Layer.Ground);
        }
    }
}
