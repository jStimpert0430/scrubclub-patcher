using System;
using System.Linq;
using BepInEx;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using MonkStyle.Core;
namespace MonkStyle;
[BepInPlugin(Guid,"MonkStyle","0.1.0")]
[BepInDependency(Jotunn.Main.ModGuid)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod,VersionStrictness.Patch)]
public sealed class Plugin:BaseUnityPlugin
{
    public const string Guid="scrubclub.monkstyle";
    Harmony harmony;
    void Awake(){BearAura.Enabled=Config.Bind("Visuals","BearAura",true,"Show a cosmetic golden aura, soft light and wind around players wearing the complete bear armor set. Local visual preference; no gameplay bonuses.");new Terminal.ConsoleCommand("monkstyle_status","Report equipped bear armor and cosmetic aura",args=>
        {
            var p=Player.m_localPlayer;if(!p)return;
            args.Context.AddString("Equipped: "+string.Join(", ",p.GetInventory().GetAllItems().Where(i=>i.m_equipped).Select(i=>i.m_dropPrefab?i.m_dropPrefab.name:i.m_shared.m_name)));
            var aura=p.GetComponent<BearAura>();args.Context.AddString(aura?aura.Status():"Bear aura component missing");
        });harmony=new Harmony(Guid);harmony.PatchAll();PrefabManager.OnVanillaPrefabsAvailable+=Register;}
    void OnDestroy(){PrefabManager.OnVanillaPrefabsAvailable-=Register;harmony?.UnpatchSelf();}
    void Register()
    {
        PrefabManager.OnVanillaPrefabsAvailable-=Register;
        foreach(var definition in Weapons.All)
        {
            var source=PrefabManager.Instance.GetPrefab(definition.Source).GetComponent<ItemDrop>().m_itemData;
            // Retain the original recipe's progression gates (including idols),
            // replacing the claw parts with wood. Preserve its station/level.
            // At the prefab event, the destination ObjectDB has not copied recipes yet.
            var recipe=UnityEngine.Resources.FindObjectsOfTypeAll<ObjectDB>().SelectMany(db=>db.m_recipes)
                .First(r=>r.m_item && r.m_item.name==definition.Source);
            var config=new ItemConfig{Name=definition.Name,Description="Carved wooden knuckles, bound for battle. Fists skill; blunt damage.",
                CraftingStation=recipe.m_craftingStation.name,MinStationLevel=recipe.m_minStationLevel};
            foreach(var requirement in recipe.m_resources.Where(r=>r.m_resItem && !IsClawPart(r.m_resItem.name)))
                config.AddRequirement(new RequirementConfig(requirement.m_resItem.name,requirement.m_amount,requirement.m_amountPerLevel));
            config.AddRequirement(new RequirementConfig(definition.Wood,20,10));
            var custom=new CustomItem(definition.Id,definition.Source,config);
            var data=custom.ItemPrefab.GetComponent<ItemDrop>().m_itemData;
            // Unity prefab cloning must not leave nested managed SharedData aliased.
            data.m_shared=AccessTools.Method(typeof(object),"MemberwiseClone").Invoke(source.m_shared,null) as ItemDrop.ItemData.SharedData;
            data.m_shared.m_name=definition.Name;data.m_shared.m_description=config.Description;
            data.m_shared.m_damages=AsBlunt(source.m_shared.m_damages);
            data.m_shared.m_damagesPerLevel=AsBlunt(source.m_shared.m_damagesPerLevel);
            data.m_shared.m_attack=source.m_shared.m_attack.Clone();
            data.m_shared.m_secondaryAttack=source.m_shared.m_secondaryAttack.Clone();
            data.m_dropPrefab=custom.ItemPrefab;
            WoodenVisuals.Apply(custom.ItemPrefab,definition);
            if(!ItemManager.Instance.AddItem(custom))throw new InvalidOperationException("Could not register "+definition.Id);
            Logger.LogInfo("Registered "+definition.Id+" from "+definition.Source+"; native tool tier unchanged: "+source.m_shared.m_toolTier);
        }
    }
    static bool IsClawPart(string id)=>id=="BjornPaw" || id=="WolfClaw" || id=="UndeadBjornRibcage";
    internal static HitData.DamageTypes AsBlunt(HitData.DamageTypes damage)
    {
        damage.m_blunt=Weapons.Blunt(damage.m_blunt,damage.m_slash,damage.m_pierce);
        damage.m_slash=0;damage.m_pierce=0;return damage;
    }
}
