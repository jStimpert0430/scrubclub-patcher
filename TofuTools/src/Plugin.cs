using BepInEx;
using HarmonyLib;

namespace TofuTools;

[BepInPlugin("scrubclub.tofutools", "TofuTools", "0.1.0")]
public sealed class Plugin : BaseUnityPlugin
{
    private Harmony harmony;

    private void Awake()
    {
        harmony = new Harmony("scrubclub.tofutools");
        harmony.PatchAll();
        Logger.LogInfo("Death protection active: earned skill levels retained; penalized deaths clear only current-level XP.");
    }

    private void OnDestroy() => harmony?.UnpatchSelf();
}
