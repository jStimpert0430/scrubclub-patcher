using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MultiUserChest;
using UnityEngine;

namespace BlueDepot;

internal sealed partial class DepotUi
{
    readonly Dictionary<int,string> intake=new Dictionary<int,string>();
    BlueDepot.Core.Chest InternalSnapshot()
    {
        var snapshot=Storage.Snapshot(depot,depot);
        var queued=BlueDepot.Core.IntakeQueue.Decode(depot.GetComponent<ZNetView>().GetZDO().GetString("BlueDepot.SortQueue",""));
        return new BlueDepot.Core.Chest(snapshot.Id,snapshot.Capacity,snapshot.Distance,snapshot.Accessible,
            snapshot.Items.Select(i=>intake.ContainsKey(int.Parse(i.Slot)) || queued.ContainsKey(int.Parse(i.Slot))
                ? new BlueDepot.Core.Stack(i.Slot,"reserved:"+i.Key,i.Name,i.Category,i.Count,i.Maximum,i.ItemId,i.PreferCart):i));
    }
    bool IntakeContains(string slot,string key)=>int.TryParse(slot,out var n) && intake.TryGetValue(n,out var expected) && key==expected;
    bool DepositIntake(ItemDrop.ItemData item,int amount)
    {
        var inventory=depot.GetInventory();
        // Never merge intake with pre-existing Internal items: those must not be
        // accidentally included in this session's close-and-sort operation.
        for(int n=0;n<inventory.GetWidth()*inventory.GetHeight();n++)
        {
            var pos=new Vector2i(n%inventory.GetWidth(),n/inventory.GetWidth());
            if(inventory.GetItemAt(pos.x,pos.y)!=null || InventoryBlock.Get(inventory).IsSlotBlocked(pos))continue;
            intake[n]=Storage.Key(item);
            Transfers.Deposit(null,depot,item,pos,Math.Min(amount,item.m_stack),true);
            return true;
        }
        Transfers.Status="Internal storage is full. Withdraw items to make room for intake.";
        return false;
    }
    void FlushIntake()
    {
        if(depot && intake.Count>0)
        {
            var chest=depot;var slots=new Dictionary<int,string>(intake);
            Plugin.Instance.StartCoroutine(SortAfterReceipt(chest,slots));
        }
        intake.Clear();
    }
    static System.Collections.IEnumerator SortAfterReceipt(Container chest,Dictionary<int,string> slots)
    {
        // Closing must not abandon a deposit which is still awaiting its owner.
        while(chest && Transfers.Busy)yield return null;
        if(chest)chest.GetComponent<DepotSorter>()?.Request(slots);
    }
}
