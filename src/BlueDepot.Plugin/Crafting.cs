using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BlueDepot.Core;
using HarmonyLib;
using MultiUserChest;
using UnityEngine;

namespace BlueDepot;

internal static class Crafting
{
    internal static int CountScope;
    internal static readonly ExecutionWindow Execution=new ExecutionWindow();
    internal static bool Executing=>Execution.Active;
    internal static void RefreshRecipes(InventoryGui gui)
    {
        if(gui && Player.m_localPlayer && InventoryGui.IsVisible())
            AccessTools.Method(typeof(InventoryGui),"UpdateCraftingPanel").Invoke(gui,new object[]{false});
    }
    internal static bool Busy;
    internal static bool Enabled=>Plugin.CraftFromChests.Value && Player.m_localPlayer && !Player.m_localPlayer.IsDead();
    internal static int Count(string name,int quality,bool remote,bool matchWorldLevel=true)
    {
        var player=Player.m_localPlayer;
        int CountInventory(Inventory inv)=>inv.GetAllItems().Where(i=>(name==null || i.m_shared.m_name==name) &&
            (quality<0 || i.m_quality==quality) && (!matchWorldLevel || i.m_worldLevel>=Game.m_worldLevel) &&
            !InventoryBlock.Get(inv).IsSlotBlocked(i.m_gridPos)).Sum(i=>i.m_stack);
        return CountInventory(player.GetInventory())+(remote?Storage.CraftingChests().Sum(c=>CountInventory(c.GetInventory())):0);
    }
    internal static Ingredient[] Plan(Piece.Requirement[] resources,int quality,int multiplier,bool onlyOne,bool building)
    {
        var station=Player.m_localPlayer.GetCurrentCraftingStation();
        var required=new List<Ingredient>();
        var alternatives=new List<IEnumerable<Ingredient>>();
        foreach(var r in resources)
        {
            if(!r.m_resItem || (!building && r.m_upgraderResource!=(station && station.m_upgrader)))continue;
            int amount=checked(r.GetAmount(quality)*multiplier);
            if(amount<=0)continue;
            var name=r.m_resItem.m_itemData.m_shared.m_name;
            if(building){required.Add(new Ingredient(name,amount));continue;}
            // Vanilla requires enough of one quality, not a sum across qualities.
            var chosen=CraftingPlan.ChooseQuality(name,amount,r.m_resItem.m_itemData.m_shared.m_maxQuality,
                (n,q)=>Count(n,q,false),(n,q)=>Count(n,q,true));
            if(onlyOne){if(chosen!=null)alternatives.Add(new[]{chosen});}
            else {if(chosen==null)return null;required.Add(chosen);}
        }
        if(!onlyOne)alternatives.Add(required);
        return CraftingPlan.Select(alternatives,(n,q)=>Count(n,q,true));
    }
    internal static bool Ready(Ingredient[] plan)=>plan!=null && plan.All(i=>CraftingPlan.Missing(i,(n,q)=>Count(n,q,false))==0);
    internal static void Begin(Ingredient[] plan,Func<bool> valid,Action completed)
    {
        if(Busy || Transfers.Busy || plan==null)return;
        Busy=true;
        Plugin.Instance.StartCoroutine(Pull(plan,valid,completed));
    }
    static IEnumerator Pull(Ingredient[] plan,Func<bool> valid,Action completed)
    {
        var player=Player.m_localPlayer;
        var origin=player.transform.position;
        bool Valid()=>Enabled && Player.m_localPlayer==player && Vector3.Distance(player.transform.position,origin)<2f && valid();
        try
        {
            // Let the triggering vanilla UI update finish before local-owner callbacks.
            yield return null;
            if(!Valid())yield break;
            player.Message(MessageHud.MessageType.Center,"Gathering ingredients from nearby chests…");
            foreach(var ingredient in plan)
            {
                while(CraftingPlan.Missing(ingredient,(n,q)=>Count(n,q,false))>0)
                {
                    if(!Valid())yield break;
                    var source=Storage.CraftingChests().SelectMany(c=>c.GetInventory().GetAllItems().Select(i=>new{Chest=c,Item=i}))
                        .FirstOrDefault(x=>x.Item.m_shared.m_name==ingredient.Name && x.Item.m_worldLevel>=Game.m_worldLevel && (ingredient.Quality<0 || x.Item.m_quality==ingredient.Quality) &&
                            !InventoryBlock.Get(x.Chest.GetInventory()).IsSlotBlocked(x.Item.m_gridPos));
                    int serial=Transfers.ReplySerial;
                    int before=Count(ingredient.Name,ingredient.Quality,false);
                    if(source==null || !Transfers.PullIngredient(source.Chest,source.Item,CraftingPlan.Missing(ingredient,(n,q)=>Count(n,q,false))))
                    {player.Message(MessageHud.MessageType.Center,"Cannot gather ingredients: check inventory space and chest access.");yield break;}
                    float deadline=Time.realtimeSinceStartup+15f;
                    while(Transfers.ReplySerial==serial && Time.realtimeSinceStartup<deadline && Valid())yield return null;
                    if(!Valid())yield break;
                    if(Transfers.ReplySerial==serial || !Transfers.LastSuccess)
                    {player.Message(MessageHud.MessageType.Center,"Ingredient transfer unconfirmed or declined. Craft cancelled.");yield break;}
                    if(!Valid())yield break;
                    if(Count(ingredient.Name,ingredient.Quality,false)<=before)
                    {player.Message(MessageHud.MessageType.Center,"Ingredients did not reach your inventory. Craft cancelled.");yield break;}
                }
            }
            if(Valid() && Ready(plan))completed();
        }
        finally{Busy=false;}
    }
}

// Scope inventory counts to vanilla requirement checks. Discovery, normal inventory
// operations and actual consumption never see hypothetical chest items.
[HarmonyPatch(typeof(Player),"HaveRequirementItems")]
static class CraftRequirementScope
{
    static void Prefix(Player __instance,bool discover,out bool __state)
    {__state=Crafting.Enabled && !Crafting.Executing && !discover && __instance==Player.m_localPlayer;if(__state)Crafting.CountScope++;}
    static void Finalizer(bool __state){if(__state)Crafting.CountScope--;}
}
[HarmonyPatch(typeof(Player),nameof(Player.HaveRequirements),typeof(Piece),typeof(Player.RequirementMode))]
static class BuildRequirementScope
{
    static void Prefix(Player __instance,Player.RequirementMode mode,out bool __state)
    {__state=Crafting.Enabled && !Crafting.Executing && mode==Player.RequirementMode.CanBuild && __instance==Player.m_localPlayer;if(__state)Crafting.CountScope++;}
    static void Finalizer(bool __state){if(__state)Crafting.CountScope--;}
}
[HarmonyPatch(typeof(Inventory),nameof(Inventory.CountItems))]
static class ChestIngredientCount
{
    static void Postfix(Inventory __instance,string name,int quality,bool matchWorldLevel,ref int __result)
    {if(Crafting.CountScope>0 && !Crafting.Executing && __instance==Player.m_localPlayer.GetInventory())__result=Crafting.Count(name,quality,true,matchWorldLevel);}
}
[HarmonyPatch(typeof(InventoryGui),nameof(InventoryGui.SetupRequirement))]
static class RequirementDisplayScope
{
    static void Prefix(Player player,out bool __state)
    {__state=Crafting.Enabled && !Crafting.Executing && player==Player.m_localPlayer;if(__state)Crafting.CountScope++;}
    static void Finalizer(bool __state){if(__state)Crafting.CountScope--;}
}
[HarmonyPatch(typeof(InventoryGui),"DoCrafting")]
static class CraftFromChests
{
    static bool Prefix(InventoryGui __instance,Player player,Recipe ___m_craftRecipe,ItemDrop.ItemData ___m_craftUpgradeItem,bool ___m_multiCrafting,out IDisposable __state)
    {
        __state=null;
        if(!Crafting.Enabled || Crafting.Executing || !___m_craftRecipe)return true;
        if(Crafting.Busy || Transfers.Busy || InventoryBlock.Get(player.GetInventory()).IsAnySlotBlocked())return false;
        if(player.NoCostCheat() || ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost))return true;
        var recipe=___m_craftRecipe;var upgrade=___m_craftUpgradeItem;
        var station=player.GetCurrentCraftingStation();
        int quality=upgrade==null?1:upgrade.m_quality+1;
        var plan=Crafting.Plan(recipe.m_resources,quality,___m_multiCrafting?__instance.m_multiCraftAmount:1,recipe.m_requireOnlyOneIngredient,false);
        if(plan==null || Crafting.Ready(plan)){__state=Crafting.Execution.Enter(()=>Crafting.RefreshRecipes(__instance));return true;}
        var view=Traverse.Create(__instance);
        int variant=view.Field("m_craftVariant").GetValue<int>();
        int multiplier=___m_multiCrafting?__instance.m_multiCraftAmount:1;
        bool SelectionMatches()
        {
            var selected=Traverse.Create(view.Field("m_selectedRecipe").GetValue());
            return selected.Property("Recipe").GetValue<Recipe>()==recipe && selected.Property("ItemData").GetValue<ItemDrop.ItemData>()==upgrade &&
                (upgrade==null || (player.GetInventory().ContainsItem(upgrade) && upgrade.m_quality+1==quality)) &&
                view.Field("m_selectedVariant").GetValue<int>()==variant &&
                view.Field("m_craftVariant").GetValue<int>()==variant &&
                (view.Field("m_multiCrafting").GetValue<bool>()?__instance.m_multiCraftAmount:1)==multiplier;
        }
        Crafting.Begin(plan,()=>__instance && InventoryGui.IsVisible() && player.GetCurrentCraftingStation()==station &&
            SelectionMatches() && view.Field("m_craftRecipe").GetValue<Recipe>()==recipe && view.Field("m_craftUpgradeItem").GetValue<ItemDrop.ItemData>()==upgrade,
            ()=>{using(Crafting.Execution.Enter(()=>Crafting.RefreshRecipes(__instance)))AccessTools.Method(typeof(InventoryGui),"DoCrafting").Invoke(__instance,new object[]{player});});
        return false;
    }
    static void Finalizer(IDisposable __state)=>__state?.Dispose();
}
[HarmonyPatch(typeof(InventoryGui),"OnCraftPressed")]
static class PreventConcurrentCraft {static bool Prefix()=>!Crafting.Busy;}
[HarmonyPatch(typeof(Player),nameof(Player.TryPlacePiece))]
static class BuildFromChests
{
    static bool Prefix(Player __instance,Piece piece,ref bool __result)
    {
        if(!Crafting.Enabled || __instance!=Player.m_localPlayer || __instance.NoCostCheat() || ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey()))return true;
        if(Crafting.Busy || Transfers.Busy || InventoryBlock.Get(__instance.GetInventory()).IsAnySlotBlocked()){__result=false;return false;}
        var plan=Crafting.Plan(piece.m_resources,0,1,false,true);
        if(Crafting.Ready(plan))return true;
        __result=false;
        DeferredBuild.Gather(__instance,piece,plan);
        return false;
    }
}
