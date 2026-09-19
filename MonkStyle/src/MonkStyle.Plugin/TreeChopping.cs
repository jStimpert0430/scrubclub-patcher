using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Jotunn.Managers;
using MonkStyle.Core;
namespace MonkStyle;
// Scope exists only on the attacking peer while vanilla constructs melee hits.
// Never alter item SharedData, tree minimum tiers, or receiver-side RPC handling.
[HarmonyPatch(typeof(Attack),"DoMeleeAttack")]
internal static class MeleeScope
{
    [ThreadStatic] internal static ItemDrop.ItemData Current;
    static void Prefix(Attack __instance,ItemDrop.ItemData ___m_weapon,out ItemDrop.ItemData __state)
    {
        __state=Current;Current=null;
        if(___m_weapon?.m_dropPrefab && Weapons.ForClaw(___m_weapon.m_dropPrefab.name)!=null &&
            __instance.m_attackAnimation==___m_weapon.m_shared.m_attack.m_attackAnimation)Current=___m_weapon;
    }
    static Exception Finalizer(Exception __exception,ItemDrop.ItemData __state){Current=__state;return __exception;}
}
[HarmonyPatch]
internal static class TreeChopping
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(TreeBase),"Damage",new[]{typeof(HitData)});
        yield return AccessTools.Method(typeof(TreeLog),"Damage",new[]{typeof(HitData)});
        yield return AccessTools.Method(typeof(Destructible),"Damage",new[]{typeof(HitData)});
    }
    static void Prefix(object __instance,HitData hit)
    {
        if(__instance is Destructible d && d.GetDestructibleType()!=DestructibleType.Tree)return;
        Apply(MeleeScope.Current,hit);
    }
    internal static void Apply(ItemDrop.ItemData weapon,HitData hit)
    {
        if(weapon?.m_dropPrefab==null || hit==null)return;
        var rule=Weapons.ForClaw(weapon.m_dropPrefab.name);if(rule==null)return;
        var axe=PrefabManager.Instance.GetPrefab(rule.Axe)?.GetComponent<ItemDrop>();if(!axe)return;
        var raw=weapon.GetDamage();var damage=hit.m_damage;
        float chop=Weapons.Chop(axe.m_itemData.m_shared.m_damages.m_chop,axe.m_itemData.m_shared.m_damagesPerLevel.m_chop,
            weapon.m_quality,damage.m_slash+damage.m_pierce+damage.m_blunt,raw.m_slash+raw.m_pierce+raw.m_blunt);
        // Convert only this hit's physical component. Combat hits are untouched.
        damage.m_slash=0;damage.m_pierce=0;damage.m_blunt=0;damage.m_chop=chop;
        hit.m_damage=damage;hit.m_toolTier=(short)rule.Tier;
    }
}
