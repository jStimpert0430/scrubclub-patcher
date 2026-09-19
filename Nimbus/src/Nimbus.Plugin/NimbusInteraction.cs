using UnityEngine;
namespace Nimbus;
// Player hover selection checks the directly hit collider before its rigidbody.
// Forward deck hits to the same controller used by the side of the cloud.
internal sealed class NimbusInteraction:MonoBehaviour,Hoverable,Interactable
{
    ShipControlls Controls=>GetComponentInParent<Ship>().m_shipControlls;
    public string GetHoverText()=>Controls.GetHoverText();
    public string GetHoverName()=>Controls.GetHoverName();
    public float GetHoverOffset()=>Controls.GetHoverOffset();
    public bool Interact(Humanoid user,bool hold,bool alt)=>Controls.Interact(user,hold,alt);
    public bool UseItem(Humanoid user,ItemDrop.ItemData item)=>Controls.UseItem(user,item);
}
