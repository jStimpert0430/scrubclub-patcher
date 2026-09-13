using System;
using System.Collections.Generic;
using System.Linq;
using BlueDepot.Core;
using HarmonyLib;
using MultiUserChest;
using UnityEngine;

namespace BlueDepot;

internal static class StationSupplies
{
    internal static bool Replaying;
    internal static T Call<T>(object station,string method,params object[] args)
        =>(T)AccessTools.Method(station.GetType(),method).Invoke(station,args);
    static bool Valid(Component station,Humanoid user)
    {
        var player=Player.m_localPlayer;
        if(!station || !player || user!=player || player.IsDead())return false;
        var nv=station.GetComponent<ZNetView>();
        return nv && nv.IsValid() && Vector3.Distance(player.transform.position,station.transform.position)<=player.m_maxInteractDistance &&
            PrivateArea.CheckAccess(station.transform.position,0f,false,true);
    }
    // true means run vanilla; false means one owner-confirmed refill is pending.
    internal static bool Supply(Component station,Humanoid user,ItemDrop.ItemData explicitItem,
        IEnumerable<ItemDrop> inputs,Func<bool> hasCapacity,Action resume,ref bool result)
    {
        if(Replaying || !Crafting.Enabled || !Plugin.SupplyStations.Value || user!=Player.m_localPlayer)return true;
        var inventory=user.GetInventory();
        var allowed=inputs.Where(i=>i).ToArray();
        bool carried=allowed.Any(i=>inventory.HaveItem(i.m_itemData.m_shared.m_name));
        bool inventoryBlocked=InventoryBlock.Get(inventory).IsAnySlotBlocked();
        // A chest transfer pause must not block ordinary carried-fuel interactions.
        // Preserve MUC's inventory locks: no bypass for slots involved in another transfer.
        if(StationSupplyRules.UseCarriedFirst(carried || explicitItem!=null,inventoryBlocked))return true;
        if(Crafting.Busy || Transfers.Busy || inventoryBlocked){result=false;return false;}
        var selected=allowed.FirstOrDefault(i=>Crafting.Count(i.m_itemData.m_shared.m_name,-1,true)>0);
        if(!StationSupplyRules.ShouldFetch(Valid(station,user),hasCapacity(),carried,selected,explicitItem!=null,false))return true;
        var plan=new[]{new Ingredient(selected.m_itemData.m_shared.m_name,1)};
        var intent=new DeferredIntent();
        Crafting.Begin(plan,()=>Valid(station,user) && hasCapacity(),()=>
        {
            if(!intent.TryConsume(Valid(station,user) && hasCapacity()))return;
            Replaying=true;
            try{resume();}finally{Replaying=false;}
        });
        result=true;
        return false;
    }
}

[HarmonyPatch(typeof(Fireplace),nameof(Fireplace.Interact))]
static class SupplyFireplace
{
    static bool Prefix(Fireplace __instance,Humanoid user,bool hold,bool alt,float ___m_lastUseTime,ref bool __result)
    {
        var fire=__instance;
        var nv=fire.GetComponent<ZNetView>();
        if(!nv || !nv.IsValid() || !fire.m_canRefill || fire.m_infiniteFuel || !fire.m_fuelItem)return true;
        float fuel=nv.GetZDO().GetFloat(ZDOVars.s_fuel);
        // Preserve toggle interactions and vanilla's held-button cadence.
        if((fire.m_canTurnOff && !hold && !alt && fuel>0) ||
            (hold && (fire.m_holdRepeatInterval<=0 || Time.time-___m_lastUseTime<fire.m_holdRepeatInterval)))return true;
        return StationSupplies.Supply(fire,user,null,new[]{fire.m_fuelItem},
            ()=>Mathf.CeilToInt(nv.GetZDO().GetFloat(ZDOVars.s_fuel))<fire.m_maxFuel,
            ()=>fire.Interact(user,hold,alt),ref __result);
    }
}
[HarmonyPatch(typeof(Smelter),"OnAddFuel")]
static class SupplySmelterFuel
{
    static bool Prefix(Smelter __instance,Switch sw,Humanoid user,ItemDrop.ItemData item,ref bool __result)
    {
        var station=__instance;
        return StationSupplies.Supply(station,user,item,new[]{station.m_fuelItem},
            ()=>StationSupplies.Call<float>(station,"GetFuel")<=station.m_maxFuel-1,
            ()=>StationSupplies.Call<bool>(station,"OnAddFuel",sw,user,null),ref __result);
    }
}
[HarmonyPatch(typeof(Smelter),"OnAddOre")]
static class SupplySmelterInput
{
    static bool Prefix(Smelter __instance,Switch sw,Humanoid user,ItemDrop.ItemData item,ref bool __result)
    {
        var station=__instance;
        return StationSupplies.Supply(station,user,item,station.m_conversion.Select(c=>c.m_from),
            ()=>StationSupplies.Call<int>(station,"GetQueueSize")<station.m_maxOre,
            ()=>StationSupplies.Call<bool>(station,"OnAddOre",sw,user,null),ref __result);
    }
}
[HarmonyPatch(typeof(CookingStation),"OnAddFuelSwitch")]
static class SupplyCookingFuel
{
    static bool Prefix(CookingStation __instance,Switch sw,Humanoid user,ItemDrop.ItemData item,ref bool __result)
    {
        var station=__instance;
        return StationSupplies.Supply(station,user,item,new[]{station.m_fuelItem},
            ()=>StationSupplies.Call<float>(station,"GetFuel")<=station.m_maxFuel-1,
            ()=>StationSupplies.Call<bool>(station,"OnAddFuelSwitch",sw,user,null),ref __result);
    }
}
[HarmonyPatch(typeof(CookingStation),"OnInteract")]
static class SupplyCookingInput
{
    static bool Prefix(CookingStation __instance,Humanoid user,ref bool __result)
    {
        var station=__instance;
        if(StationSupplies.Call<bool>(station,"HaveDoneItem"))return true;
        return StationSupplies.Supply(station,user,null,station.m_conversion.Select(c=>c.m_from),
            ()=>StationSupplies.Call<int>(station,"GetFreeSlot")>=0 && (!station.m_requireFire || StationSupplies.Call<bool>(station,"IsFireLit")),
            ()=>StationSupplies.Call<bool>(station,"OnInteract",user),ref __result);
    }
}
