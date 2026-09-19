using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Torchlight.Core;
namespace Torchlight;
[BepInPlugin("scrubclub.torchlight","Torchlight","0.1.0")]
public sealed class Plugin:BaseUnityPlugin
{
    static ConfigEntry<float> distance;
    Harmony harmony;
    void Awake()
    {
        distance=Config.Bind("Visibility","MinimumDistance",125f,new ConfigDescription(
            "Minimum player-to-torch/campfire visibility distance in metres. Vanilla placed torches use 80; a burning campfire uses 100. Does not change illumination radius, intensity, shadows, fuel or graphics light-count limits. Restart the game after changing.",
            new AcceptableValueRange<float>(80,160)));
        harmony=new Harmony("scrubclub.torchlight");harmony.PatchAll();
        Logger.LogInfo("Torch/campfire visibility minimum: "+distance.Value+"m. Native light radius, brightness, fading and graphics budgets retained.");
    }
    void OnDestroy()=>harmony?.UnpatchSelf();
    [HarmonyPatch(typeof(LightLod),"Awake")]
    static class TorchDistance
    {
        static void Prefix(LightLod __instance)
        {
            var piece=__instance.GetComponentInParent<Piece>(true);
            if(!piece)return;
            __instance.m_lightDistance=Visibility.Distance(piece.gameObject.name,__instance.m_lightLod,__instance.m_lightDistance,distance.Value);
        }
    }
}
