using System;
using System.Collections.Generic;
using System.Linq;
using BlueDepot.Core;
using Jotunn.Managers;
using MultiUserChest;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace BlueDepot;

internal sealed partial class DepotUi:MonoBehaviour
{
    static DepotUi instance;
    Container depot;GameObject panel;Transform content;Text heading,footer; InputField searchField; float closingAt=-1; string lastRows="";
    Category? category;bool dropBox,internalBox;float refresh;string search="";
    readonly List<GameObject> rows=new List<GameObject>();
    Button dropMaterialsButton;ScrollRect itemScroll;RectTransform scrollContent;
    bool bulkRunning;
    internal static bool BulkRunning=>instance && instance.bulkRunning;
    readonly List<(Button Button,Category? Category,bool Drop,bool Internal,GameObject Outline)> categoryButtons=new List<(Button,Category?,bool,bool,GameObject)>();
    internal static bool EditingSearch => instance && instance.panel && instance.searchField && instance.searchField.isFocused;
    void Awake(){instance=this;}
    internal static void Open(Container chest)
    {
        if(!instance)return;
        instance.DisposePanel();instance.depot=chest;instance.dropBox=true;
        try
        {
            InventoryGui.instance.Show(null);
            instance.Build();instance.Refresh();
        }
        catch(Exception error){instance.DisposePanel();InventoryGui.instance.Hide();Plugin.Instance.LoggerForTransfers(error);}
    }
    void Update()
    {
        if(!panel)return;
        UpdateGridInteraction();
        var gui=InventoryGui.instance;
        if(!gui){DisposePanel();return;}
        if(closingAt>=0){if(Time.unscaledTime>=closingAt)DisposePanel();return;}
        if(!Storage.ValidSession(depot)){Close();return;}
        if(!gui.GetComponent<Animator>().GetBool("visible")){BeginClose();return;}
        if(EditingSearch && Input.GetKeyDown(KeyCode.Escape)){searchField.DeactivateInputField();Close();return;}
        if(Time.unscaledTime>refresh){refresh=Time.unscaledTime+0.25f;Refresh();}
    }
    void OnDestroy(){DisposePanel();}
    void Close(){if(InventoryGui.instance)InventoryGui.instance.Hide();BeginClose();}
    void BeginClose()
    {
        if(closingAt>=0)return;
        FlushIntake();CancelGridInteraction();
        closingAt=Time.unscaledTime+0.35f;
        if(panel)panel.GetComponent<CanvasGroup>().interactable=false;
        StopAllCoroutines();bulkRunning=false;
    }
    void DisposePanel()
    {
        FlushIntake();CancelGridInteraction();
        if(panel)Destroy(panel);
        panel=null;depot=null;rows.Clear();categoryButtons.Clear();search="";dropBox=false;internalBox=false;category=null;closingAt=-1;lastRows="";
        StopAllCoroutines();bulkRunning=false;
    }
    static RectTransform Rect(GameObject go,Transform parent,float x,float y,float width,float height)
    {
        go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>()??go.AddComponent<RectTransform>();
        r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);return r;
    }
    static Text Label(Transform parent,string value,float x,float y,float width,float height,int size=16)
    {
        var manager=GUIManager.Instance;
        if(!manager.AveriaSerifBold)throw new InvalidOperationException("Valheim UI font has not loaded");
        var go=manager.CreateText(value,parent,new Vector2(0,1),new Vector2(0,1),Vector2.zero,
            manager.AveriaSerifBold,size,manager.ValheimOrange,true,Color.black,width,height,false);
        Rect(go,parent,x,y,width,height);
        var text=go.GetComponent<Text>();text.raycastTarget=false;text.supportRichText=false;
        text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
        return text;
    }
    static GameObject Button(Transform parent,string title,float x,float y,float width,float height,Action action)
    {
        var go=GUIManager.Instance.CreateButton(title,parent,new Vector2(0,1),new Vector2(0,1),Vector2.zero,width,height);
        Rect(go,parent,x,y,width,height);
        var button=go.GetComponent<Button>();button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>action());
        var text=go.GetComponentInChildren<Text>();text.fontSize=14;text.raycastTarget=false;
        return go;
    }
    void Build()
    {
        // A child of the animated player panel follows the exact vanilla slide-in/out.
        // Show(null) keeps inventory, character stats and crafting available, without opening
        // the depot as a vanilla 10x10 grid or replacing its physical inventory.
        var gui=InventoryGui.instance;
        panel=GUIManager.Instance.CreateWoodpanel(gui.m_player,new Vector2(0,0),new Vector2(0,0),Vector2.zero,634,542,false);
        panel.name="BlueDepotUI";
        var r=panel.GetComponent<RectTransform>();r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(-10,-16);
        panel.AddComponent<CanvasGroup>();
        heading=Label(panel.transform,"Blue Depot",14,8,500,42,17);
        Button(panel.transform,"Close",526,8,70,28,Close);
        var tabs=new List<(string Title,Category? Category,bool Drop,bool Internal)>{("All",null,false,false),("Drop box",null,true,false),("Internal",null,false,true)};
        tabs.AddRange(Enum.GetValues(typeof(Category)).Cast<Category>().Select(c=>(c.ToString(),(Category?)c,false,false)));
        for(int i=0;i<tabs.Count;i++)
        {
            var tab=tabs[i];
            // Storage views have their own three-column row, above the category grid.
            bool storageView=i<3;
            int categoryIndex=i-3;
            float x=storageView?14+i*196:14+(categoryIndex%6)*98;
            float y=storageView?54:94+(categoryIndex/6)*30;
            var tabButton=Button(panel.transform,tab.Title,x,y,storageView?190:94,27,()=>Select(tab.Category,tab.Drop,tab.Internal)).GetComponent<Button>();
            categoryButtons.Add((tabButton,tab.Category,tab.Drop,tab.Internal,CreateTabOutline(tabButton)));
        }
        var inputGo=GUIManager.Instance.CreateInputField(panel.transform,new Vector2(0,1),new Vector2(0,1),Vector2.zero,
            InputField.ContentType.Standard,"Search items…",14,360,28);
        Rect(inputGo,panel.transform,14,160,360,28);searchField=inputGo.GetComponent<InputField>();
        searchField.onValueChanged.AddListener(v=>{search=v;if(scrollContent)scrollContent.anchoredPosition=Vector2.zero;Refresh();});
        dropMaterialsButton=Button(panel.transform,"Drop materials",390,160,206,28,()=>{if(bulkRunning || Transfers.Busy)return;dropBox=true;internalBox=false;category=null;StartCoroutine(DepositMaterials());Refresh();}).GetComponent<Button>();
        var viewport=new GameObject("InventoryScroll",typeof(RectTransform),typeof(Image),typeof(RectMask2D));
        var viewportRect=Rect(viewport,panel.transform,14,196,582,216);
        viewport.GetComponent<Image>().color=Color.clear;
        var contentGo=new GameObject("ItemGrid",typeof(RectTransform));scrollContent=Rect(contentGo,viewport.transform,0,0,582,216);content=contentGo.transform;
        itemScroll=viewport.AddComponent<ScrollRect>();itemScroll.viewport=viewportRect;itemScroll.content=scrollContent;
        itemScroll.horizontal=false;itemScroll.vertical=true;itemScroll.movementType=ScrollRect.MovementType.Clamped;itemScroll.scrollSensitivity=72;
        var track=new GameObject("InventoryScrollbar",typeof(RectTransform),typeof(Image),typeof(Scrollbar));
        Rect(track,panel.transform,598,196,8,216);track.GetComponent<Image>().color=new Color(.12f,.09f,.06f,.8f);
        var handle=new GameObject("Handle",typeof(RectTransform),typeof(Image));
        Rect(handle,track.transform,0,0,8,40);handle.GetComponent<Image>().color=new Color(.65f,.45f,.22f);
        var scrollbar=track.GetComponent<Scrollbar>();scrollbar.handleRect=handle.GetComponent<RectTransform>();scrollbar.targetGraphic=handle.GetComponent<Image>();
        scrollbar.direction=Scrollbar.Direction.BottomToTop;itemScroll.verticalScrollbar=scrollbar;
        itemScroll.onValueChanged.AddListener(_=>Refresh());
        Button(panel.transform,"Sort",14,422,120,28,SortControlled);
        Label(panel.transform,"Scroll to browse · Items ordered by ID",148,427,440,24,13);
        footer=Label(panel.transform,"",14,457,582,40,13);
        PrivacyCheckbox.Create(panel.transform,depot,new Vector2(14,42),582);
    }
    static GameObject CreateTabOutline(Button button)
    {
        var outline=new GameObject("SelectedTabOutline",typeof(RectTransform));
        var root=outline.GetComponent<RectTransform>();root.SetParent(button.transform,false);
        root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
        // Separate border graphics keep selection visible through hover/press tints.
        // Ignore pointer events so every edge still clicks the underlying button.
        foreach(var edge in new[]{"Top","Bottom","Left","Right"})
        {
            var go=new GameObject(edge,typeof(RectTransform),typeof(Image));
            var r=go.GetComponent<RectTransform>();r.SetParent(root,false);
            bool horizontal=edge=="Top" || edge=="Bottom";
            r.anchorMin=edge=="Top"?new Vector2(0,1):edge=="Right"?new Vector2(1,0):Vector2.zero;
            r.anchorMax=edge=="Bottom"?new Vector2(1,0):edge=="Left"?new Vector2(0,1):Vector2.one;
            r.pivot=edge=="Top" || edge=="Right"?Vector2.one:Vector2.zero;
            r.sizeDelta=horizontal?new Vector2(0,2):new Vector2(2,0);r.anchoredPosition=Vector2.zero;
            var image=go.GetComponent<Image>();image.color=new Color(1f,.82f,.35f);image.raycastTarget=false;
        }
        outline.SetActive(false);return outline;
    }
    static Color CategoryColor(Category? tab,bool drop)
    {
        if(drop)return new Color(.20f,.60f,.55f);
        if(!tab.HasValue)return new Color(.48f,.57f,.72f);
        switch(tab.Value)
        {
            case Category.Materials:return new Color(.72f,.49f,.22f);
            case Category.Weapons:return new Color(.75f,.26f,.23f);
            case Category.Armor:return new Color(.28f,.48f,.76f);
            case Category.Food:return new Color(.42f,.66f,.25f);
            case Category.Potions:return new Color(.64f,.32f,.78f);
            case Category.Ammunition:return new Color(.80f,.61f,.20f);
            case Category.Tools:return new Color(.22f,.66f,.72f);
            case Category.Trophies:return new Color(.79f,.39f,.19f);
            default:return new Color(.63f,.45f,.57f);
        }
    }
    static void Tint(Button button,Color tint,bool selected=false)
    {
        button.transition=Selectable.Transition.ColorTint;
        button.targetGraphic.color=Color.white;
        var colors=button.colors;
        colors.normalColor=selected?Color.Lerp(tint,Color.white,.25f):tint;
        colors.highlightedColor=Color.Lerp(tint,Color.white,.35f);
        colors.selectedColor=colors.normalColor;
        colors.pressedColor=Color.Lerp(tint,Color.black,.2f);
        colors.disabledColor=new Color(.36f,.36f,.36f);
        colors.colorMultiplier=1;colors.fadeDuration=.08f;button.colors=colors;
        var label=button.GetComponentInChildren<Text>();
        if(label){label.color=button.interactable?Color.white:new Color(.65f,.65f,.65f);label.fontStyle=selected?FontStyle.Bold:FontStyle.Normal;}
    }
    bool EligibleMaterial(ItemDrop.ItemData item,Inventory inventory)=>
        BulkDropRules.Eligible(item.m_shared.m_itemType==ItemDrop.ItemData.ItemType.Material?Category.Materials:Storage.CategoryOf(item),item.m_equipped,item.m_gridPos.y,item.m_stack,
            InventoryBlock.Get(inventory).IsSlotBlocked(item.m_gridPos));
    void UpdateButtonStates(Inventory inventory)
    {
        foreach(var tab in categoryButtons)
        {
            bool selected=tab.Drop==dropBox && tab.Internal==internalBox && tab.Category==category;
            Tint(tab.Button,CategoryColor(tab.Category,tab.Drop),selected);
            tab.Outline.SetActive(selected);
        }
        dropMaterialsButton.interactable=!bulkRunning && !Transfers.Busy && inventory.GetAllItems().Any(i=>EligibleMaterial(i,inventory));
        Tint(dropMaterialsButton,new Color(.22f,.72f,.30f));
    }
    void Select(Category? tab,bool drop,bool native=false){category=tab;dropBox=drop;internalBox=native;if(scrollContent)scrollContent.anchoredPosition=Vector2.zero;Refresh();}
    void Refresh()
    {
        if(!panel || !Storage.CanAccess(depot))return;

        var chests=new[]{depot}.Concat(Storage.Nearby(depot)).ToArray();
        var view=new StorageView(Storage.Snapshot(depot,depot),chests.Skip(1).Select(c=>Storage.Snapshot(c,depot)),Plugin.Radius.Value);
        heading.text=$"Blue Depot — {(dropBox?"Drop box":internalBox?"Internal":category?.ToString()??"All items")}\n{view.Occupied} / {view.Capacity} slots · {chests.Length} chests · {Plugin.Radius.Value:0} m";
        var inventory=Player.m_localPlayer.GetInventory();
        UpdateButtonStates(inventory);
        var listed=view.Rows(category,search).Where(row=>(!internalBox || row.Chest.Id==Storage.Id(depot)) && (!dropBox || (row.Chest.Id==Storage.Id(depot) && IntakeContains(row.Item.Slot,row.Item.Key)))).Select(row=>{
            var c=chests.First(x=>Storage.Id(x)==row.Chest.Id);var pos=Storage.Position(row.Item.Slot,c.GetInventory());
            return (Chest:c,Item:c.GetInventory().GetItemAt(pos.x,pos.y));}).Where(x=>x.Item!=null).ToList();
        int totalRows=Math.Max(3,(listed.Count+7)/8);
        scrollContent.sizeDelta=new Vector2(582,totalRows*72);
        int firstRow=Math.Max(0,Math.Min((int)(scrollContent.anchoredPosition.y/72),totalRows-3));
        int start=firstRow*8;
        footer.text=$"{listed.Count} stacks · Ctrl-click: move · Shift-click: split\n"+Transfers.Status;
        var signature=dropBox+"/"+internalBox+"/"+category+"/"+start+"/"+string.Join(";",listed.Skip(start).Take(40).Select(row=>
            (row.Chest?Storage.Id(row.Chest):"player")+":"+row.Item.m_gridPos+":"+Storage.Key(row.Item)+":"+row.Item.m_stack));
        if(signature==lastRows)
        {
            foreach(var row in rows){var button=row.GetComponent<Button>();if(button)button.interactable=!Transfers.Busy && !Crafting.Busy;}
            return;
        }
        lastRows=signature;
        foreach(var row in rows){row.SetActive(false);Destroy(row);}rows.Clear();
        var visible=listed.Skip(start).Take(40).ToArray();
        for(int index=0;index<Math.Min(40,totalRows*8-start);index++)
        {
            var row=index<visible.Length?visible[index]:(Chest:(Container)null,Item:(ItemDrop.ItemData)null);
            rows.Add(CreateGridCell(start+index,row.Chest,row.Item));
        }
    }
    void SortControlled()
    {
        if(Transfers.Busy || Crafting.Busy || bulkRunning || !Storage.ValidSession(depot))return;
        CancelGridInteraction();
        foreach(var chest in new[]{depot}.Concat(Storage.Nearby(depot)).Where(c=>!c.IsInUse()))
            chest.GetComponent<StorageConsolidator>()?.Request(depot);
        Transfers.Status="Stack consolidation requested. Items are displayed by category and item ID.";
        Refresh();
    }
    static string LocalName(ItemDrop.ItemData item)=>Localization.instance.Localize(item.m_shared.m_name);
    bool Deposit(ItemDrop.ItemData item,int amount=int.MaxValue,bool immediate=false)
    {
        if(Transfers.Busy || Crafting.Busy || !Storage.ValidSession(depot) || item.m_shared.m_questItem || amount<=0)return false;
        var inv=Player.m_localPlayer.GetInventory();if(!inv.ContainsItem(item) || InventoryBlock.Get(inv).IsSlotBlocked(item.m_gridPos))return false;
        if(item.m_equipped)
        {
            Player.m_localPlayer.RemoveEquipAction(item);Player.m_localPlayer.UnequipItem(item,false);
            if(item.m_equipped)return false;
        }
        var mode=DepositRules.Mode(dropBox,internalBox,immediate);
        if(mode==DepositMode.Intake)return DepositIntake(item,amount);
        var destinations=mode==DepositMode.Automatic?Storage.Nearby(depot).Where(c=>!c.IsInUse()).ToArray():new Container[0];
        var placement=Routing.Next(Storage.Item(item,inv),"player",destinations.Select(c=>Storage.Snapshot(c,depot)));
        var target=placement==null?depot:destinations.First(c=>Storage.Id(c)==placement.ChestId);
        if(placement==null)placement=Routing.Next(Storage.Item(item,inv),"player",new[]{InternalSnapshot()});
        if(placement==null){Transfers.Status="Drop box full. Wait for sorting or withdraw items.";return false;}
        Transfers.Deposit(null,target,item,Storage.Position(placement.Slot,target.GetInventory()),Math.Min(amount,placement.Amount),target==depot);
        return true;
    }
    System.Collections.IEnumerator DepositMaterials()
    {
        // Do not deposit equipped items or hotbar slots as part of a bulk operation.
        if(bulkRunning)yield break;
        bulkRunning=true;
        var inventory=Player.m_localPlayer.GetInventory();
        var sessionPanel=panel;var sessionDepot=depot;
        try
        {
        var pass=new BulkDepositPass();
        // Let existing operations release their inventory slots before taking the batch.
        float deadline=Time.realtimeSinceStartup+15;
        while(Transfers.Busy || Crafting.Busy || InventoryBlock.Get(inventory).IsAnySlotBlocked())
        {
            if(panel!=sessionPanel || !panel || Time.realtimeSinceStartup>=deadline)yield break;
            yield return null;
        }
        foreach(var item in inventory.GetAllItems().Where(i=>EligibleMaterial(i,inventory)))pass.Include(Storage.Key(item),item.m_stack);
        while(panel && panel==sessionPanel && depot==sessionDepot && Storage.ValidSession(depot))
        {
            deadline=Time.realtimeSinceStartup+15;
            while(Transfers.Busy || Crafting.Busy || InventoryBlock.Get(inventory).IsAnySlotBlocked())
            {
                if(panel!=sessionPanel || !panel || Time.realtimeSinceStartup>=deadline)yield break;
                yield return null;
            }
            if(!Storage.ValidSession(depot))yield break;
            var item=inventory.GetAllItems().FirstOrDefault(i=>EligibleMaterial(i,inventory) && pass.Allowance(Storage.Key(i))>0);
            if(item==null)break;
            string key=Storage.Key(item);
            int before=inventory.GetAllItems().Where(i=>Storage.Key(i)==key).Sum(i=>i.m_stack);
            int serial=Transfers.ReplySerial;
            if(!Deposit(item,pass.Allowance(key),true)){pass.NoSpace(key);continue;}
            deadline=Time.realtimeSinceStartup+16;
            while(Transfers.Busy)
            {
                if(panel!=sessionPanel || !panel || Time.realtimeSinceStartup>=deadline)yield break;
                yield return null;
            }
            bool confirmed=Transfers.ReplySerial!=serial;
            int after=inventory.GetAllItems().Where(i=>Storage.Key(i)==key).Sum(i=>i.m_stack);
            pass.Observe(key,before,after,confirmed || after==before);
            if(!confirmed && after<before){Transfers.Status="Deposit stopped: source changed without a transfer confirmation.";yield break;}
            // The reply can precede the destination's replicated inventory. Give it
            // time to arrive before selecting space again, especially after a decline.
            yield return new WaitForSecondsRealtime(after<before && confirmed ? .25f : 1f);
        }
        if(panel && panel==sessionPanel && !Transfers.Busy)
            Transfers.Status=inventory.GetAllItems().Any(i=>EligibleMaterial(i,inventory))
                ?"Some materials remain: storage is full or unavailable."
                :"Materials deposited.";
        }
        finally{bulkRunning=false;}
    }
}

// Typing 'e' or the inventory hotkey in search must not close the vanilla screen.
[HarmonyPatch(typeof(InventoryGui),"Update")]
static class DepotSearchInput
{
    static bool Prefix()=>!DepotUi.EditingSearch;
}
