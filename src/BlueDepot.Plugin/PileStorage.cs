using System;
using System.Linq;
using BlueDepot.Core;
using HarmonyLib;
using MultiUserChest;
using UnityEngine;
namespace BlueDepot;

// A pile keeps its original prefab and ZDO. Its recoverable materials live in
// a native serialized Inventory, so partial withdrawals use the same confirmed
// transfer protocol as chests. Never seed an inventory that already has bytes.
internal sealed class PileStorage:MonoBehaviour,Interactable,Hoverable
{
    Container container;Piece piece;ZNetView view;Piece.Requirement resource;float next;
    static readonly Func<Container,bool> load=AccessTools.MethodDelegate<Func<Container,bool>>(AccessTools.Method(typeof(Container),"Load"));
    internal static bool IsPile(Container c)=>c && c.GetComponentInParent<PileStorage>();
    internal static bool Ready(Container c)
    {
        if(!IsPile(c))return true;
        var pile=c.GetComponentInParent<PileStorage>();
        return pile.view && pile.view.IsValid() && pile.view.GetZDO().GetByteArray(ZDOVars.s_items)!=null;
    }
    internal static bool Candidate(Piece p)
    {
        if(!p || p.GetComponent<Container>() || p.GetComponentInChildren<Container>())return false;
        var resources=p.m_resources.Where(r=>r.m_resItem && r.m_amount>0).ToArray();
        var r=resources.FirstOrDefault();
        return r!=null && PileRules.IsResourcePile(Utils.GetPrefabName(p.gameObject),resources.Length,r.m_recover,r.m_amount,
            r.m_resItem.m_itemData.m_shared.m_itemType==ItemDrop.ItemData.ItemType.Material);
    }
    void Awake()
    {
        piece=GetComponent<Piece>();view=GetComponent<ZNetView>();resource=piece.m_resources.First(r=>r.m_resItem && r.m_amount>0);
        var go=new GameObject("BlueDepotPileInventory");go.SetActive(false);go.transform.SetParent(transform,false);
        container=go.AddComponent<Container>();container.m_rootObjectOverride=view;
        container.m_width=Math.Max(1,(int)Math.Ceiling((double)resource.m_amount/resource.m_resItem.m_itemData.m_shared.m_maxStackSize));
        container.m_height=1;container.m_name=piece.m_name;container.m_checkGuardStone=true;
        var template=Jotunn.Managers.PrefabManager.Instance.GetPrefab("piece_chest_wood");
        if(template)container.m_bkg=template.GetComponent<Container>().m_bkg;
        go.SetActive(true);
    }
    void Update()
    {
        if(Time.unscaledTime<next)return;next=Time.unscaledTime+.25f;
        if(!view || !view.IsValid() || !container)return;
        var zdo=view.GetZDO();bool saved=zdo.GetByteArray(ZDOVars.s_items)!=null;
        if(saved){load(container);return;}
        if(!PileRules.NeedsInitialization(piece.IsPlacedByPlayer(),saved,view.IsOwner(),ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey())))return;
        // Inventory.AddItem serializes each successful change using Container's
        // existing onChanged handler. No separate initialization flag can lag it.
        var inventory=container.GetInventory();
        if(inventory.NrOfItems()!=0)return;
        int left=resource.m_amount;
        // Construct the complete inventory off-line, then persist it once. A
        // save between stack additions must not leave a half-initialized pile.
        var initial=new Inventory(piece.m_name,null,container.m_width,1);
        while(left>0)
        {
            var item=resource.m_resItem.m_itemData.Clone();item.m_dropPrefab=resource.m_resItem.gameObject;
            item.m_stack=Math.Min(left,item.m_shared.m_maxStackSize);left-=item.m_stack;
            item.m_cheated=zdo.GetBool(ZDOVars.s_cheated);
            if(!initial.AddItem(item))throw new InvalidOperationException("Pile inventory capacity mismatch");
        }
        var package=new ZPackage();initial.Save(package);zdo.Set(ZDOVars.s_items,package.GetArray());load(container);
    }
    internal static bool Accepts(Inventory inventory,ItemDrop.ItemData item)
    {
        if(!ContainerExtend.GetContainer(inventory,out var owner) || !IsPile(owner.Container))return true;
        var p=owner.Container.GetComponentInParent<PileStorage>();
        return Ready(owner.Container) && item!=null && item.m_quality==p.resource.m_resItem.m_itemData.m_quality &&
            PileRules.Accepts(p.resource.m_resItem.name,item.m_dropPrefab?item.m_dropPrefab.name:"",p.resource.m_amount,
                inventory.GetAllItems().Sum(i=>i.m_stack),item.m_stack);
    }
    internal static bool HasSavedInventory(Piece p)
    {
        var pile=p.GetComponent<PileStorage>();
        if(!pile || !pile.view || !pile.view.IsValid())return false;
        bool saved=pile.view.GetZDO().GetByteArray(ZDOVars.s_items)!=null;
        if(saved && pile.view.IsOwner())load(pile.container);
        return saved;
    }
    internal static string Contents(Piece p)
    {
        var pile=p.GetComponent<PileStorage>();
        return pile && Ready(pile.container)?$"\n{pile.container.GetInventory().GetAllItems().Sum(i=>i.m_stack)} / {pile.resource.m_amount} · [E] Open pile":"";
    }
    public string GetHoverText()=>Localization.instance.Localize(piece.m_name+Contents(piece));
    public float GetHoverOffset()=>0;
    public string GetHoverName()=>Localization.instance.Localize(piece.m_name);
    public bool Interact(Humanoid human,bool hold,bool alt)=>Ready(container) && container.Interact(human,hold,alt);
    public bool UseItem(Humanoid user,ItemDrop.ItemData item)=>false;
}
[HarmonyPatch(typeof(Piece),"Awake")]
static class RegisterPileStorage
{
    static void Postfix(Piece __instance)
    {
        var view=__instance.GetComponent<ZNetView>();
        if(view && view.IsValid() && PileStorage.Candidate(__instance) && !__instance.GetComponent<PileStorage>())
            __instance.gameObject.AddComponent<PileStorage>();
    }
}
[HarmonyPatch(typeof(Piece),nameof(Piece.DropResources))]
static class PileRefund
{
    // Container.OnDestroyed drops the remaining inventory. Returning the
    // original recipe as well would duplicate consumed or withdrawn resources.
    static bool Prefix(Piece __instance)=>!PileStorage.HasSavedInventory(__instance);
}
[HarmonyPatch(typeof(Inventory),nameof(Inventory.AddItem),typeof(ItemDrop.ItemData))]
static class PileItemRestriction
{
    static bool Prefix(Inventory __instance,ItemDrop.ItemData item,ref bool __result)
    {
        if(PileStorage.Accepts(__instance,item))return true;
        __result=false;return false;
    }
}
