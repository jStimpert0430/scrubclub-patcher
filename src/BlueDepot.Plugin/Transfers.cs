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
    internal static void Take(Container depot,Container source,ItemDrop.ItemData item)
    {
        if(Crafting.Busy)return;
        if(!Storage.ValidSession(depot) || !Storage.CanAccess(source) ||
            Vector3.Distance(source.transform.position,depot.transform.position)>Plugin.Radius.Value)return;
        var inv=Player.m_localPlayer.GetInventory();
        // Explicit free/compatible slot ensures MUC will not overwrite unrelated player items.
        var playerSnapshot=new Chest("player",inv.GetWidth()*inv.GetHeight(),0,true,inv.GetAllItems().ConvertAll(i=>Storage.Item(i,inv)));
        var target=Routing.Next(Storage.Item(item,source.GetInventory()),"chest",new[]{playerSnapshot});
        if(target==null){Status="Your inventory is full.";return;}
        Issue(()=>ContainerHandler.RemoveItemFromChest(source,item,inv,Storage.Position(target.Slot,inv),Player.m_localPlayer.GetZDOID(),target.Amount));
    }
}
[HarmonyPatch(typeof(InventoryHandler),nameof(InventoryHandler.RPC_RequestItemAddResponse),typeof(Inventory),typeof(RequestChestAddResponse))]
static class AddResponse {static void Postfix(RequestChestAddResponse response)=>Transfers.Reply(response.SourceID,response.Success,response.Amount);}
[HarmonyPatch(typeof(InventoryHandler),nameof(InventoryHandler.RPC_RequestItemRemoveResponse),typeof(Inventory),typeof(RequestChestRemoveResponse))]
static class RemoveResponse {static void Postfix(RequestChestRemoveResponse response)=>Transfers.Reply(response.SourceID,response.Success,response.Amount);}
