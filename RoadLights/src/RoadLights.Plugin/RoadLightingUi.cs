using System;
using System.Linq;
using HarmonyLib;
using Jotunn.Managers;
using RoadLights.Core;
using UnityEngine;
using UnityEngine.UI;
namespace RoadLights;

public sealed class RoadLightingUi:MonoBehaviour
{
    GameObject tabs,panel;
    BuildUi owner;
    RectTransform pieceView;
    float originalTop;
    bool headerReserved;
    bool showing;
    string rendered;
    float nextRefresh;
    void LateUpdate()
    {
        var player=Player.m_localPlayer;
        var ui=Hud.instance?Hud.instance.m_buildUi:null;
        bool visible=player && ui && ui.gameObject.activeInHierarchy
            && RoadPlacement.HoldingHoe(player);
        if(!visible){ReserveHeader(false);if(tabs)tabs.SetActive(false);if(panel)panel.SetActive(false);return;}
        if(owner!=ui || !tabs)Build(ui);
        ReserveHeader(true);
        tabs.SetActive(true);panel.SetActive(showing);
        if(showing && Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+0.5f;Refresh(player);}
    }
    void OnDestroy(){ReserveHeader(false);if(tabs)Destroy(tabs);if(panel)Destroy(panel);}
    void ReserveHeader(bool reserve)
    {
        if(!pieceView || (!reserve && !headerReserved))return;
        // Change only the top inset, preserving native horizontal/tag-list layout
        // and the bottom edge. Repeated updates must not accumulate the offset.
        var top=pieceView.offsetMax;
        top.y=originalTop-(reserve?54f:0f);
        pieceView.offsetMax=top;
        headerReserved=reserve;
    }
    static RectTransform Rect(GameObject go,Transform parent,float x,float y,float width,float height)
    {
        go.transform.SetParent(parent,false);
        var r=go.GetComponent<RectTransform>()??go.AddComponent<RectTransform>();
        r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);return r;
    }
    static GameObject Button(Transform parent,string text,float x,float y,float width,float height,Action action)
    {
        var go=GUIManager.Instance.CreateButton(text,parent,new Vector2(0,1),new Vector2(0,1),Vector2.zero,width,height);
        Rect(go,parent,x,y,width,height);
        var button=go.GetComponent<Button>();button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>action());
        var label=go.GetComponentInChildren<Text>();if(label){label.fontSize=14;label.raycastTarget=false;}
        return go;
    }
    static void Label(Transform parent,string text,float x,float y,float width,float height)
    {
        var m=GUIManager.Instance;
        var go=m.CreateText(text,parent,new Vector2(0,1),new Vector2(0,1),Vector2.zero,m.AveriaSerifBold,16,m.ValheimOrange,true,Color.black,width,height,false);
        Rect(go,parent,x,y,width,height);go.GetComponent<Text>().raycastTarget=false;
    }
    void Build(BuildUi ui)
    {
        ReserveHeader(false);
        if(tabs)Destroy(tabs);if(panel)Destroy(panel);
        owner=ui;rendered=null;
        var view=AccessTools.Field(typeof(BuildUi),"m_pieceView").GetValue(ui) as RectTransform;
        pieceView=view;originalTop=view.offsetMax.y;
        tabs=new GameObject("RoadLightsHoeTabs",typeof(RectTransform));
        // The native content moves down 54 px. Tabs occupy that reserved row,
        // 8 px below the original content top and 12 px above the new content.
        Rect(tabs,view,0,-46,400,34);
        Button(tabs.transform,"Hoe",0,0,130,34,()=>{showing=false;panel.SetActive(false);});
        Button(tabs.transform,"Road lighting",136,0,190,34,()=>{showing=true;rendered=null;});
        panel=GUIManager.Instance.CreateWoodpanel(view,new Vector2(0,1),new Vector2(0,1),Vector2.zero,620,410,false);
        panel.name="RoadLightsHoePanel";Rect(panel,view,0,0,620,410);
        panel.SetActive(false);
    }
    void Refresh(Player player)
    {
        var recipes=LightingPolicy.Prefabs.Select(RoadPlacement.Recipe).Where(p=>RoadPlacement.Known(player,p)).ToArray();
        string signature=Plugin.RoadSelection.Value+"|"+string.Join("|",recipes.Select(p=>p.name));
        if(signature==rendered)return;rendered=signature;
        // Leave the woodpanel background intact; all dynamic content lives in a child.
        var old=panel.transform.Find("Choices");if(old){old.gameObject.SetActive(false);Destroy(old.gameObject);}
        var content=new GameObject("Choices",typeof(RectTransform));Rect(content,panel.transform,0,0,620,410);
        Label(content.transform,"Pathen road lighting",16,12,580,28);
        Label(content.transform,"Free lights • 40 ft spacing • random roadside",16,42,580,26);
        Choice(content.transform,"","Off",null,0);
        for(int i=0;i<recipes.Length;i++)
            Choice(content.transform,recipes[i].name,Localization.instance.Localize(recipes[i].m_name),recipes[i].m_icon,i+1);
        if(recipes.Length==0)Label(content.transform,"Discover light recipes to add them here.",16,145,580,40);
    }
    void Choice(Transform parent,string name,string title,Sprite icon,int i)
    {
        bool selected=Plugin.RoadSelection.Value==name;
        var button=Button(parent,(selected?"✓ ":"")+title,16+(i%3)*198,80+(i/3)*60,190,54,()=>
        {Plugin.RoadSelection.Value=name;rendered=null;});
        if(selected){var colors=button.GetComponent<Button>().colors;colors.normalColor=new Color(0.65f,0.9f,0.55f);button.GetComponent<Button>().colors=colors;}
        if(icon)
        {
            var image=new GameObject("Icon",typeof(RectTransform),typeof(Image));Rect(image,button.transform,5,11,30,30);
            image.GetComponent<Image>().sprite=icon;image.GetComponent<Image>().preserveAspect=true;image.GetComponent<Image>().raycastTarget=false;
            var text=button.GetComponentInChildren<Text>();if(text)text.rectTransform.offsetMin=new Vector2(38,text.rectTransform.offsetMin.y);
        }
    }
}
