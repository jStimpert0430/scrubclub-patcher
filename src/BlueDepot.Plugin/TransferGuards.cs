using UnityEngine;
using System.Runtime.CompilerServices;
using BlueDepot.Core;
using HarmonyLib;
using MultiUserChest;

namespace BlueDepot;

// Add an optional trailer to OUR MUC requests. MUC's default remove protocol identifies
// a slot, not the item that occupied it. Owner-side identity checks prevent taking a
// replacement item when two players click the same stale row. All peers require this mod.
internal static class TransferGuards
{
    const string Marker="BlueDepot.v2";
    sealed class Expected { internal string Key;internal bool PrivateAccess;internal Expected(string key,bool privateAccess){Key=key;PrivateAccess=privateAccess;} }
    static readonly ConditionalWeakTable<object,Expected> guards=new ConditionalWeakTable<object,Expected>();
    internal static void Stamp(object request,ItemDrop.ItemData item)
    {if(Transfers.issuing && item!=null)guards.Add(request,new Expected(Storage.Key(item),Transfers.PrivateAccess));}
    internal static bool Read(object request,out string key)
    {if(guards.TryGetValue(request,out var guard)){key=guard.Key;return true;}key=null;return false;}
    internal static bool Permitted(object request,Inventory inventory)
    {
        if(!guards.TryGetValue(request,out var expected))return true; // ordinary manual chest UI
        return expected.PrivateAccess || !ContainerExtend.GetContainer(inventory,out var owner) || !ChestPrivacy.IsPrivate(owner.Container);
    }
    internal static void Write(object request,ZPackage package)
    {if(Read(request,out var key)){package.Write(Marker);package.Write(key);package.Write(guards.GetValue(request,_=>null).PrivateAccess);}}
    internal static void Load(object request,ZPackage package)
    {
        if(package.GetPos()>=package.Size())return;
        var old=package.GetPos();
        if(package.ReadString()==Marker)guards.Add(request,new Expected(package.ReadString(),package.ReadBool()));else package.SetPos(old);
    }
}
[HarmonyPatch(typeof(RequestChestAdd),MethodType.Constructor,new[]{typeof(Vector2i),typeof(int),typeof(ItemDrop.ItemData),typeof(Inventory),typeof(Inventory)})]
static class StampAdd {static void Postfix(RequestChestAdd __instance,ItemDrop.ItemData dragItem)=>TransferGuards.Stamp(__instance,dragItem);}
[HarmonyPatch(typeof(RequestChestRemove),MethodType.Constructor,new[]{typeof(Vector2i),typeof(Vector2i),typeof(int),typeof(ItemDrop.ItemData),typeof(Inventory),typeof(Inventory)})]
static class StampRemove {static void Postfix(RequestChestRemove __instance,Vector2i fromPos,Inventory sourceInventory)=>TransferGuards.Stamp(__instance,sourceInventory?.GetItemAt(fromPos.x,fromPos.y));}
[HarmonyPatch(typeof(RequestChestAdd),nameof(RequestChestAdd.WriteToPackage))]
static class WriteAdd {static void Postfix(RequestChestAdd __instance,ZPackage __result)=>TransferGuards.Write(__instance,__result);}
[HarmonyPatch(typeof(RequestChestRemove),nameof(RequestChestRemove.WriteToPackage))]
static class WriteRemove {static void Postfix(RequestChestRemove __instance,ZPackage __result)=>TransferGuards.Write(__instance,__result);}
[HarmonyPatch(typeof(RequestChestAdd),MethodType.Constructor,new[]{typeof(ZPackage)})]
static class ReadAdd {static void Postfix(RequestChestAdd __instance,ZPackage package)=>TransferGuards.Load(__instance,package);}
[HarmonyPatch(typeof(RequestChestRemove),MethodType.Constructor,new[]{typeof(ZPackage)})]
static class ReadRemove {static void Postfix(RequestChestRemove __instance,ZPackage package)=>TransferGuards.Load(__instance,package);}
[HarmonyPatch(typeof(RequestMove),MethodType.Constructor,new[]{typeof(ItemDrop.ItemData),typeof(Vector2i),typeof(int),typeof(Inventory)})]
static class StampMove {static void Postfix(RequestMove __instance,ItemDrop.ItemData itemToMove)=>TransferGuards.Stamp(__instance,itemToMove);}
[HarmonyPatch(typeof(RequestMove),nameof(RequestMove.WriteToPackage))]
static class WriteMove {static void Postfix(RequestMove __instance,ZPackage __result)=>TransferGuards.Write(__instance,__result);}
[HarmonyPatch(typeof(RequestMove),MethodType.Constructor,new[]{typeof(ZPackage)})]
static class ReadMove {static void Postfix(RequestMove __instance,ZPackage package)=>TransferGuards.Load(__instance,package);}
[HarmonyPatch(typeof(ContainerRPCHandler),nameof(ContainerRPCHandler.RequestItemMove))]
static class GuardMove
{
    static bool Prefix(Inventory inventory,RequestMove request,ref RequestMoveResponse __result)
    {
        if(!TransferGuards.Read(request,out var key))return true;
        var current=inventory.GetItemAt(request.fromPos.x,request.fromPos.y);
        var target=inventory.GetItemAt(request.toPos.x,request.toPos.y);
        if(TransferGuards.Permitted(request,inventory) && request.toPos.x>=0 && request.toPos.y>=0 && request.toPos.x<inventory.GetWidth() && request.toPos.y<inventory.GetHeight() &&
            current!=null && request.dragAmount<=current.m_stack &&
            TransferRules.CanRemove(key,Storage.Key(current),request.dragAmount) &&
            TransferRules.CanDeposit(key,target==null?null:Storage.Key(target),request.dragAmount))return true;
        __result=new RequestMoveResponse(request.RequestID,false,0);return false;
    }
}
[HarmonyPatch(typeof(ContainerRPCHandler),nameof(ContainerRPCHandler.RequestItemAdd))]
static class GuardAdd
{
    static bool Prefix(Inventory inventory,RequestChestAdd request,ref RequestChestAddResponse __result)
    {
        if(!PileStorage.Accepts(inventory,request.dragItem))
        {__result=new RequestChestAddResponse(request.RequestID,false,request.dragItem?.m_gridPos??Vector2i.zero,0,request.dragItem);return false;}
        if(!TransferGuards.Read(request,out var key))return true;
        var current=inventory.GetItemAt(request.toPos.x,request.toPos.y);
        if(TransferGuards.Permitted(request,inventory) && TransferRules.CanDeposit(key,current==null?null:Storage.Key(current),request.dragItem?.m_stack??0))return true;
        __result=new RequestChestAddResponse(request.RequestID,false,request.dragItem.m_gridPos,0,request.dragItem);return false;
    }
}
[HarmonyPatch(typeof(ContainerRPCHandler),nameof(ContainerRPCHandler.RequestItemRemove))]
static class GuardRemove
{
    static bool Prefix(Inventory inventory,RequestChestRemove request,ref RequestChestRemoveResponse __result)
    {
        // A vanilla drag can exchange a player item for a pile item. Piles
        // accept only their material; reject swaps before either side mutates.
        if(request.switchItem!=null && ContainerExtend.GetContainer(inventory,out var owner) && PileStorage.IsPile(owner.Container))
        {
            var at=inventory.GetItemAt(request.fromPos.x,request.fromPos.y);
            if(at==null || Storage.Key(at)!=Storage.Key(request.switchItem))
            {__result=new RequestChestRemoveResponse(request.RequestID,false,0,false,request.switchItem);return false;}
        }
        if(!TransferGuards.Read(request,out var key))return true;
        var current=inventory.GetItemAt(request.fromPos.x,request.fromPos.y);
        if(TransferGuards.Permitted(request,inventory) && TransferRules.CanRemove(key,current==null?null:Storage.Key(current),request.dragAmount))return true;
        __result=new RequestChestRemoveResponse(request.RequestID,false,0,false,null);return false;
    }
}
