using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace BlueDepot;

[BepInPlugin(Guid, "Blue Depot", "0.1.3")]
[BepInDependency(Jotunn.Main.ModGuid)]
[BepInDependency("com.maxsch.valheim.MultiUserChest")]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Guid="scrubclub.bluedepot";
    internal const string Prefab="BlueDepot_Chest";
    internal static ConfigEntry<float> Radius;
    internal static ConfigEntry<bool> CraftFromChests;
    internal static Plugin Instance;
    Harmony harmony;
    void Awake()
    {
        Instance=this;
        CraftFromChests=Config.Bind("Crafting","Enabled",true,new ConfigDescription("Craft, upgrade and build using accessible nearby storage.",null,new ConfigurationManagerAttributes{IsAdminOnly=true}));
        Radius=Config.Bind("Storage","Radius",20f,new ConfigDescription("Storage radius in metres.",new AcceptableValueRange<float>(5,50),new ConfigurationManagerAttributes{IsAdminOnly=true}));
        harmony=new Harmony(Guid);harmony.PatchAll();
        PrefabManager.OnVanillaPrefabsAvailable+=Register;
        gameObject.AddComponent<DepotUi>();
    }
    internal void LoggerForTransfers(System.Exception error)=>Logger.LogError(error);
    void OnDestroy(){PrefabManager.OnVanillaPrefabsAvailable-=Register;harmony?.UnpatchSelf();}
    void Register()
    {
        PrefabManager.OnVanillaPrefabsAvailable-=Register;
        var config=new PieceConfig{Name="Blue Depot",Description="100-slot drop box and shared access to nearby storage.",PieceTable="Hammer",Category="Furniture",CraftingStation="piece_workbench"};
        config.AddRequirement(new RequirementConfig("Wood",20,0,true));
        config.AddRequirement(new RequirementConfig("Blueberries",5,0,true));
        var piece=new CustomPiece(Prefab,"piece_chest_wood",config);
        var c=piece.PiecePrefab.GetComponent<Container>();
        c.m_width=10;c.m_height=10;c.m_name="Blue Depot";
        // Instantiate materials: never tint the shared vanilla material asset.
        foreach(var r in piece.PiecePrefab.GetComponentsInChildren<Renderer>(true))
        foreach(var mat in r.materials)
        {
            if(mat.HasProperty("_Color"))mat.SetColor("_Color",new Color(0.22f,0.48f,1f));
            if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",new Color(0.22f,0.48f,1f));
        }
        if(!PieceManager.Instance.AddPiece(piece))throw new System.InvalidOperationException("Blue Depot prefab registration failed");
        Logger.LogInfo("Blue Depot registered; 100 native slots");
    }
    internal static bool IsDepot(Container c) => c && c.GetComponent<ZNetView>() is ZNetView nv && nv.IsValid() && nv.GetZDO().GetPrefab()==Prefab.GetStableHashCode();
}

[HarmonyPatch(typeof(Container),"Awake")]
static class ChestAwake
{
    static void Postfix(Container __instance)
    {
        Storage.Register(__instance);
        if(Plugin.IsDepot(__instance) && !__instance.GetComponent<DepotSorter>())__instance.gameObject.AddComponent<DepotSorter>();
    }
}
[HarmonyPatch(typeof(Container),"OnDestroyed")]
static class ChestDestroy { static void Prefix(Container __instance)=>Storage.Unregister(__instance); }
[HarmonyPatch(typeof(Container),"Interact")]
static class ChestInteract
{
    [HarmonyPriority(Priority.First)]
    static bool Prefix(Container __instance,Humanoid character,bool hold,ref bool __result)
    {
        if(!Plugin.IsDepot(__instance))return true;
        __result=false;
        if(hold || character!=Player.m_localPlayer || !Storage.CanAccess(__instance))return false;
        DepotUi.Open(__instance);__result=true;return false;
    }
}
