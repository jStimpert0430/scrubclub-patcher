using System;
using System.Collections;
using System.Linq;
using BepInEx;
using Jotunn.Managers;
using Jotunn.Utils;
using Jotunn;
using UnityEngine;
[BepInPlugin("local.nimbus.probe","Nimbus runtime probe","1.0.0")]
[BepInDependency("scrubclub.nimbus")]
public class Probe:BaseUnityPlugin
{
    void Awake(){PrefabManager.OnVanillaPrefabsAvailable+=Run;StartCoroutine(Timeout());}
    IEnumerator Timeout(){yield return new WaitForSecondsRealtime(90);Logger.LogError("NIMBUS_PROBE TIMEOUT");Application.Quit(2);}
    void Run(){PrefabManager.OnVanillaPrefabsAvailable-=Run;StartCoroutine(Check());}
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    IEnumerator Check()
    {
        yield return null;
        try
        {
            var prefab=PrefabManager.Instance.GetPrefab("Nimbus_Cloud");Require(prefab,"Nimbus not registered");
            prefab.FixReferences(false); // Same resource resolution used at world-entry registration.
            var source=PrefabManager.Instance.GetPrefab("Karve");var piece=prefab.GetComponent<Piece>();var ship=prefab.GetComponent<Ship>();
            Require(piece.m_resources.Length==3,"Recipe mismatch");
            foreach(var expected in new[]{("Iron",20),("ElderBark",10),("Feathers",10)})Require(piece.m_resources.Any(r=>r.m_resItem && r.m_resItem.name==expected.Item1 && r.m_amount==expected.Item2),"Recipe resource missing "+expected.Item1);
            Require(piece.m_craftingStation && piece.m_craftingStation.name=="forge","Missing forge gate");
            Require(!piece.m_waterPiece,"Still water-only placement");
            var cargo=prefab.GetComponentsInChildren<Container>(true);
            Require(cargo.Length==1 && cargo[0].m_width==2 && cargo[0].m_height==2,"Expected four-slot cargo");
            Require(cargo[0].m_rootObjectOverride==prefab.GetComponent<ZNetView>(),"Cargo network root incorrect");
            Require(source.GetComponentsInChildren<Container>(true).Length>0,"Original Karve cargo modified");
            Require(prefab.GetComponentsInChildren<Renderer>(true).Count(r=>r.enabled)==9,"Expected nine white cloud puffs");
            Require(ship.m_shipControlls && ship.m_shipControlls.m_attachPoint && ship.m_shipControlls.m_attachPoint.IsChildOf(prefab.transform),"Rider attachment invalid");
            Require(ship.m_shipControlls.m_attachAnimation=="NimbusStanding","Rider still seated");
            Require(prefab.GetComponentsInChildren<ImpactEffect>(true).All(i=>!i.m_damageToSelf),"Collision self-damage still enabled");
            Require(prefab.GetComponent<Rigidbody>().useGravity,"Gravity missing");
            var deck=prefab.transform.Find("Nimbus white cloud/Nimbus deck").GetComponent<BoxCollider>();
            Require(deck && Mathf.Abs(deck.center.y+deck.size.y*.5f-.28f)<.001f,"Standing deck height incorrect");
            Require(deck.GetComponent<Hoverable>()!=null && deck.GetComponent<Interactable>()!=null,"Top deck cannot be interacted with");
            Require(deck.gameObject.layer==LayerMask.NameToLayer("piece"),"Deck absent from piece interaction layer");
            int playerLayer=PrefabManager.Instance.GetPrefab("Player").layer;
            Require((deck.includeLayers.value & (1<<playerLayer))!=0,"Deck excludes player layer");
            Require((deck.excludeLayers.value & (1<<LayerMask.NameToLayer("terrain")))!=0,"Deck collides with terrain");
            Require(!ship.m_ashlandsReady,"Midgame vehicle bypasses Ashlands gate");
            Require(prefab.GetComponents<MonoBehaviour>().Any(b=>b.GetType().Name=="NimbusMotor"),"Motor missing");
            Require(piece.m_icon && piece.m_icon.texture.width==64,"Cloud icon missing");
            Logger.LogInfo("NIMBUS_PROBE PASS: recipe, networked ship/controller, white cloud model, single rider attachment, four-slot native cargo, gravity, native Karve untouched");
            Application.Quit(0);
        }
        catch(Exception e){Logger.LogError("NIMBUS_PROBE FAIL "+e);Application.Quit(1);}
    }
}
