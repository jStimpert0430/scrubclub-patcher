using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using RoadLights.Core;
using UnityEngine;

namespace RoadLights;

[BepInPlugin("scrubclub.roadlights","Road Lights","0.2.0")]
[BepInDependency(Jotunn.Main.ModGuid)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod,VersionStrictness.Patch)]
public sealed class Plugin:BaseUnityPlugin
{
    static readonly HashSet<int> hashes=new HashSet<int>(LightingPolicy.Prefabs.Select(n=>n.GetStableHashCode()));
    Harmony harmony;
    internal static BepInEx.Logging.ManualLogSource Log;
    internal static BepInEx.Configuration.ConfigEntry<string> RoadSelection;
    static readonly HashSet<string> loggedPieces=new HashSet<string>();
    void Awake()
    {
        Log=Logger;
        RoadSelection=Config.Bind("Road lighting","SelectedLight","","Selected unlocked freestanding light prefab; empty disables automatic Pathen lighting.");
        gameObject.AddComponent<RoadLightingUi>();
        harmony=new Harmony("scrubclub.roadlights");harmony.PatchAll();
        PrefabManager.OnVanillaPrefabsAvailable+=ConfigurePrefabs;
    }
    void OnDestroy(){PrefabManager.OnVanillaPrefabsAvailable-=ConfigurePrefabs;harmony?.UnpatchSelf();}
    void ConfigurePrefabs()
    {
        PrefabManager.OnVanillaPrefabsAvailable-=ConfigurePrefabs;
        int count=0;
        foreach(string name in LightingPolicy.Prefabs)
        {
            var prefab=PrefabManager.Instance.GetPrefab(name);
            if(!prefab || !prefab.GetComponent<Piece>()){Logger.LogWarning("Road Lights prefab unavailable: "+name);continue;}
            var piece=prefab.GetComponent<Piece>();
            ApplyPiece(piece);
            if(!Target(piece) || piece.m_craftingStation)
            {Logger.LogError("Road Lights station configuration failed: "+name);continue;}
            foreach(var fire in prefab.GetComponentsInChildren<Fireplace>(true))ApplyFire(fire);
            count++;
        }
        Logger.LogInfo($"Road Lights configured {count} decorative light prefabs; material requirements unchanged");
    }
    internal static bool Target(Component component)
    {
        if(!component)return false;
        // Build recipes are inactive assets. Never re-discover an already supplied
        // Piece through an active-only hierarchy search.
        var piece=component as Piece;
        if(!piece)piece=component.GetComponentInParent<Piece>(true);
        if(!piece)return false;
        var view=piece.GetComponent<ZNetView>();
        if(view && view.IsValid())return hashes.Contains(view.GetZDO().GetPrefab());
        // Prefabs and placement ghosts may not have valid network data.
        string name=piece.gameObject.name;
        if(name.EndsWith("(Clone)",StringComparison.Ordinal))name=name.Substring(0,name.Length-7);
        return LightingPolicy.Includes(name);
    }
    internal static void ApplyPiece(Piece piece)
    {
        if(!piece)return;
        bool targeted=Target(piece);
        if(targeted)
        {
            string previous=piece.m_craftingStation?piece.m_craftingStation.name:"none";
            piece.m_craftingStation=null;
            if(loggedPieces.Add(piece.name+":"+piece.gameObject.activeInHierarchy))
                Log?.LogInfo($"Light recipe {piece.name}: active={piece.gameObject.activeInHierarchy}, station={previous} -> none");
        }
    }
    internal static void ApplyFire(Fireplace fire)
    {
        if(!Target(fire))return;
        fire.m_infiniteFuel=true;
        fire.m_canRefill=false;
        // Do not refill or overwrite saved fuel/state. IsBurning supports infinite fuel at zero.
    }
}

[HarmonyPatch(typeof(Piece),"Awake")]
static class ConfigurePlacedLight
{
    static void Postfix(Piece __instance)=>Plugin.ApplyPiece(__instance);
}
// Configure the actual tool's recipe objects as well as scene prefabs. Asset
// loading can supply different instances to the build menu.
[HarmonyPatch(typeof(PieceTable),nameof(PieceTable.UpdateAvailable))]
static class ConfigureBuildMenuLights
{
    static void Prefix(PieceTable __instance)
    {
        foreach(var prefab in __instance.m_pieces)
            if(prefab)Plugin.ApplyPiece(prefab.GetComponent<Piece>());
    }
}
[HarmonyPatch(typeof(Player),nameof(Player.HaveRequirements),typeof(Piece),typeof(Player.RequirementMode))]
static class ConfigureLightRequirement
{
    // Run the original requirement check, including material costs and DLC.
    static void Prefix(Piece piece)=>Plugin.ApplyPiece(piece);
}
[HarmonyPatch(typeof(Fireplace),nameof(Fireplace.Awake))]
static class ConfigurePlacedFire
{
    static void Prefix(Fireplace __instance)=>Plugin.ApplyFire(__instance);
}
[HarmonyPatch(typeof(Fireplace),nameof(Fireplace.GetHoverText))]
static class LightHover
{
    static bool Prefix(Fireplace __instance,ref string __result)
    {
        if(!Plugin.Target(__instance))return true;
        var view=__instance.GetComponent<ZNetView>();
        if(!view || !view.IsValid()){__result="";return false;}
        string text=__instance.m_name+"\nNo fuel required";
        if(__instance.m_canTurnOff)text+="\n[<color=yellow><b>$KEY_Use</b></color>] $piece_use";
        __result=Localization.instance.Localize(text);return false;
    }
}
[HarmonyPatch(typeof(Fireplace),nameof(Fireplace.Interact))]
static class LightInteract
{
    [HarmonyPriority(Priority.First)]
    static bool Prefix(Fireplace __instance,Humanoid user,bool hold,bool alt,ref bool __result)
    {
        if(!Plugin.Target(__instance))return true;
        __result=false;
        var view=__instance.GetComponent<ZNetView>();
        // Vanilla requires positive saved fuel before toggling. Infinite lights may have
        // zero saved fuel; preserve their native on/off RPC without inventing fuel units.
        if(!LightingPolicy.CanToggle(true,view && view.IsValid() && user,__instance.m_canTurnOff,hold,alt,
            PrivateArea.CheckAccess(__instance.transform.position,0f,false,true)))return false;
        if(!view.HasOwner())view.ClaimOwnership();
        view.InvokeRPC("RPC_ToggleOn");__result=true;return false;
    }
}
[HarmonyPatch(typeof(Fireplace),nameof(Fireplace.UseItem))]
static class NoLightRefills
{
    [HarmonyPriority(Priority.First)]
    static bool Prefix(Fireplace __instance,ref bool __result)
    {
        if(!Plugin.Target(__instance))return true;
        __result=false;return false;
    }
}
