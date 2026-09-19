using System;
using BepInEx;
using HarmonyLib;
namespace ScrubclubAchievements;
[BepInPlugin(Guid,"Scrubclub Achievements","0.1.0")]
public sealed class Plugin:BaseUnityPlugin
{
    public const string Guid="scrubclub.achievements";
    Harmony harmony;
    void Awake()
    {
        harmony=new Harmony(Guid);
        try
        {
            harmony.PatchAll(typeof(Plugin).Assembly);AchievementPatch.InvalidateCache();
            Logger.LogInfo("Modded achievement progression enabled. Native cheat, world-modifier and achievement requirements remain in effect. No achievements granted or saves edited.");
        }
        catch(Exception error)
        {
            harmony.UnpatchSelf();Logger.LogError("Achievement support could not initialize; normal game checks remain active. "+error);
        }
    }
    void OnDestroy(){harmony?.UnpatchSelf();AchievementPatch.InvalidateCache();}
}
