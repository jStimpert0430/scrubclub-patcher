using HarmonyLib;
using UnityEngine;
namespace Nimbus;
[HarmonyPatch(typeof(Hud),"UpdateShipHud")]
static class NimbusHud
{
    static readonly Vector3[] Corners=new Vector3[4];
    static void Postfix(Hud __instance,Player player)
    {
        var ship=player?player.GetControlledShip():null;
        if(!ship || !ship.GetComponent<NimbusMotor>() || !__instance.m_shipControlsRoot || !__instance.m_shipWindIndicatorRoot)return;
        // The vanilla method resets the controls' position every frame. Change
        // only their position after it runs, so ordinary boats restore naturally.
        var controls=__instance.m_shipControlsRoot.GetComponent<RectTransform>();
        var wind=__instance.m_shipWindIndicatorRoot;
        var canvas=wind.GetComponentInParent<Canvas>();
        float scale=canvas?canvas.scaleFactor:1;
        wind.GetWorldCorners(Corners);
        float left=Mathf.Min(Mathf.Min(Corners[0].x,Corners[1].x),Mathf.Min(Corners[2].x,Corners[3].x));
        float halfWidth=controls?controls.rect.width*(1-controls.pivot.x)*scale:24*scale;
        var position=wind.position;position.x=left-20*scale-halfWidth;
        __instance.m_shipControlsRoot.transform.position=position;
    }
}
