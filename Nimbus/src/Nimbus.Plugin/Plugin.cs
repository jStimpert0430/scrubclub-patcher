using System;
using System.Linq;
using BepInEx;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
namespace Nimbus;
[BepInPlugin(Guid,"Nimbus","0.1.2")]
[BepInDependency(Jotunn.Main.ModGuid)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod,VersionStrictness.Patch)]
public sealed class Plugin:BaseUnityPlugin
{
    public const string Guid="scrubclub.nimbus",Prefab="Nimbus_Cloud";
    Harmony harmony;
    void Awake(){new Terminal.ConsoleCommand("nimbus_status","Report nearby Nimbus physics and controller status",args=>
        {
            var player=Player.m_localPlayer;if(!player)return;
            var cloud=UnityEngine.Object.FindObjectsByType<NimbusMotor>(FindObjectsSortMode.None).OrderBy(m=>Vector3.Distance(m.transform.position,player.transform.position)).FirstOrDefault();
            args.Context.AddString(cloud?cloud.Status():"No loaded Nimbus found");
        });harmony=new Harmony(Guid);harmony.PatchAll();PrefabManager.OnVanillaPrefabsAvailable+=Register;}
    void OnDestroy(){PrefabManager.OnVanillaPrefabsAvailable-=Register;harmony?.UnpatchSelf();}
    void Register()
    {
        PrefabManager.OnVanillaPrefabsAvailable-=Register;
        var config=new PieceConfig{Name="Nimbus",Description="A cargo cloud for one rider. Steer freely over land and water.",PieceTable="Hammer",Category="Misc",CraftingStation="forge"};
        config.AddRequirement(new RequirementConfig("IronNails",20,0,true));config.AddRequirement(new RequirementConfig("ElderBark",10,0,true));config.AddRequirement(new RequirementConfig("Feathers",10,0,true));
        var piece=new CustomPiece(Prefab,"Karve",config);var go=piece.PiecePrefab;
        foreach(var r in go.GetComponentsInChildren<Renderer>(true))r.enabled=false;
        foreach(var a in go.GetComponentsInChildren<AudioSource>(true))a.enabled=false;
        foreach(var p in go.GetComponentsInChildren<ParticleSystem>(true))p.gameObject.SetActive(false);
        foreach(var c in go.GetComponentsInChildren<Collider>(true))c.enabled=false;
        // Retain the Karve's native persistent container and destruction/drop
        // handling; expose it through the cloud's alternate interaction.
        var cargo=go.GetComponentInChildren<Container>(true);
        if(!cargo)throw new InvalidOperationException("Karve cargo container missing");
        cargo.m_width=2;cargo.m_height=2;cargo.m_name="Nimbus cargo";cargo.m_rootObjectOverride=go.GetComponent<ZNetView>();
        foreach(var b in go.GetComponentsInChildren<Behaviour>(true))if(b.GetType().Name=="ShipEffects" || b.GetType().Name=="MagicaCloth")b.enabled=false;
        var ship=go.GetComponent<Ship>();ship.enabled=true;var controls=ship.m_shipControlls;controls.enabled=true;
        controls.transform.SetParent(go.transform,false);controls.transform.localPosition=Vector3.zero;controls.transform.localRotation=Quaternion.identity;controls.transform.localScale=Vector3.one;
        controls.gameObject.layer=LayerMask.NameToLayer("piece");controls.gameObject.SetActive(true);controls.m_hoverText="Ride Nimbus";controls.m_maxUseRange=3;
        var attach=new GameObject("Nimbus rider").transform;attach.SetParent(go.transform,false);attach.localPosition=new Vector3(0,.28f,0);
        controls.m_attachPoint=attach;controls.m_attachAnimation=StandingRider.Animation;controls.m_detachOffset=Vector3.zero;
        var solid=controls.gameObject.AddComponent<BoxCollider>();solid.size=new Vector3(1.35f,.24f,1.85f);solid.center=new Vector3(0,.02f,0);
        solid.sharedMaterial=new PhysicsMaterial("Nimbus glide"){staticFriction=0,dynamicFriction=0,frictionCombine=PhysicsMaterialCombine.Minimum,bounciness=0,bounceCombine=PhysicsMaterialCombine.Minimum};
        var volume=new GameObject("Nimbus proximity");volume.layer=LayerMask.NameToLayer("Ignore Raycast");volume.transform.SetParent(go.transform,false);var trigger=volume.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.center=Vector3.up;trigger.size=new Vector3(5,4,5);
        ship.m_controlGuiPos=attach;ship.m_hasSail=false;ship.m_ashlandsReady=false;
        var body=go.GetComponent<Rigidbody>();body.mass=100;body.useGravity=true;body.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.linearDamping=0;body.angularDamping=2;body.centerOfMass=Vector3.zero;
        // Gentle ground contact must not apply the boat's collision self-damage.
        // WearNTear still handles enemy damage, repairs and destruction normally.
        foreach(var impact in go.GetComponentsInChildren<ImpactEffect>(true))impact.m_damageToSelf=false;
        var build=go.GetComponent<Piece>();build.m_waterPiece=false;build.m_noInWater=false;build.m_onlyInTeleportArea=false;
        CloudModel.Create(go);
        // A separate rider-only deck stays under the player's feet after
        // control release. It cannot snag terrain, scenery or other vehicles.
        var deckObject=new GameObject("Nimbus deck");deckObject.layer=LayerMask.NameToLayer("piece");deckObject.transform.SetParent(go.transform.Find("Nimbus white cloud"),false);
        deckObject.AddComponent<NimbusInteraction>();
        var deck=deckObject.AddComponent<BoxCollider>();deck.size=new Vector3(1.45f,.12f,1.9f);deck.center=new Vector3(0,.22f,0);
        int playerLayers=LayerMask.GetMask("character","character_net");
        deck.includeLayers=playerLayers;deck.excludeLayers=~playerLayers;deck.layerOverridePriority=10;
        go.AddComponent<NimbusMotor>();
        go.AddComponent<CloudMist>();
        if(!PieceManager.Instance.AddPiece(piece))throw new InvalidOperationException("Nimbus registration failed");
        Logger.LogInfo("Nimbus registered: white single-rider cloud, forge recipe; 20 iron nails, 10 ancient bark, 10 feathers.");
    }
}
[HarmonyPatch(typeof(Ship),"CustomFixedUpdate")]
static class HoverPhysics
{
    static bool Prefix(Ship __instance,float fixedDeltaTime)
    {var motor=__instance.GetComponent<NimbusMotor>();if(!motor)return true;return false;}
}
[HarmonyPatch(typeof(ShipControlls),"Interact")]
static class MountFromGround
{
    static bool Prefix(ShipControlls __instance,Humanoid character,bool repeat,bool alt,ref bool __result)
    {
        if(!__instance.m_ship.GetComponent<NimbusMotor>())return true;
        __result=false;var player=character as Player;
        if(repeat || !player || player!=Player.m_localPlayer || player.IsDead() || Vector3.Distance(player.transform.position,__instance.transform.position)>2.5f)return false;
        if(alt)
        {
            var cargo=__instance.m_ship.GetComponentInChildren<Container>();
            if(cargo)__result=cargo.Interact(character,false,false);
            return false;
        }
        if(player.IsEncumbered()){player.Message(MessageHud.MessageType.Center,"You are carrying too much to ride Nimbus. Put some items in its cargo first.");return false;}
        var view=__instance.m_ship.GetComponent<ZNetView>();if(!view || !view.IsValid())return false;
        // Owner-side Nimbus arbitration keeps the native attachment response.
        view.InvokeRPC("RequestControl",player.GetPlayerID());__result=true;return false;
    }
}

[HarmonyPatch(typeof(ShipControlls),"GetHoverText")]
static class NimbusCargoHint
{
    static void Postfix(ShipControlls __instance,ref string __result)
    {
        if(__instance.m_ship.GetComponent<NimbusMotor>() && Player.m_localPlayer && Vector3.Distance(Player.m_localPlayer.transform.position,__instance.transform.position)<=2.5f)
            __result+="\n"+Localization.instance.Localize("[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] Open cargo (4 slots)");
    }
}

[HarmonyPatch(typeof(ShipControlls),"ApplyControlls")]
static class NimbusMovementControls
{
    static bool Prefix(ShipControlls __instance,Vector3 moveDir,Vector3 lookDir,bool run)
    {
        var motor=__instance.m_ship.GetComponent<NimbusMotor>();if(!motor)return true;
        motor.SendInput(moveDir,lookDir,run);
        return false;
    }
}

// Boats require the rider to already be inside their deck trigger. A hovering
// cloud is boarded from outside: validate the requesting peer and distance instead.
[HarmonyPatch(typeof(ShipControlls),"RPC_RequestControl")]
static class NimbusMountRequest
{
    static bool Prefix(ShipControlls __instance,long sender,long playerID)
    {
        var motor=__instance.m_ship.GetComponent<NimbusMotor>();if(!motor)return true;
        var view=__instance.m_ship.GetComponent<ZNetView>();if(!view || !view.IsValid() || !view.IsOwner())return false;
        var player=Player.GetPlayer(playerID);var pv=player?player.GetComponent<ZNetView>():null;
        bool granted=player && pv && pv.IsValid() && Nimbus.Core.MountRules.CanGrant(playerID,sender,pv.GetZDO().GetOwner(),
            Vector3.Distance(player.transform.position,__instance.transform.position),!player.IsDead(),player.IsEncumbered(),__instance.GetUser(),__instance.HaveValidUser());
        if(granted)view.GetZDO().Set(ZDOVars.s_user,playerID);
        motor.LastMount=granted?"granted":"denied (identity, distance, weight or occupied)";
        view.InvokeRPC(sender,"RequestRespons",granted);
        return false;
    }
}
[HarmonyPatch(typeof(ShipControlls),"HaveValidUser")]
static class NimbusValidRider
{
    static bool Prefix(ShipControlls __instance,ref bool __result)
    {
        if(!__instance.m_ship.GetComponent<NimbusMotor>())return true;
        var player=Player.GetPlayer(__instance.GetUser());
        __result=player && !player.IsDead() && Vector3.Distance(player.transform.position,__instance.transform.position)<=3f;
        return false;
    }
}

// Attachment supplies position/rotation; leave the animator in its normal idle
// standing state instead of entering a chair/helm pose. Do not invent an animator parameter.
[HarmonyPatch(typeof(ZSyncAnimation),"SetBool",new[]{typeof(string),typeof(bool)})]
static class StandingRider
{
    internal const string Animation="NimbusStanding";
    static bool Prefix(string name)=>name!=Animation;
}

[HarmonyPatch(typeof(ShipControlls),"OnUseStop")]
static class NimbusParkRider
{
    static bool Prefix(ShipControlls __instance,Player player)
    {
        if(!__instance.m_ship.GetComponent<NimbusMotor>() || !player || player.IsDead())return true;
        var view=__instance.m_ship.GetComponent<ZNetView>();
        if(!view || !view.IsValid())return true;
        // StopDoodadControl clears input after this callback. Retain the native
        // attachment and seat reservation, so the rider remains on the cloud.
        // Normal movement/jump then detaches through Player.SetControls; E resumes.
        return false;
    }
}
