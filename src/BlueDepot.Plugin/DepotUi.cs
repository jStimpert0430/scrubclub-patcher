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

internal sealed class DepotUi:MonoBehaviour
{
    static DepotUi instance;
    Container depot;GameObject panel;Transform content;Text heading,footer; InputField searchField; float closingAt=-1; string lastRows="";
    Category? category;bool dropBox;int page;float refresh;string search="";
    readonly List<GameObject> rows=new List<GameObject>();
    Button dropMaterialsButton;
    bool bulkRunning;
    readonly List<(Button Button,Category? Category,bool Drop)> categoryButtons=new List<(Button,Category?,bool)>();
    const int PageSize=5;
    internal static bool EditingSearch => instance && instance.panel && instance.searchField && instance.searchField.isFocused;
    void Awake(){instance=this;}
    internal static void Open(Container chest)
    {
        if(!instance)return;
        instance.DisposePanel();instance.depot=chest;
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
        closingAt=Time.unscaledTime+0.35f;
        if(panel)panel.GetComponent<CanvasGroup>().interactable=false;
        StopAllCoroutines();bulkRunning=false;
    }
    void DisposePanel()
    {
        if(panel)Destroy(panel);
        panel=null;depot=null;rows.Clear();categoryButtons.Clear();page=0;search="";dropBox=false;category=null;closingAt=-1;lastRows="";
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
        panel=GUIManager.Instance.CreateWoodpanel(gui.m_player,new Vector2(0,0),new Vector2(0,0),Vector2.zero,610,416,false);
        panel.name="BlueDepotUI";
        var r=panel.GetComponent<RectTransform>();r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(0,-8);
        panel.AddComponent<CanvasGroup>();
        heading=Label(panel.transform,"Blue Depot",14,8,500,42,17);
        Button(panel.transform,"Close",526,8,70,28,Close);
        var tabs=new List<(string Title,Category? Category,bool Drop)>{("All",null,false),("Drop box",null,true)};
        tabs.AddRange(Enum.GetValues(typeof(Category)).Cast<Category>().Select(c=>(c.ToString(),(Category?)c,false)));
        for(int i=0;i<tabs.Count;i++)
        {
            var tab=tabs[i];
            var tabButton=Button(panel.transform,tab.Title,14+(i%6)*98,54+(i/6)*30,94,27,()=>Select(tab.Category,tab.Drop)).GetComponent<Button>();
            categoryButtons.Add((tabButton,tab.Category,tab.Drop));
        }
        var inputGo=GUIManager.Instance.CreateInputField(panel.transform,new Vector2(0,1),new Vector2(0,1),Vector2.zero,
            InputField.ContentType.Standard,"Search items…",14,360,28);
        Rect(inputGo,panel.transform,14,120,360,28);searchField=inputGo.GetComponent<InputField>();
        searchField.onValueChanged.AddListener(v=>{search=v;page=0;Refresh();});
        dropMaterialsButton=Button(panel.transform,"Drop materials",390,120,206,28,()=>{if(bulkRunning || Transfers.Busy)return;dropBox=true;category=null;page=0;StartCoroutine(DepositMaterials());Refresh();}).GetComponent<Button>();
        var contentGo=new GameObject("Rows",typeof(RectTransform));Rect(contentGo,panel.transform,14,156,582,175);content=contentGo.transform;
        Button(panel.transform,"Previous",14,338,92,28,()=>{page=Math.Max(0,page-1);Refresh();});
        Button(panel.transform,"Next",112,338,92,28,()=>{page++;Refresh();});
        footer=Label(panel.transform,"",14,371,582,40,13);
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
        BulkDropRules.Eligible(Storage.CategoryOf(item),item.m_equipped,item.m_gridPos.y,item.m_stack,
            InventoryBlock.Get(inventory).IsSlotBlocked(item.m_gridPos));
    void UpdateButtonStates(Inventory inventory)
    {
        foreach(var tab in categoryButtons)Tint(tab.Button,CategoryColor(tab.Category,tab.Drop),tab.Drop==dropBox && tab.Category==category);
        dropMaterialsButton.interactable=!bulkRunning && !Transfers.Busy && inventory.GetAllItems().Any(i=>EligibleMaterial(i,inventory));
        Tint(dropMaterialsButton,new Color(.22f,.72f,.30f));
    }
    void Select(Category? tab,bool drop){category=tab;dropBox=drop;page=0;Refresh();}
    void Refresh()
    {
        if(!panel || !Storage.CanAccess(depot))return;

        var chests=new[]{depot}.Concat(Storage.Nearby(depot)).ToArray();
        var view=new StorageView(Storage.Snapshot(depot,depot),chests.Skip(1).Select(c=>Storage.Snapshot(c,depot)),Plugin.Radius.Value);
        heading.text=$"Blue Depot — {(dropBox?"Drop box":category?.ToString()??"All items")}\n{view.Occupied} / {view.Capacity} slots · {chests.Length} chests · {Plugin.Radius.Value:0} m";
        var inventory=Player.m_localPlayer.GetInventory();
        UpdateButtonStates(inventory);
        var listed=dropBox
            ? inventory.GetAllItems().Where(i=>!i.m_equipped && LocalName(i).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)
                .Select(i=>(Chest:(Container)null,Item:i)).ToList()
            : view.Rows(category,search).Select(row=>{
                var c=chests.First(x=>Storage.Id(x)==row.Chest.Id);var pos=Storage.Position(row.Item.Slot,c.GetInventory());
                return (Chest:c,Item:c.GetInventory().GetItemAt(pos.x,pos.y));}).Where(x=>x.Item!=null).ToList();
        page=Math.Min(page,Math.Max(0,(listed.Count-1)/PageSize));
        footer.text=$"Page {page+1}/{Math.Max(1,(listed.Count+PageSize-1)/PageSize)} · "+(dropBox?"Click to deposit a stack.":"Click to take a stack.")+"\n"+Transfers.Status;
        var signature=dropBox+"/"+category+"/"+page+"/"+string.Join(";",listed.Skip(page*PageSize).Take(PageSize).Select(row=>
            (row.Chest?Storage.Id(row.Chest):"player")+":"+row.Item.m_gridPos+":"+Storage.Key(row.Item)+":"+row.Item.m_stack));
        if(signature==lastRows)
        {
            foreach(var row in rows){var button=row.GetComponent<Button>();if(button)button.interactable=!Transfers.Busy;}
            return;
        }
        lastRows=signature;
        foreach(var row in rows){row.SetActive(false);Destroy(row);}rows.Clear();
        int index=0;
        foreach(var row in listed.Skip(page*PageSize).Take(PageSize))
        {
            var c=row.Chest;var item=row.Item;
            var button=Button(content,"",0,index*35,582,32,()=>{if(dropBox)Deposit(item);else Transfers.Take(depot,c,item);});rows.Add(button);
            button.GetComponent<Button>().interactable=!Transfers.Busy;
            var icon=new GameObject("ItemIcon",typeof(RectTransform),typeof(Image));Rect(icon,button.transform,4,2,28,28);icon.GetComponent<Image>().sprite=item.GetIcon();icon.GetComponent<Image>().raycastTarget=false;
            Label(button.transform,$"{LocalName(item)} ×{item.m_stack}"+(item.m_quality>1?$" (quality {item.m_quality})":""),38,5,360,25,15);
            Label(button.transform,dropBox?"Deposit":(c==depot?"Drop box · Take":"Nearby · Take"),405,7,171,24,13);
            index++;
        }
        if(index==0){var empty=Label(content,dropBox?"No unequipped items to deposit.":"No stored items in this category.",4,10,570,40);rows.Add(empty.gameObject);}
    }
    static string LocalName(ItemDrop.ItemData item)=>Localization.instance.Localize(item.m_shared.m_name);
    void Deposit(ItemDrop.ItemData item)
    {
        if(!Storage.ValidSession(depot) || item.m_equipped)return;
        var inv=Player.m_localPlayer.GetInventory();if(!inv.ContainsItem(item))return;
        var placement=Routing.Next(Storage.Item(item,inv),"player",new[]{Storage.Snapshot(depot,depot)});
        if(placement==null){Transfers.Status="Drop box full. Wait for sorting or withdraw items.";return;}
        Transfers.Deposit(null,depot,item,Storage.Position(placement.Slot,depot.GetInventory()),placement.Amount);
    }
    System.Collections.IEnumerator DepositMaterials()
    {
        // Do not deposit equipped items or hotbar slots as part of a bulk operation.
        if(bulkRunning)yield break;
        bulkRunning=true;
        var inventory=Player.m_localPlayer.GetInventory();
        var items=inventory.GetAllItems().Where(i=>EligibleMaterial(i,inventory)).ToArray();
        try
        {
        foreach(var item in items)
        {
            if(!panel || !Storage.ValidSession(depot))yield break;
            if(Transfers.Busy)yield break;
            if(!inventory.ContainsItem(item) || !EligibleMaterial(item,inventory))continue;
            Deposit(item);yield return new WaitForSecondsRealtime(.15f);
            while(Transfers.Busy){if(!panel)yield break;yield return null;}
        }
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
