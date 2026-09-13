using System;
using BlueDepot.Core;
using HarmonyLib;
using MultiUserChest;
using UnityEngine;
namespace BlueDepot;

internal static class Transfers
{
    static readonly TransferGate gate=new TransferGate();
    internal static bool issuing;static float issuedAt;
    internal static string Status="";
    internal static int ReplySerial;
    internal static bool LastSuccess;
    internal static bool Busy
    {
        get{
            if(gate.Pending.HasValue && Time.realtimeSinceStartup-issuedAt>15){gate.Timeout();Status="Transfer awaiting confirmation. Sorting paused; do not retry.";}
            return issuing || gate.Busy;
        }
    }
    // MUC already handles synchronous local-owner responses. Only remote requests need a gate.
    static void Issue(Func<IRequest> send)
    {
        if(Busy)return;
        issuing=true;Status="Transferring…";
        try
        {
            var request=send();
            if(request!=null && request.RequestID!=0){gate.Begin(request.RequestID);issuedAt=Time.realtimeSinceStartup;}
            else if(request!=null)Status="Inventory changed or has no room. Refresh and try again.";
        }
        catch(Exception e){gate.Fault();Status="Transfer error; inspect the game log before retrying.";Plugin.Instance.LoggerForTransfers(e);}
        finally{issuing=false;}
    }
    internal static void Reply(int id,bool success,int amount)
    {
        if(issuing || gate.Complete(id)){ReplySerial++;LastSuccess=success && amount>0;Status=success?$"Moved {amount} item(s).":"Transfer declined; inventory changed.";}
    }
    internal static bool PullIngredient(Container source,ItemDrop.ItemData item,int amount)
    {
        if(Busy || !Storage.CraftingChests().Contains(source))return false;
        var inv=Player.m_localPlayer.GetInventory();
        if(InventoryBlock.Get(inv).IsAnySlotBlocked() || InventoryBlock.Get(source.GetInventory()).IsSlotBlocked(item.m_gridPos))return false;
        var snapshot=new Chest("player",inv.GetWidth()*inv.GetHeight(),0,true,inv.GetAllItems().ConvertAll(i=>Storage.Item(i,inv)));
        var target=Routing.Next(Storage.Item(item,source.GetInventory()),"chest",new[]{snapshot});
        if(target==null)return false;
        Issue(()=>ContainerHandler.RemoveItemFromChest(source,item,inv,Storage.Position(target.Slot,inv),Player.m_localPlayer.GetZDOID(),Math.Min(amount,target.Amount)));
        return true;
    }
    internal static void Deposit(Container source,Container target,ItemDrop.ItemData item,Vector2i slot,int amount)
    {
        if(Crafting.Busy)return;
        if(!Storage.CanAccess(target) || target.IsInUse())return;
        Inventory inv;
        if(source){if(!Storage.CanAccess(source) || !source.GetComponent<ZNetView>().IsOwner())return;inv=source.GetInventory();}
        else inv=Player.m_localPlayer.GetInventory();
        if(!inv.ContainsItem(item) || InventoryBlock.Get(inv).IsSlotBlocked(item.m_gridPos))return;
        var at=target.GetInventory().GetItemAt(slot.x,slot.y);
        // Refuse replacement/swap semantics: this UI only ever deposits into compatible space.
        if(at!=null && Storage.Key(at)!=Storage.Key(item))return;
        Issue(()=>ContainerHandler.AddItemToChest(target,item,inv,slot,Player.m_localPlayer.GetZDOID(),amount));
    }
    internal static bool Take(Container depot,Container source,ItemDrop.ItemData item,int amount=int.MaxValue,Vector2i? destination=null)
    {
        if(Crafting.Busy || Busy || item==null || item.m_shared.m_questItem || amount<=0)return false;
        if(!Storage.ValidSession(depot) || !Storage.CanAccess(source) ||
            (source!=depot && !Storage.Nearby(depot).Contains(source)))return false;
        var sourceInventory=source.GetInventory();
        if(!sourceInventory.ContainsItem(item) || InventoryBlock.Get(sourceInventory).IsSlotBlocked(item.m_gridPos))return false;
        var inv=Player.m_localPlayer.GetInventory();
        Vector2i slot;int capacity;
        if(destination.HasValue)
        {
            slot=destination.Value;
            if(slot.x<0 || slot.y<0 || slot.x>=inv.GetWidth() || slot.y>=inv.GetHeight())return false;
            var at=inv.GetItemAt(slot.x,slot.y);
            if(at!=null && Storage.Key(at)!=Storage.Key(item)){Status="Choose an empty or compatible inventory slot.";return false;}
            capacity=at==null?item.m_shared.m_maxStackSize:at.m_shared.m_maxStackSize-at.m_stack;
        }
        else
        {
            var playerSnapshot=new Chest("player",inv.GetWidth()*inv.GetHeight(),0,true,inv.GetAllItems().ConvertAll(i=>Storage.Item(i,inv)));
            var target=Routing.Next(Storage.Item(item,sourceInventory),"chest",new[]{playerSnapshot});
            if(target==null){Status="Your inventory is full.";return false;}
            slot=Storage.Position(target.Slot,inv);capacity=target.Amount;
        }
        if(capacity<=0 || InventoryBlock.Get(inv).IsSlotBlocked(slot))return false;
        int moved=GridRules.MoveAmount(amount,item.m_stack,capacity);
        Issue(()=>ContainerHandler.RemoveItemFromChest(source,item,inv,slot,Player.m_localPlayer.GetZDOID(),moved));
        return true;
    }
    internal static bool MoveWithin(Container depot,Container source,ItemDrop.ItemData item,Vector2i slot,int amount,bool requireOpenSession=true)
    {
        if(Busy || Crafting.Busy || !(requireOpenSession?Storage.ValidSession(depot):Storage.CanAccess(depot)) || !Storage.CanAccess(source) ||
            (source!=depot && !Storage.Nearby(depot).Contains(source)) || item.m_shared.m_questItem)return false;
        var inv=source.GetInventory();
        if(!inv.ContainsItem(item) || slot==item.m_gridPos || slot.x<0 || slot.y<0 || slot.x>=inv.GetWidth() || slot.y>=inv.GetHeight() ||
            InventoryBlock.Get(inv).IsSlotBlocked(item.m_gridPos) || InventoryBlock.Get(inv).IsSlotBlocked(slot))return false;
        var at=inv.GetItemAt(slot.x,slot.y);
        if(at!=null && Storage.Key(at)!=Storage.Key(item))return false;
        int moved=GridRules.MoveAmount(amount,item.m_stack,at==null?item.m_shared.m_maxStackSize:at.m_shared.m_maxStackSize-at.m_stack);
        if(moved<=0)return false;
        Issue(()=>
        {
            var request=new RequestMove(item,slot,moved,inv);
            InventoryPreview.AddPackage(request);
            MultiUserChest.Patches.GamePatches.InvokeRPC(source.GetComponent<ZNetView>(),MultiUserChest.Patches.ContainerPatch.ItemMoveRPC,request);
            return request;
        });
        return true;
    }

}
[HarmonyPatch(typeof(InventoryHandler),nameof(InventoryHandler.RPC_RequestItemAddResponse),typeof(Inventory),typeof(RequestChestAddResponse))]
static class AddResponse {static void Postfix(RequestChestAddResponse response)=>Transfers.Reply(response.SourceID,response.Success,response.Amount);}
[HarmonyPatch(typeof(InventoryHandler),nameof(InventoryHandler.RPC_RequestItemRemoveResponse),typeof(Inventory),typeof(RequestChestRemoveResponse))]
static class RemoveResponse {static void Postfix(RequestChestRemoveResponse response)=>Transfers.Reply(response.SourceID,response.Success,response.Amount);}
[HarmonyPatch(typeof(InventoryHandler),nameof(InventoryHandler.RPC_RequestItemMoveResponse),typeof(RequestMoveResponse))]
static class MoveResponse {static void Postfix(RequestMoveResponse response)=>Transfers.Reply(response.SourceID,response.Success,response.Amount);}
