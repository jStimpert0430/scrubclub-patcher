using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BepInEx;
using Jotunn.Managers;
using MonkStyle.Core;
using UnityEngine;
[BepInPlugin("local.monkstyle.probe","MonkStyle runtime probe","1.0.0")]
[BepInDependency("scrubclub.monkstyle")]
public class Probe:BaseUnityPlugin
{
    void Awake(){PrefabManager.OnVanillaPrefabsAvailable+=StartProbe;StartCoroutine(Timeout());}
    IEnumerator Timeout(){yield return new WaitForSecondsRealtime(90);Logger.LogError("MONK_PROBE TIMEOUT");Application.Quit(2);}
    void StartProbe(){PrefabManager.OnVanillaPrefabsAvailable-=StartProbe;StartCoroutine(Run());}
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    IEnumerator Run()
    {
        yield return null;
        try
        {
            // Recipes normally register on entering a world; exercise that same
            // registration method against this disposable menu ObjectDB.
            typeof(ItemManager).GetMethod("RegisterCustomRecipes",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ItemManager.Instance,new object[]{ObjectDB.instance});
            foreach(var go in ObjectDB.instance.m_items) {
                var d=go.GetComponent<ItemDrop>();if(!d)continue;
                if(go.name.ToLowerInvariant().Contains("bjorn") || Localization.instance.Localize(d.m_itemData.m_shared.m_name).ToLowerInvariant().Contains("bear"))
                    Logger.LogInfo("BEAR_DATA "+go.name+" name="+Localization.instance.Localize(d.m_itemData.m_shared.m_name)+" type="+d.m_itemData.m_shared.m_itemType+" set="+d.m_itemData.m_shared.m_setName+" size="+d.m_itemData.m_shared.m_setSize);
            }
            var assembly=typeof(MonkStyle.Plugin).Assembly;
            var tree=assembly.GetType("MonkStyle.TreeChopping");var prefix=tree.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic);
            var scope=assembly.GetType("MonkStyle.MeleeScope");var current=scope.GetField("Current",BindingFlags.Static|BindingFlags.NonPublic);
            foreach(var w in Weapons.All)
            {
                var originalPrefab=PrefabManager.Instance.GetPrefab(w.Source);
                var original=originalPrefab.GetComponent<ItemDrop>().m_itemData.Clone();
                original.m_dropPrefab=originalPrefab; // Assigned by vanilla when an actual item is spawned/loaded.
                var item=ObjectDB.instance.GetItemPrefab(w.Id).GetComponent<ItemDrop>().m_itemData;
                Check(original.m_shared.m_toolTier==3,"Original tool tier changed");
                Check(original.m_shared.m_damages.m_chop==0,"Original chop changed");
                Check(original.m_shared.m_damages.m_slash>0,"Original combat damage changed");
                Check(!ReferenceEquals(original.m_shared,item.m_shared),"Shared item data aliased");
                Check(item.m_shared.m_damages.m_slash==0 && item.m_shared.m_damages.m_pierce==0,"New weapon not purely blunt");
                Check(item.m_shared.m_damages.m_blunt==original.m_shared.m_damages.m_slash+original.m_shared.m_damages.m_pierce,"Damage mismatch");
                Check(item.m_shared.m_damagesPerLevel.m_blunt==4,"Upgrade damage mismatch");
                Check(item.m_shared.m_backstabBonus==6 && item.m_shared.m_timedBlockBonus==6,"Multiplier mismatch");
                Check(item.m_shared.m_attack.m_attackStamina==original.m_shared.m_attack.m_attackStamina,"Stamina changed");
                var recipe=ObjectDB.instance.m_recipes.Single(r=>r.m_item && r.m_item.name==w.Id);
                Check(recipe.m_resources.Any(r=>r.m_resItem.name==w.Wood),"Wood missing");
                Check(recipe.m_resources.Any(r=>r.m_resItem.name.StartsWith("Upgrader")),"Idol gate missing");
                Check(item.m_shared.m_icons.Length==1 && item.m_shared.m_icons[0].texture.width==128,"Icon missing");
                System.IO.File.WriteAllBytes("/home/bunta/famitracker/mods/MonkStyle/artifacts/runtime-probe/"+w.Id+".png",ImageConversion.EncodeToPNG(item.m_shared.m_icons[0].texture));
                var skin=item.m_dropPrefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Check(skin.sharedMesh.vertexCount==144,"Custom model missing");
                Check(!ReferenceEquals(skin.sharedMesh,original.m_dropPrefab.GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMesh),"Model aliases original");
                current.SetValue(null,original);
                var hit=new HitData{m_damage=original.GetDamage(),m_toolTier=3};
                var treePrefab=Resources.FindObjectsOfTypeAll<TreeBase>().First(t=>t.name=="Birch1");
                int min=treePrefab.m_minToolTier;prefix.Invoke(null,new object[]{treePrefab,hit});
                Check(hit.m_toolTier==w.Tier && hit.m_damage.m_chop>0,"Tree conversion failed");
                Check(treePrefab.m_minToolTier==min,"Tree hardness changed");
                Check(hit.CheckToolTier(min,true)==(w.Tier>=2),"Birch progression changed");
                Check(hit.CheckToolTier(4,true)==(w.Tier>=4),"Yggdrasil progression changed");
                var rock=Resources.FindObjectsOfTypeAll<Destructible>().First(d=>d.m_destructibleType!=DestructibleType.Tree);
                hit=new HitData{m_damage=original.GetDamage(),m_toolTier=3};prefix.Invoke(null,new object[]{rock,hit});
                Check(hit.m_damage.m_chop==0 && hit.m_toolTier==3,"Non-tree hit changed");
                current.SetValue(null,null);
                hit=new HitData{m_damage=original.GetDamage(),m_toolTier=3};prefix.Invoke(null,new object[]{treePrefab,hit});
                Check(hit.m_damage.m_chop==0 && hit.m_toolTier==3,"Unscoped damage changed");
                var enter=scope.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic);var exit=scope.GetMethod("Finalizer",BindingFlags.Static|BindingFlags.NonPublic);
                var args=new object[]{original.m_shared.m_attack,original,null};enter.Invoke(null,args);
                Check(ReferenceEquals(current.GetValue(null),original),"Primary attack scope missing");
                var nested=new object[]{original.m_shared.m_secondaryAttack,original,null};enter.Invoke(null,nested);
                Check(current.GetValue(null)==null,"Secondary attack must not chop");
                var failure=new Exception("simulated");Check(ReferenceEquals(exit.Invoke(null,new object[]{failure,nested[2]}),failure),"Attack exception swallowed");
                Check(ReferenceEquals(current.GetValue(null),original),"Nested scope not restored");
                exit.Invoke(null,new object[]{null,args[2]});Check(current.GetValue(null)==null,"Scope leaked after attack");
                Logger.LogInfo("MONK_PROBE PASS "+w.Id+" blunt="+item.m_shared.m_damages.m_blunt+" nativeTier="+original.m_shared.m_toolTier+" harvestTier="+w.Tier+" meshBounds="+skin.sharedMesh.bounds+" recipe="+string.Join(",",recipe.m_resources.Select(r=>r.m_resItem.name+":"+r.m_amount)));
            }
            foreach(var id in new[]{BearAuraRules.Helmet,BearAuraRules.Chest,BearAuraRules.Legs}) {
                var armor=ObjectDB.instance.GetItemPrefab(id).GetComponent<ItemDrop>().m_itemData.m_shared;
                Check(armor.m_setName=="berserker_armor" && armor.m_setSize==3,"Bear armor set changed");
            }
            var auraType=assembly.GetType("MonkStyle.BearAura");
            var auraObject=new GameObject("AuraProbe");auraObject.SetActive(false);
            var aura=auraObject.AddComponent(auraType);
            auraType.GetMethod("Build",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(aura,null);
            var effect=(GameObject)auraType.GetField("effect",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(aura);
            Check(effect,"Aura construction failed");
            Check(effect.GetComponentsInChildren<LineRenderer>(true).Length==3,"Wind ribbons missing");
            Check(effect.GetComponentInChildren<ParticleSystem>(true).main.maxParticles<=64,"Unbounded aura particles");
            Check(effect.GetComponentInChildren<Light>(true).shadows==LightShadows.None,"Aura casts expensive shadows");
            auraType.GetMethod("Clear",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(aura,null);
            Check(auraType.GetField("effect",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(aura)==null,"Aura did not clean up");
            Destroy(auraObject);
            Logger.LogInfo("MONK_PROBE PASS bear set, aura construction, bounded particles, wind, soft light and cleanup");
            Logger.LogInfo("MONK_PROBE PASS ALL");Application.Quit(0);
        }
        catch(Exception e){Logger.LogError("MONK_PROBE FAIL "+e);Application.Quit(1);}
    }
}
