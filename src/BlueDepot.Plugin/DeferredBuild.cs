using System;
using BlueDepot.Core;
using HarmonyLib;
using UnityEngine;

namespace BlueDepot;

internal static class DeferredBuild
{
    static readonly Func<Humanoid,ItemDrop.ItemData> rightItem=AccessTools.MethodDelegate<Func<Humanoid,ItemDrop.ItemData>>(AccessTools.Method(typeof(Humanoid),"GetRightItem"));
    sealed class Pending
    {
        internal Player Player;
        internal Piece Piece;
        internal ItemDrop.ItemData Tool;
        internal Vector3 Position;
        internal Quaternion Rotation;
        internal float Deadline;
        internal readonly DeferredIntent Intent=new DeferredIntent();
        internal bool Valid()
        {
            if(!Player || Player!=Player.m_localPlayer || Player.IsDead() || !Player.InPlaceMode() ||
                Player.GetSelectedPiece()!=Piece || rightItem(Player)!=Tool || Hud.IsPieceSelectionVisible())return false;
            AccessTools.Method(typeof(Player),"UpdatePlacementGhost").Invoke(Player,new object[]{false});
            var ghost=Traverse.Create(Player).Field("m_placementGhost").GetValue<GameObject>();
            return ghost && Vector3.Distance(ghost.transform.position,Position)<0.25f && Quaternion.Angle(ghost.transform.rotation,Rotation)<5f;
        }
    }
    static Pending pending;
    internal static void Gather(Player player,Piece piece,Ingredient[] plan)
    {
        if(plan==null)return;
        AccessTools.Method(typeof(Player),"UpdatePlacementGhost").Invoke(player,new object[]{false});
        var ghost=Traverse.Create(player).Field("m_placementGhost").GetValue<GameObject>();
        if(!ghost)return;
        var request=new Pending{Player=player,Piece=piece,Tool=rightItem(player),Position=ghost.transform.position,Rotation=ghost.transform.rotation};
        Crafting.Begin(plan,request.Valid,()=>{request.Deadline=Time.realtimeSinceStartup+1;pending=request;});
    }
    internal static bool Resume(Player player,bool takeInput,ref float pressed)
    {
        if(pending==null || pending.Player!=player)return false;
        var request=pending;pending=null;
        if(!request.Intent.TryConsume(takeInput && Time.realtimeSinceStartup<=request.Deadline && request.Valid()))return false;
        // Feed the normal placement loop once. It owns resource consumption, tool
        // durability, stamina, statistics, animations and fresh placement validation.
        pressed=Time.time;
        return true;
    }
}

[HarmonyPatch(typeof(Player),"UpdatePlacement")]
static class ResumeDeferredBuild
{
    static void Prefix(Player __instance,bool takeInput,ref float ___m_placePressedTime,out bool __state)
        =>__state=DeferredBuild.Resume(__instance,takeInput,ref ___m_placePressedTime);
    static void Finalizer(bool __state,ref float ___m_placePressedTime)
    {if(__state)___m_placePressedTime=-9999f;}
}
