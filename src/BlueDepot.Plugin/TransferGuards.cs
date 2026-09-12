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
    const string Marker="BlueDepot.v1";
    sealed class Expected { internal string Key;internal Expected(string key){Key=key;} }
    static readonly ConditionalWeakTable<object,Expected> guards=new ConditionalWeakTable<object,Expected>();
    internal static void Stamp(object request,ItemDrop.ItemData item)
    {if(Transfers.issuing && item!=null)guards.Add(request,new Expected(Storage.Key(item)));}
    internal static bool Read(object request,out string key)
    {if(guards.TryGetValue(request,out var guard)){key=guard.Key;return true;}key=null;return false;}
    internal static void Write(object request,ZPackage package)
    {if(Read(request,out var key)){package.Write(Marker);package.Write(key);}}
    internal static void Load(object request,ZPackage package)
    {
        if(package.GetPos()>=package.Size())return;
        var old=package.GetPos();
        if(package.ReadString()==Marker)guards.Add(request,new Expected(package.ReadString()));else package.SetPos(old);
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
[HarmonyPatch(typeof(ContainerRPCHandler),nameof(ContainerRPCHandler.RequestItemAdd))]
static class GuardAdd
{
    static bool Prefix(Inventory inventory,RequestChestAdd request,ref RequestChestAddResponse __result)
    {
        if(!TransferGuards.Read(request,out var key))return true;
        var current=inventory.GetItemAt(request.toPos.x,request.toPos.y);
        if(TransferRules.CanDeposit(key,current==null?null:Storage.Key(current),request.dragItem?.m_stack??0))return true;
        __result=new RequestChestAddResponse(request.RequestID,false,request.dragItem.m_gridPos,0,request.dragItem);return false;
    }
}
[HarmonyPatch(typeof(ContainerRPCHandler),nameof(ContainerRPCHandler.RequestItemRemove))]
static class GuardRemove
{
    static bool Prefix(Inventory inventory,RequestChestRemove request,ref RequestChestRemoveResponse __result)
    {
        if(!TransferGuards.Read(request,out var key))return true;
        var current=inventory.GetItemAt(request.fromPos.x,request.fromPos.y);
        if(TransferRules.CanRemove(key,current==null?null:Storage.Key(current),request.dragAmount))return true;
        __result=new RequestChestRemoveResponse(request.RequestID,false,0,false,null);return false;
    }
}
