using System;
using Jotunn.Managers;
using SleepVote.Core;
using UnityEngine;
using UnityEngine.UI;
namespace SleepVote;
public sealed class VoteHud:MonoBehaviour
{
    GameObject panel;
    Text label;
    internal string Rows="",Reason="";
    internal SleepPhase Phase;
    internal float Remaining,Received,Until;
    internal void Clear(){Until=0;if(panel)panel.SetActive(false);}
    void Update()
    {
        if(!Player.m_localPlayer || Time.unscaledTime>=Until || Plugin.Instance?.HideVoteUi==true)
        {if(panel)panel.SetActive(false);return;}
        if(!panel)
        {
            if(!GUIManager.CustomGUIFront)return;
            var m=GUIManager.Instance;
            panel=m.CreateWoodpanel(GUIManager.CustomGUIFront.transform,new Vector2(1,1),new Vector2(1,1),Vector2.zero,420,300,false);
            var rect=panel.GetComponent<RectTransform>();rect.pivot=new Vector2(1,1);rect.anchoredPosition=new Vector2(-24,-260);
            var text=m.CreateText("",panel.transform,new Vector2(0,1),new Vector2(0,1),Vector2.zero,m.AveriaSerifBold,17,m.ValheimOrange,true,Color.black,384,268,false);
            var tr=text.GetComponent<RectTransform>();tr.pivot=new Vector2(0,1);tr.anchoredPosition=new Vector2(18,-16);
            label=text.GetComponent<Text>();label.supportRichText=false;label.alignment=TextAnchor.UpperLeft;
            foreach(var graphic in panel.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
        }
        bool ticking=Phase==SleepPhase.WaitingForVotes || Phase==SleepPhase.GetToBed;
        int seconds=(int)Math.Ceiling(Math.Max(0,Remaining-(ticking?Time.unscaledTime-Received:0)));
        string heading=Phase==SleepPhase.WaitingForCombat?"Waiting for votes, player in combat":
            Phase==SleepPhase.GetToBed?"Get to bed — "+seconds+"s":
            Phase==SleepPhase.Overridden?"Overridden — "+Reason:Phase==SleepPhase.Finished?"Night ended / sleep started":"Waiting for votes — "+seconds+"s";
        if(Phase==SleepPhase.WaitingForCombat)heading+="\nCountdown paused ("+seconds+"s remaining)";
        label.text="Sleep vote\n"+heading+"\n\n"+Rows;
        int lines=label.text.Split('\n').Length;
        float height=Mathf.Clamp(lines*24+32,180,700);
        panel.GetComponent<RectTransform>().sizeDelta=new Vector2(420,height);
        label.rectTransform.sizeDelta=new Vector2(384,height-32);
        panel.SetActive(true);
    }
    void OnDestroy(){if(panel)Destroy(panel);}
}
