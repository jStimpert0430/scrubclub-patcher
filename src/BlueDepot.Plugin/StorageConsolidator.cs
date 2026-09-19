using System.Linq;
using BlueDepot.Core;
using UnityEngine;

namespace BlueDepot;

internal sealed class StorageConsolidator:MonoBehaviour
{
    const string Job="BlueDepot.Consolidate";
    Container chest;ZNetView view;float next;
    void Awake()
    {
        chest=GetComponent<Container>();view=Storage.View(chest);
        view.Register<string>(Job,(sender,id)=>
        {
            if(!view.IsOwner() || id.Length>100)return;
            var depot=Storage.Find(id);
            if(depot && !ChestPrivacy.IsPrivate(chest) && !ChestPrivacy.IsPrivate(depot) && Plugin.IsDepot(depot) && Vector3.Distance(chest.transform.position,depot.transform.position)<=Plugin.Radius.Value)
                view.GetZDO().Set(Job,id);
        });
    }
    internal void Request(Container depot){if(!ChestPrivacy.IsPrivate(chest) && !ChestPrivacy.IsPrivate(depot))view.InvokeRPC(Job,Storage.Id(depot));}
    void Update()
    {
        if(Time.time<next)return;next=Time.time+.3f;
        if(!view || !view.IsValid() || !view.IsOwner() || Transfers.Busy || Crafting.Busy || DepotUi.BulkRunning || !Storage.CanAccess(chest) || ChestPrivacy.IsPrivate(chest) || chest.IsInUse())return;
        string id=view.GetZDO().GetString(Job,"");if(id.Length==0)return;
        var depot=Storage.Find(id);if(!depot || !Storage.CanAccess(depot) || ChestPrivacy.IsPrivate(depot))return;
        if(Vector3.Distance(Player.m_localPlayer.transform.position,depot.transform.position)>Plugin.Radius.Value)return;
        var controlled=new[]{depot}.Concat(Storage.Nearby(depot)).Where(c=>!c.IsInUse()).ToArray();
        if(!controlled.Contains(chest)){view.GetZDO().Set(Job,"");return;}
        var move=Consolidation.Next(controlled.Select(c=>Storage.Snapshot(c,depot)),Storage.Id(chest));
        if(move==null){view.GetZDO().Set(Job,"");return;}
        var item=chest.GetInventory().GetAllItems().FirstOrDefault(i=>Storage.Item(i,chest.GetInventory()).Slot==move.SourceSlot);
        if(item==null)return;
        var target=controlled.First(c=>Storage.Id(c)==move.Target.ChestId);
        var slot=Storage.Position(move.Target.Slot,target.GetInventory());
        if(target==chest)Transfers.MoveWithin(depot,chest,item,slot,move.Target.Amount,false);
        else Transfers.Deposit(chest,target,item,slot,move.Target.Amount);
    }
}
