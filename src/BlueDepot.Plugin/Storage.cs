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
    internal static ZNetView View(Container c)=>!c?null:c.m_rootObjectOverride?c.m_rootObjectOverride:c.GetComponent<ZNetView>();
    internal static Container Find(string id)=>all.FirstOrDefault(c=>c && View(c) && View(c).IsValid() && Id(c)==id);
    internal static string Id(Container c)=>View(c).GetZDO().m_uid.ToString();
    internal static bool IsCart(Container c)=>c && (c.m_wagon || c.GetComponentInParent<Vagon>() || (View(c) && View(c).GetComponent<Vagon>()));
    internal static bool Eligible(Container c)=>c && !ChestPrivacy.IsPrivate(c) &&
        !c.GetComponentInParent<TombStone>() && !(View(c) && View(c).GetComponent<TombStone>()) &&
        !c.GetComponentInParent<Incinerator>() && !c.GetComponentInParent<Ship>();
    internal static bool CanAccess(Container c)
    {
        var player=Player.m_localPlayer;
        if(!c || !player)return false;
        var nv=View(c);
        return nv && nv.IsValid() && nv.HasOwner() && c.GetInventory()!=null && PileStorage.Ready(c) &&
            checkAccess(c,player.GetPlayerID()) && PrivateArea.CheckAccess(c.transform.position,0f,false,true);
    }
    internal static List<Container> Nearby(Container depot)
    {
        all.RemoveWhere(c=>!c);
        // A private depot is a standalone chest, including while directly open.
        if(!CanAccess(depot) || ChestPrivacy.IsPrivate(depot))return new List<Container>();
        return all.Where(c=>c!=depot && Eligible(c) && !PileStorage.IsPile(c) &&
            Vector3.Distance(depot.transform.position,c.transform.position)<=Plugin.Radius.Value && CanAccess(c))
            .OrderBy(c=>Vector3.SqrMagnitude(c.transform.position-depot.transform.position)).ThenBy(Id,StringComparer.Ordinal).ToList();
    }
    internal static List<Container> CraftingChests()
    {
        all.RemoveWhere(c=>!c);
        var player=Player.m_localPlayer;
        if(!player)return new List<Container>();
        var network=WorkbenchReach.Connected(player.transform.position);
        var direct=all.Where(c=>Eligible(c) && !c.IsInUse() &&
            (Vector3.Distance(player.transform.position,c.transform.position)<=Plugin.Radius.Value || WorkbenchReach.Covers(network,c.transform.position)) && CanAccess(c)).ToList();
        return direct.Concat(direct.Where(Plugin.IsDepot).SelectMany(Nearby)).Where(c=>!c.IsInUse())
            .GroupBy(Id).Select(g=>g.First()).OrderBy(c=>Vector3.SqrMagnitude(c.transform.position-player.transform.position))
            .ThenBy(Id,StringComparer.Ordinal).ToList();
    }
    internal static Category CategoryOf(ItemDrop.ItemData i)
    {
        var s=i.m_shared;
        // Some trophies are also ingredients. Their trophy identity takes
        // precedence over inferred food-ingredient membership.
        if(Categories.IsTrophy(s.m_itemType.ToString(),i.m_dropPrefab?i.m_dropPrefab.name:null))return Category.Trophies;
        if(s.m_food>0 || s.m_foodStamina>0 || s.m_foodEitr>0 || s.m_itemType.ToString()=="Fish" || FoodIngredients.Contains(s.m_name))return Category.Food;
        return Categories.Classify(s.m_itemType.ToString(),s.m_food>0 || s.m_foodStamina>0 || s.m_foodEitr>0,
            s.m_itemType==ItemDrop.ItemData.ItemType.Consumable && s.m_consumeStatusEffect && s.m_food<=0 && s.m_foodStamina<=0 && s.m_foodEitr<=0);
    }
    static readonly HashSet<string> foodIngredients=new HashSet<string>();
    static ObjectDB foodDatabase;static int recipeCount=-1;
    static HashSet<string> FoodIngredients
    {
        get
        {
            var db=ObjectDB.instance;
            if(db && (foodDatabase!=db || recipeCount!=db.m_recipes.Count))
            {
                foodDatabase=db;recipeCount=db.m_recipes.Count;foodIngredients.Clear();
                foreach(var recipe in db.m_recipes)
                {
                    if(!recipe || !recipe.m_item)continue;
                    var output=recipe.m_item.m_itemData.m_shared;
                    if(output.m_food<=0 && output.m_foodStamina<=0 && output.m_foodEitr<=0)continue;
                    foreach(var requirement in recipe.m_resources)
                        if(requirement.m_resItem)foodIngredients.Add(requirement.m_resItem.m_itemData.m_shared.m_name);
                }
                if(ZNetScene.instance)foreach(var prefab in ZNetScene.instance.m_prefabs)
                {
                    var cooking=prefab.GetComponent<CookingStation>();
                    if(cooking)foreach(var conversion in cooking.m_conversion)
                        if(conversion.m_from)foodIngredients.Add(conversion.m_from.m_itemData.m_shared.m_name);
                }
            }
            return foodIngredients;
        }
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
        Localization.instance.Localize(i.m_shared.m_name),CategoryOf(i),i.m_stack,i.m_shared.m_maxStackSize,i.m_dropPrefab?i.m_dropPrefab.name:i.m_shared.m_name,!i.m_shared.m_teleportable);
    internal static Chest Snapshot(Container c,Container depot)
    {
        var inv=c.GetInventory();
        return new Chest(Id(c),inv.GetWidth()*inv.GetHeight(),Vector3.Distance(c.transform.position,depot.transform.position),CanAccess(c),
            inv.GetAllItems().Where(i=>i.m_stack>0).Select(i=>Item(i,inv)),ChestPrivacy.IsPrivate(c),IsCart(c));
    }
    internal static Vector2i Position(string slot,Inventory inventory)
    {int n=int.Parse(slot,CultureInfo.InvariantCulture);return new Vector2i(n%inventory.GetWidth(),n/inventory.GetWidth());}
    internal static bool ValidSession(Container depot)=>CanAccess(depot) && !Player.m_localPlayer.IsDead() &&
        Vector3.Distance(Player.m_localPlayer.transform.position,depot.transform.position)<5f;
}

internal sealed class DepotSorter:MonoBehaviour
{
    const string QueueKey="BlueDepot.SortQueue";
    const string SortRpc="BlueDepotSortIntake";
    Container chest;ZNetView view;float next;
    void Awake()
    {
        chest=GetComponent<Container>();view=GetComponent<ZNetView>();
        view.Register<string>(SortRpc,Receive);
    }
    internal void Request(Dictionary<int,string> slots)
    {
        if(!view || !view.IsValid() || !view.HasOwner() || ChestPrivacy.IsPrivate(chest))return;
        var data=BlueDepot.Core.IntakeQueue.Encode(slots);
        if(view.IsOwner())Receive(0,data);else view.InvokeRPC(SortRpc,data);
    }
    void Receive(long sender,string data)
    {
        if(!view.IsOwner() || ChestPrivacy.IsPrivate(chest))return;
        var queue=BlueDepot.Core.IntakeQueue.Decode(view.GetZDO().GetString(QueueKey,""));
        foreach(var pair in BlueDepot.Core.IntakeQueue.Decode(data))
        {
            var inv=chest.GetInventory();var current=inv.GetItemAt(pair.Key%inv.GetWidth(),pair.Key/inv.GetWidth());
            if(current!=null && Storage.Key(current)==pair.Value)queue[pair.Key]=pair.Value;
        }
        view.GetZDO().Set(QueueKey,BlueDepot.Core.IntakeQueue.Encode(queue));
    }
    void Update()
    {
        if(Time.time<next)return;next=Time.time+.25f;
        if(!view || !view.IsValid() || !view.IsOwner() || Transfers.Busy || Crafting.Busy || DepotUi.BulkRunning || !Storage.CanAccess(chest) || ChestPrivacy.IsPrivate(chest) || chest.IsInUse())return;
        if(Vector3.Distance(Player.m_localPlayer.transform.position,chest.transform.position)>Plugin.Radius.Value)return;
        var queue=BlueDepot.Core.IntakeQueue.Decode(view.GetZDO().GetString(QueueKey,""));
        if(queue.Count==0)return;
        var inv=chest.GetInventory();
        var candidates=Storage.Nearby(chest).Where(c=>!c.IsInUse()).ToArray();
        var snapshots=candidates.Select(c=>Storage.Snapshot(c,chest)).ToArray();
        foreach(var pair in queue.ToArray())
        {
            var item=inv.GetItemAt(pair.Key%inv.GetWidth(),pair.Key/inv.GetWidth());
            if(item==null || Storage.Key(item)!=pair.Value){queue.Remove(pair.Key);continue;}
            if(InventoryBlock.Get(inv).IsSlotBlocked(item.m_gridPos))continue;
            var move=Routing.Next(Storage.Item(item,inv),Storage.Id(chest),snapshots);
            if(move==null){queue.Remove(pair.Key);continue;} // overflow remains safely in Internal
            var target=candidates.First(c=>Storage.Id(c)==move.ChestId);
            Transfers.Deposit(chest,target,item,Storage.Position(move.Slot,target.GetInventory()),move.Amount);
            break;
        }
        view.GetZDO().Set(QueueKey,BlueDepot.Core.IntakeQueue.Encode(queue));
    }
}
