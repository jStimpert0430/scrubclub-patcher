using System;
using HarmonyLib;
using MultiUserChest;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlueDepot;

internal sealed partial class DepotUi
{
    sealed class Selection
    {
        internal Container Chest;
        internal ItemDrop.ItemData Item;
        internal string Key;
        internal Vector2i Slot;
        internal int Amount;
        internal bool Valid()=>Chest && Storage.CanAccess(Chest) && Chest.GetInventory().ContainsItem(Item) &&
            Item.m_gridPos==Slot && Storage.Key(Item)==Key && Amount>0 && Item.m_stack>=Amount &&
            !InventoryBlock.Get(Chest.GetInventory()).IsSlotBlocked(Slot);
    }
    Selection held,splitSelection;
    GameObject heldIcon;
    internal static bool GridOpen=>instance && instance.panel && instance.closingAt<0 && Storage.ValidSession(instance.depot);
    static void GuiCall(string method,params object[] args)=>AccessTools.Method(typeof(InventoryGui),method).Invoke(InventoryGui.instance,args);
    static Selection Capture(Container chest,ItemDrop.ItemData item,int amount)=>new Selection{
        Chest=chest,Item=item,Key=Storage.Key(item),Slot=item.m_gridPos,Amount=amount};
    GameObject CreateGridCell(int index,Container chest,ItemDrop.ItemData item)
    {
        var grid=InventoryGui.instance.m_playerGrid;
        var go=Instantiate(grid.m_elementPrefab,content,false);
        Rect(go,content,(index%8)*72,(index/8)*72,70,70);
        var e=go.GetComponent<InventoryElement>();e.Initialize(index%8,index/8);
        e.m_button.onClick.RemoveAllListeners();e.m_button.interactable=!Transfers.Busy && !Crafting.Busy;
        e.m_icon.enabled=item!=null;e.m_amount.enabled=item!=null && item.m_shared.m_maxStackSize>1;
        e.m_quality.enabled=item!=null && item.m_shared.m_maxQuality>1;
        e.m_equiped.enabled=false;e.m_queued.enabled=false;e.m_food.enabled=false;
        e.m_selected.SetActive(false);e.m_dropFocus.enabled=false;
        e.m_noteleport.enabled=item!=null && !item.m_shared.m_teleportable && !ZoneSystem.instance.GetGlobalKey(GlobalKeys.TeleportAll);
        e.m_durability.gameObject.SetActive(item!=null && item.m_shared.m_useDurability && item.m_durability<item.GetMaxDurability());
        var binding=go.transform.Find("binding");if(binding)binding.gameObject.SetActive(false);
        if(item!=null)
        {
            e.m_icon.sprite=item.GetIcon();e.m_icon.color=Color.white;
            e.m_amount.text=$"{item.m_stack}/{item.m_shared.m_maxStackSize}";e.m_quality.text=item.m_quality.ToString();
            if(item.m_shared.m_useDurability){e.m_durability.SetValue(item.GetDurabilityPercentage());e.m_durability.ResetColor();}
            AccessTools.Method(typeof(InventoryGrid),"CreateItemTooltip").Invoke(grid,new object[]{item,e.m_tooltip});
        }
        else e.m_tooltip.Set("","",null);
        // Slots are views of real chest stacks, never a synthetic inventory to mutate.
        var expected=item==null?null:Capture(chest,item,1);
        var input=go.GetComponentInChildren<UIInputHandler>();
        input.m_onLeftDown=_=>CellClick(expected);
        input.m_onLeftClick=null;input.m_onLeftUp=null;input.m_onRightDown=_=>ClearHeld();
        input.m_onPointerEnter=null;
        var drag=go.GetComponentInChildren<UIDragHandler>();
        if(drag)
        {
            drag.m_onBeginDrag=null;drag.m_onEndDrag=null;
            drag.m_onReleasedOn=_=>
            {
                // A release is a drop target, never a second quick-move click.
                if(Traverse.Create(InventoryGui.instance).Field("m_dragItem").GetValue<ItemDrop.ItemData>()!=null)CellClick(expected);
            };
        }
        return go;
    }
    void CellClick(Selection cell)
    {
        if(!GridOpen || Transfers.Busy || Crafting.Busy || InventoryGui.instance.m_splitDialog.IsActive)return;
        var gui=Traverse.Create(InventoryGui.instance);
        var nativeItem=gui.Field("m_dragItem").GetValue<ItemDrop.ItemData>();
        var nativeInventory=gui.Field("m_dragInventory").GetValue<Inventory>();
        if(nativeItem!=null && nativeInventory==Player.m_localPlayer.GetInventory())
        {
            if(Deposit(nativeItem,gui.Field("m_dragAmount").GetValue<int>()))GuiCall("SetupDragItem",null,null,1);
            return;
        }
        if(held!=null){MoveHeldWithin(cell);return;}
        if(cell==null || !cell.Valid() || cell.Item.m_shared.m_questItem)return;
        bool shift=ZInput.GetKey(KeyCode.LeftShift) || ZInput.GetKey(KeyCode.RightShift);
        bool control=ZInput.GetKey(KeyCode.LeftControl) || ZInput.GetKey(KeyCode.RightControl);
        if(shift && cell.Item.m_stack>1)
        {
            splitSelection=Capture(cell.Chest,cell.Item,1);
            GuiCall("ShowSplitDialog",cell.Item,cell.Chest.GetInventory());
        }
        else if(control)Transfers.Take(depot,cell.Chest,cell.Item);
        else Pick(Capture(cell.Chest,cell.Item,cell.Item.m_stack));
    }
    void Pick(Selection selected)
    {
        ClearHeld();held=selected;
        heldIcon=Instantiate(InventoryGui.instance.m_dragItemPrefab,InventoryGui.instance.transform,false);
        foreach(var graphic in heldIcon.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
        var group=heldIcon.GetComponent<CanvasGroup>()??heldIcon.AddComponent<CanvasGroup>();group.blocksRaycasts=false;
        heldIcon.transform.Find("icon").GetComponent<Image>().sprite=selected.Item.GetIcon();
        heldIcon.transform.Find("name").GetComponent<TMP_Text>().text=LocalName(selected.Item);
        heldIcon.transform.Find("amount").GetComponent<TMP_Text>().text=selected.Amount>1?selected.Amount.ToString():"";
        UITooltip.HideTooltip();
    }
    void MoveHeldWithin(Selection target)
    {
        if(!held.Valid()){ClearHeld();return;}
        if(target!=null && (!target.Valid() || target.Chest!=held.Chest))
        {Transfers.Status="Move between chests through your inventory.";return;}
        var inv=held.Chest.GetInventory();Vector2i slot;
        if(held.Chest==depot && intake.ContainsKey(held.Slot.y*inv.GetWidth()+held.Slot.x) && target!=null &&
            !intake.ContainsKey(target.Slot.y*inv.GetWidth()+target.Slot.x))
        {Transfers.Status="Keep intake separate: choose an empty slot or another intake stack.";return;}
        if(target!=null)slot=target.Slot;
        else
        {
            slot=new Vector2i(-1,-1);
            for(int n=0;n<inv.GetWidth()*inv.GetHeight();n++)
            {
                var pos=new Vector2i(n%inv.GetWidth(),n/inv.GetWidth());
                if(inv.GetItemAt(pos.x,pos.y)==null && !InventoryBlock.Get(inv).IsSlotBlocked(pos)){slot=pos;break;}
            }
        }
        if(slot==held.Slot){ClearHeld();return;}
        if(Transfers.MoveWithin(depot,held.Chest,held.Item,slot,held.Amount))
        {
            int sourceSlot=held.Slot.y*inv.GetWidth()+held.Slot.x;
            if(held.Chest==depot && intake.ContainsKey(sourceSlot))intake[slot.y*inv.GetWidth()+slot.x]=held.Key;
            ClearHeld();
        }
    }
    void ClearHeld(){held=null;if(heldIcon)Destroy(heldIcon);heldIcon=null;}
    void CancelGridInteraction()
    {
        ClearHeld();
        if(splitSelection!=null && InventoryGui.instance)GuiCall("OnSplitCancel");
        splitSelection=null;UITooltip.HideTooltip();
    }
    void UpdateGridInteraction()
    {
        if(held==null)return;
        if(!held.Valid() || ZInput.GetMouseButton(1)){ClearHeld();return;}
        if(heldIcon)heldIcon.transform.position=ZInput.pointerPosition;
    }
    internal static bool PlayerClick(InventoryGrid grid,ItemDrop.ItemData item,Vector2i pos,InventoryGrid.Modifier mod)
    {
        if(!GridOpen || grid!=InventoryGui.instance.m_playerGrid)return true;
        if(instance.held!=null)
        {
            if(Transfers.Busy || Crafting.Busy)return false;
            var selected=instance.held;
            if(!selected.Valid()){instance.ClearHeld();return false;}
            if(Transfers.Take(instance.depot,selected.Chest,selected.Item,selected.Amount,pos))instance.ClearHeld();
            return false;
        }
        if(mod==InventoryGrid.Modifier.Move)
        {
            if(Traverse.Create(InventoryGui.instance).Field("m_dragItem").GetValue<ItemDrop.ItemData>()!=null)return true;
            // Without a vanilla currentContainer, the original handler drops items
            // on the ground. Always intercept quick-move while the depot is open.
            if(item!=null)instance.Deposit(item);
            return false;
        }
        return true;
    }
    internal static bool PlayerRelease(InventoryGrid grid,ItemDrop.ItemData item,Vector2i pos)
    {
        if(!GridOpen || instance.held==null)return true;
        if(!grid){instance.ClearHeld();return false;}
        return PlayerClick(grid,item,pos,InventoryGrid.Modifier.Select);
    }
    internal static bool SplitAccepted()
    {
        if(!instance || instance.splitSelection==null)return true;
        var selected=instance.splitSelection;
        selected.Amount=(int)InventoryGui.instance.m_splitDialog.m_splitSlider.value;
        GuiCall("OnSplitCancel");instance.splitSelection=null;
        if(GridOpen && selected.Valid())instance.Pick(selected);
        return false;
    }
    internal static void SplitCancelled(){if(instance)instance.splitSelection=null;}
    internal static bool DropOutside()
    {
        if(!GridOpen || instance.held==null)return true;
        instance.ClearHeld();return false;
    }
}

[HarmonyPatch(typeof(InventoryGui),"OnSelectedItem")]
static class DepotPlayerClick
{
    [HarmonyPriority(Priority.First)]
    static bool Prefix(InventoryGrid grid,ItemDrop.ItemData item,Vector2i pos,InventoryGrid.Modifier mod)=>DepotUi.PlayerClick(grid,item,pos,mod);
}
[HarmonyPatch(typeof(InventoryGui),"OnReleasedItem")]
static class DepotPlayerRelease
{
    [HarmonyPriority(Priority.First)]
    static bool Prefix(InventoryGrid grid,ItemDrop.ItemData item,Vector2i pos)=>DepotUi.PlayerRelease(grid,item,pos);
}
[HarmonyPatch(typeof(InventoryGui),"OnSplitOk")]
static class DepotSplitAccepted {static bool Prefix()=>DepotUi.SplitAccepted();}
[HarmonyPatch(typeof(InventoryGui),"OnSplitCancel")]
static class DepotSplitCancelled {static void Postfix()=>DepotUi.SplitCancelled();}
[HarmonyPatch(typeof(InventoryGui),"OnDropOutside")]
static class DepotOutsideDrop {static bool Prefix()=>DepotUi.DropOutside();}
