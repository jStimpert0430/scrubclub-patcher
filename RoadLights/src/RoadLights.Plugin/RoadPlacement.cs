using System;
using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Managers;
using RoadLights.Core;
using UnityEngine;
namespace RoadLights;

static class RoadPlacement
{
    internal static readonly int FreeKey="scrubclub_roadlights_free".GetStableHashCode();
    static readonly List<Piece> nearby=new List<Piece>();
    static readonly AccessTools.FieldRef<Humanoid,ItemDrop.ItemData> rightItem=AccessTools.FieldRefAccess<Humanoid,ItemDrop.ItemData>("m_rightItem");
    internal static bool HoldingHoe(Player player)
    {
        var tool=player?rightItem(player):null;
        return tool?.m_dropPrefab && Utils.GetPrefabName(tool.m_dropPrefab)=="Hoe";
    }
    internal static Piece Recipe(string name)
        =>RoadPolicy.Freestanding(name)?PrefabManager.Instance.GetPrefab(name)?.GetComponent<Piece>():null;
    internal static bool Known(Player player,Piece piece)
        =>player && piece && piece.m_enabled && player.IsRecipeKnown(piece.m_name)
          && (string.IsNullOrEmpty(piece.m_dlc)||DLCMan.instance.IsDLCInstalled(piece.m_dlc));
    static bool HasLight(Vector3 point)
    {
        nearby.Clear();Piece.GetAllPiecesInRadius(point,RoadPolicy.Radius+0.01f,nearby);
        foreach(var p in nearby)
        {
            var view=p.GetComponent<ZNetView>();
            Vector3 delta=p.transform.position-point;
            if(view && view.IsValid() && Plugin.Target(p) && RoadPolicy.WithinRadius(delta.x,delta.y,delta.z))return true;
        }
        return false;
    }
    internal static void AfterPath(Player player,Piece path,Vector3 center)
    {
        if(!player || player!=Player.m_localPlayer || !path || Utils.GetPrefabName(path.gameObject)!="path_v2")return;
        if(!HoldingHoe(player))return;
        string selected=Plugin.RoadSelection.Value;
        Piece light=Recipe(selected);
        if(!RoadPolicy.ShouldPlace("path_v2",true,Known(player,light),selected!="",HasLight(center)))return;
        Vector3 forward=center-player.transform.position;forward.y=0;
        if(forward.sqrMagnitude<0.01f)forward=player.transform.forward;
        bool left=UnityEngine.Random.value<0.5f;
        for(int side=0;side<2;side++)
        {
            var offset=RoadPolicy.Side(forward.x,forward.z,side==0?left:!left);
            Vector3 candidate=center+new Vector3(offset.x,0,offset.z);
            if(!FindSupport(selected,light,center,candidate,out var pos,out var rot))continue;
            if(Location.IsInsideNoBuildLocation(pos) || !PrivateArea.CheckAccess(pos,0f,false,true) || HasLight(pos))continue;
            // This overload performs native placement/ownership/effects but does not
            // consume recipe resources (consumption belongs to the normal input path).
            // Tag the instance when native placement assigns its creator.
            Generating=true;
            try{player.PlacePiece(light,pos,rot,false,false);}
            finally{Generating=false;}
            return;
        }
    }
    internal static bool Generating;
    static bool FindSupport(string name,Piece light,Vector3 center,Vector3 candidate,out Vector3 pos,out Quaternion rot)
    {
        pos=default;rot=Quaternion.LookRotation((center-candidate).normalized,Vector3.up);
        int solid=LayerMask.GetMask("Default","static_solid","Default_small","piece","terrain");
        RaycastHit hit;
        if(!RoadPolicy.Freestanding(name))return false;
        if(!Physics.Raycast(candidate+Vector3.up*3f,Vector3.down,out hit,6f,solid,QueryTriggerInteraction.Ignore) || hit.normal.y<0.75f)return false;
        pos=hit.point;
        if(Physics.CheckSphere(pos+Vector3.up*0.6f,0.3f,LayerMask.GetMask("piece","static_solid"),QueryTriggerInteraction.Ignore))return false;
        if(light.m_onlyInBiome!=Heightmap.Biome.None && (Heightmap.FindBiome(pos)&light.m_onlyInBiome)==0)return false;
        // Do not create road lights below the water surface.
        if(pos.y<ZoneSystem.instance.m_waterLevel)return false;
        return true;
    }
}

[HarmonyPatch(typeof(Player),nameof(Player.PlacePiece))]
static class RoadAfterPlacement
{
    static void Postfix(Player __instance,Piece piece,Vector3 pos)
    {
        try{RoadPlacement.AfterPath(__instance,piece,pos);}
        catch(Exception error){Plugin.Log.LogError("Road lighting skipped: "+error);}
    }
}
[HarmonyPatch(typeof(Piece),nameof(Piece.SetCreator))]
static class MarkFreeRoadLight
{
    static void Postfix(Piece __instance)
    {
        if(!RoadPlacement.Generating || !Plugin.Target(__instance))return;
        var view=__instance.GetComponent<ZNetView>();
        if(view && view.IsValid() && view.IsOwner())view.GetZDO().Set(RoadPlacement.FreeKey,true);
    }
}
[HarmonyPatch(typeof(Piece),nameof(Piece.DropResources))]
static class FreeRoadLightDrops
{
    static bool Prefix(Piece __instance)
    {
        if(!Plugin.Target(__instance))return true;
        var view=__instance.GetComponent<ZNetView>();
        return !view || !view.IsValid() || !view.GetZDO().GetBool(RoadPlacement.FreeKey,false);
    }
}
