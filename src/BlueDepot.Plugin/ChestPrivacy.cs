using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Jotunn.Managers;
namespace BlueDepot;

// This is an automation opt-out, deliberately separate from Container.m_privacy.
internal sealed class ChestPrivacy:MonoBehaviour
{
    const string Key="BlueDepot.Private",Rpc="BlueDepot.SetPrivate";
    Container chest;ZNetView view;
    static readonly Func<Container,long,bool> checkAccess=AccessTools.MethodDelegate<Func<Container,long,bool>>(AccessTools.Method(typeof(Container),"CheckAccess"));
    static readonly Func<PrivateArea,bool> enabledArea=AccessTools.MethodDelegate<Func<PrivateArea,bool>>(AccessTools.Method(typeof(PrivateArea),"IsEnabled"));
    static readonly Func<PrivateArea,Vector3,float,bool> inside=AccessTools.MethodDelegate<Func<PrivateArea,Vector3,float,bool>>(AccessTools.Method(typeof(PrivateArea),"IsInside"));
    static readonly Func<PrivateArea,long,bool> permitted=AccessTools.MethodDelegate<Func<PrivateArea,long,bool>>(AccessTools.Method(typeof(PrivateArea),"IsPermitted"));
    static readonly System.Reflection.FieldInfo areas=AccessTools.Field(typeof(PrivateArea),"m_allAreas");
    internal static bool IsPrivate(Container c)=>Storage.View(c) && Storage.View(c).IsValid() && Storage.View(c).GetZDO().GetBool(Key,false);
    void Awake(){chest=GetComponent<Container>();view=Storage.View(chest);if(view && view.IsValid())view.Register<bool>(Rpc,Receive);}
    internal static void Request(Container c,bool value)
    {
        if(!Storage.ValidSession(c) || Transfers.Busy || Crafting.Busy)return;
        var component=c.GetComponent<ChestPrivacy>();if(!component)return;
        if(component.view.IsOwner())component.Receive(ZNet.GetUID(),value);
        else component.view.InvokeRPC(Rpc,value);
    }
    void Receive(long sender,bool value)
    {
        if(!view || !view.IsValid() || !view.IsOwner() || !ZNet.instance)return;
        // Derive player identity and position from the sender's character, never
        // an ID/name/position supplied with the checkbox request.
        var player=ZNet.instance.GetAllCharacterZDOS().Concat(ZNet.instance.GetPlayerList().Select(p=>ZDOMan.instance.GetZDO(p.m_characterID)))
            .FirstOrDefault(z=>z!=null && z.GetOwner()==sender);
        if(player==null || Vector3.Distance(player.GetPosition(),chest.transform.position)>5f)return;
        long id=player.GetLong(ZDOVars.s_playerID,0);
        if(id==0 || !checkAccess(chest,id))return;
        foreach(var area in (List<PrivateArea>)areas.GetValue(null))
            if(area && enabledArea(area) && inside(area,chest.transform.position,0) &&
                area.GetComponent<Piece>().GetCreator()!=id && !permitted(area,id))return;
        view.GetZDO().Set(Key,value);
        // Cancel queued automation rather than unexpectedly running it when the
        // chest is later made public. Contents themselves are untouched.
        if(value){view.GetZDO().Set("BlueDepot.SortQueue","");view.GetZDO().Set("BlueDepot.Consolidate","");}
    }
}

internal sealed class PrivacyCheckbox:MonoBehaviour
{
    Container chest;Toggle toggle;float pendingUntil;
    internal static void Create(Transform parent,Container chest,Vector2 position,float width)
    {
        var go=GUIManager.Instance.CreateWoodpanel(parent,new Vector2(0,0),new Vector2(0,0),Vector2.zero,width,36,false);
        go.name="BlueDepotPrivacy";var rect=go.GetComponent<RectTransform>();rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;
        var component=go.AddComponent<PrivacyCheckbox>();component.chest=chest;
        var bg=new GameObject("Checkbox",typeof(RectTransform),typeof(Image));bg.transform.SetParent(go.transform,false);
        var br=bg.GetComponent<RectTransform>();br.anchorMin=br.anchorMax=new Vector2(0,.5f);br.sizeDelta=new Vector2(22,22);br.anchoredPosition=new Vector2(22,0);
        bg.GetComponent<Image>().color=new Color(.16f,.11f,.07f);
        // Separate edge images stay visible whether checked, hovered or disabled.
        foreach(var edge in new[]{new Vector4(0,0,1,0),new Vector4(0,1,1,1),new Vector4(0,0,0,1),new Vector4(1,0,1,1)})
        {
            var border=new GameObject("Checkbox border",typeof(RectTransform),typeof(Image));border.transform.SetParent(bg.transform,false);
            var r=border.GetComponent<RectTransform>();r.anchorMin=new Vector2(edge.x,edge.y);r.anchorMax=new Vector2(edge.z,edge.w);r.sizeDelta=edge.x==edge.z?new Vector2(2,0):new Vector2(0,2);r.anchoredPosition=Vector2.zero;
            var image=border.GetComponent<Image>();image.color=new Color(.88f,.69f,.37f);image.raycastTarget=false;
        }
        var tick=new GameObject("Checked",typeof(RectTransform),typeof(Image));tick.transform.SetParent(bg.transform,false);
        tick.GetComponent<RectTransform>().sizeDelta=new Vector2(14,14);tick.GetComponent<Image>().color=new Color(.4f,.85f,.4f);
        var m=GUIManager.Instance;var text=m.CreateText("Private — exclude from automation",go.transform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(40,0),m.AveriaSerifBold,14,m.ValheimOrange,true,Color.black,Mathf.Max(0,width-52),28,false);
        text.GetComponent<RectTransform>().pivot=new Vector2(0,.5f);text.GetComponent<Text>().raycastTarget=false;text.GetComponent<Text>().resizeTextForBestFit=true;text.GetComponent<Text>().resizeTextMinSize=10;text.GetComponent<Text>().resizeTextMaxSize=14;
        component.toggle=go.AddComponent<Toggle>();component.toggle.targetGraphic=bg.GetComponent<Image>();component.toggle.graphic=tick.GetComponent<Image>();
        component.toggle.SetIsOnWithoutNotify(ChestPrivacy.IsPrivate(chest));
        component.toggle.onValueChanged.AddListener(value=>{ChestPrivacy.Request(component.chest,value);component.pendingUntil=Time.unscaledTime+1;});
    }
    void Update()
    {
        if(!chest){Destroy(gameObject);return;}
        toggle.interactable=!Transfers.Busy && !Crafting.Busy && Time.unscaledTime>=pendingUntil;
        if(Time.unscaledTime>=pendingUntil)toggle.SetIsOnWithoutNotify(ChestPrivacy.IsPrivate(chest));
    }
}

internal sealed class ChestPrivacyUi:MonoBehaviour
{
    static readonly AccessTools.FieldRef<InventoryGui,Container> current=AccessTools.FieldRefAccess<InventoryGui,Container>("m_currentContainer");
    Container shown;GameObject panel;
    void Update()
    {
        var gui=InventoryGui.instance;
        var c=gui && InventoryGui.IsVisible()?current(gui):null;
        if(c==shown && (c==null || panel))return;
        if(panel)Destroy(panel);panel=null;shown=c;
        if(!c || Plugin.IsDepot(c) || !Storage.View(c) || !Storage.View(c).IsValid())return;
        PrivacyCheckbox.Create(gui.m_container,c,new Vector2(8,-8),Mathf.Max(40,gui.m_container.rect.width-16));
        panel=gui.m_container.Find("BlueDepotPrivacy").gameObject;
    }
    void OnDestroy(){if(panel)Destroy(panel);}
}
