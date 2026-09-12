using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using BlueDepot.Core;
using HarmonyLib;
using MultiUserChest;
using UnityEngine;
using Stack = BlueDepot.Core.Stack;

namespace BlueDepot;
internal static class Storage
{
    static readonly HashSet<Container> all=new HashSet<Container>();
    static readonly Func<Container,long,bool> checkAccess=AccessTools.MethodDelegate<Func<Container,long,bool>>(AccessTools.Method(typeof(Container),"CheckAccess"));
    internal static void Register(Container c){if(c)all.Add(c);}
    internal static void Unregister(Container c)=>all.Remove(c);
    internal static string Id(Container c)=>c.GetComponent<ZNetView>().GetZDO().m_uid.ToString();
    internal static bool CanAccess(Container c)
    {
        var player=Player.m_localPlayer;
        if(!c || !player)return false;
        var nv=c.GetComponent<ZNetView>();
        return nv && nv.IsValid() && nv.HasOwner() && c.GetInventory()!=null &&
            checkAccess(c,player.GetPlayerID()) && PrivateArea.CheckAccess(c.transform.position,0f,false,true);
    }
    internal static List<Container> Nearby(Container depot)
    {
        all.RemoveWhere(c=>!c);
        if(!CanAccess(depot))return new List<Container>();
        return all.Where(c=>c!=depot && !Plugin.IsDepot(c) && c.GetComponent<Piece>() &&
            !c.GetComponentInParent<Incinerator>() && !c.GetComponentInParent<Ship>() && !c.GetComponentInParent<Vagon>() &&
            Vector3.Distance(depot.transform.position,c.transform.position)<=Plugin.Radius.Value && CanAccess(c))
            .OrderBy(c=>Vector3.SqrMagnitude(c.transform.position-depot.transform.position)).ThenBy(Id,StringComparer.Ordinal).ToList();
    }
    internal static List<Container> CraftingChests()
    {
        all.RemoveWhere(c=>!c);
        var player=Player.m_localPlayer;
        if(!player)return new List<Container>();
        var direct=all.Where(c=>c.GetComponent<Piece>() && !c.IsInUse() &&
            !c.GetComponentInParent<Incinerator>() && !c.GetComponentInParent<Ship>() && !c.GetComponentInParent<Vagon>() &&
            Vector3.Distance(player.transform.position,c.transform.position)<=Plugin.Radius.Value && CanAccess(c)).ToList();
        return direct.Concat(direct.Where(Plugin.IsDepot).SelectMany(Nearby)).Where(c=>!c.IsInUse())
            .GroupBy(Id).Select(g=>g.First()).OrderBy(c=>Vector3.SqrMagnitude(c.transform.position-player.transform.position))
            .ThenBy(Id,StringComparer.Ordinal).ToList();
    }
    internal static Category CategoryOf(ItemDrop.ItemData i)
    {
        var s=i.m_shared;
        return Categories.Classify(s.m_itemType.ToString(),s.m_food>0 || s.m_foodStamina>0 || s.m_foodEitr>0,
            s.m_itemType==ItemDrop.ItemData.ItemType.Consumable && s.m_consumeStatusEffect && s.m_food<=0 && s.m_foodStamina<=0 && s.m_foodEitr<=0);
    }
    internal static string Key(ItemDrop.ItemData i)
    {
        // Length-prefixed fields prevent collisions from modded names/custom metadata delimiters.
        var values=new List<string>{i.m_dropPrefab ? i.m_dropPrefab.name : i.m_shared.m_name,
            i.m_quality.ToString(),i.m_variant.ToString(),i.m_worldLevel.ToString(),
            i.m_durability.ToString("R",CultureInfo.InvariantCulture),i.m_crafterID.ToString(),i.m_crafterName??""};
        if(i.m_customData!=null)foreach(var p in i.m_customData.OrderBy(p=>p.Key,StringComparer.Ordinal)){values.Add(p.Key);values.Add(p.Value);}
        return string.Concat(values.Select(v=>v.Length+":"+v));
    }
    internal static Stack Item(ItemDrop.ItemData i,Inventory inv)=>new Stack(
        (i.m_gridPos.y*inv.GetWidth()+i.m_gridPos.x).ToString(CultureInfo.InvariantCulture),Key(i),
        Localization.instance.Localize(i.m_shared.m_name),CategoryOf(i),i.m_stack,i.m_shared.m_maxStackSize);
    internal static Chest Snapshot(Container c,Container depot)
    {
        var inv=c.GetInventory();
        return new Chest(Id(c),inv.GetWidth()*inv.GetHeight(),Vector3.Distance(c.transform.position,depot.transform.position),CanAccess(c),
            inv.GetAllItems().Where(i=>i.m_stack>0).Select(i=>Item(i,inv)));
    }
    internal static Vector2i Position(string slot,Inventory inventory)
    {int n=int.Parse(slot,CultureInfo.InvariantCulture);return new Vector2i(n%inventory.GetWidth(),n/inventory.GetWidth());}
    internal static bool ValidSession(Container depot)=>CanAccess(depot) && !Player.m_localPlayer.IsDead() &&
        Vector3.Distance(Player.m_localPlayer.transform.position,depot.transform.position)<5f;
}

internal sealed class DepotSorter:MonoBehaviour
{
    Container chest;float next;int cursor;
    void Awake(){chest=GetComponent<Container>();}
    void Update()
    {
        if(Time.time<next)return;next=Time.time+1;
        if(Transfers.Busy || Crafting.Busy || !Storage.CanAccess(chest))return;
        if(!chest.GetComponent<ZNetView>().IsOwner() || chest.IsInUse())return;
        // Only a nearby player's active area is simulated. No unattended global scans.
        if(Vector3.Distance(Player.m_localPlayer.transform.position,chest.transform.position)>Plugin.Radius.Value)return;
        var inv=chest.GetInventory();var items=inv.GetAllItems().ToArray();if(items.Length==0)return;
        var candidates=Storage.Nearby(chest).Where(c=>!c.IsInUse()).ToArray();
        var snapshots=candidates.Select(c=>Storage.Snapshot(c,chest)).ToArray();
        // Rotate through at most eight source stacks per tick, so unroutable gear cannot starve materials.
        for(int n=0;n<Math.Min(8,items.Length);n++)
        {
            var item=items[cursor++%items.Length];
            if(InventoryBlock.Get(inv).IsSlotBlocked(item.m_gridPos))continue;
            var move=Routing.Next(Storage.Item(item,inv),Storage.Id(chest),snapshots);
            if(move==null)continue;
            var target=candidates.First(c=>Storage.Id(c)==move.ChestId);
            Transfers.Deposit(chest,target,item,Storage.Position(move.Slot,target.GetInventory()),move.Amount);
            break;
        }
    }
}
