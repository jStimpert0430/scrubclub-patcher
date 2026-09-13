using System;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using Jotunn.Utils;
using SleepVote.Core;
using UnityEngine;
namespace SleepVote;
[BepInPlugin("scrubclub.sleepvote","Sleep Vote","0.1.0")]
[BepInDependency(Jotunn.Main.ModGuid)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod,VersionStrictness.Patch)]
public sealed class Plugin:BaseUnityPlugin
{
    internal static Plugin Instance;
    Ballot ballot=new Ballot();
    readonly HashSet<ZNetPeer> registered=new HashSet<ZNetPeer>();
    Harmony harmony;
    float nextTick;
    int pending;
    float expires;
    YesNoPopup popup;
    bool preview;
    static readonly AccessTools.FieldRef<UnifiedPopup,Stack<PopupBase>> stack=AccessTools.FieldRefAccess<UnifiedPopup,Stack<PopupBase>>("popupStack");
    static readonly System.Reflection.FieldInfo popupInstance=AccessTools.Field(typeof(UnifiedPopup),"instance");
    void Awake()
    {
        Instance=this;harmony=new Harmony("scrubclub.sleepvote");harmony.PatchAll();
        new Terminal.ConsoleCommand("sleepvote_preview","Preview the sleep prompt in a solo local world; does not change time.",args=>
        {
            if(!ZNet.instance || !ZNet.instance.IsServer() || ZNet.instance.GetPeers().Count!=0 || !Player.m_localPlayer)
            {args.Context.AddString("Use this preview in a solo local world.");return;}
            Receive(-1,60);preview=true;
        });
    }
    void OnDestroy(){Reset();harmony?.UnpatchSelf();Instance=null;}
    internal void Reset(){ballot=new Ballot();registered.Clear();pending=0;preview=false;ClosePopup();}
    internal void Register(ZNetPeer peer)
    {
        if(!registered.Add(peer))return;
        peer.m_rpc.Register<int,bool>("SC_SleepVoteAnswer",(rpc,id,yes)=>
        {
            if(!ZNet.instance || !ZNet.instance.IsServer() || !peer.IsReady() || !ZNet.instance.GetPeers().Contains(peer))return;
            RefreshBallot();
            if(ballot.Respond(peer.m_uid,id,yes,Time.realtimeSinceStartup))Broadcast();
        });
        peer.m_rpc.Register<int,int>("SC_SleepVotePrompt",(rpc,id,seconds)=>
        {
            if(!ZNet.instance || ZNet.instance.IsServer() || peer!=ZNet.instance.GetServerPeer())return;
            Receive(id,seconds);
        });
    }
    void Update()
    {
        if(ZNet.instance && Time.unscaledTime>=nextTick)
        {
            nextTick=Time.unscaledTime+1;
            foreach(var peer in ZNet.instance.GetPeers())Register(peer);
            registered.RemoveWhere(p=>!ZNet.instance.GetPeers().Contains(p));
            if(ZNet.instance.IsServer()){RefreshBallot();Broadcast();}
        }
        if(pending!=0 && (Time.unscaledTime>=expires || !Player.m_localPlayer || Player.m_localPlayer.InBed())){pending=0;preview=false;}
        if(pending==0){ClosePopup();return;}
        if(popup==null && UnifiedPopup.IsAvailable() && !UnifiedPopup.IsVisible())
        {
            int id=pending;
            popup=new YesNoPopup("Skip the night?","Another player is in bed. Allow the night to pass?\nAfter everyone agrees, you have 30 seconds to get into bed for rested benefits. Everyone in bed can sleep sooner.\nNo reply within 60 seconds keeps the night going.",()=>Answer(id,true),()=>Answer(id,false),false);
            UnifiedPopup.Push(popup);
        }
    }
    void Receive(int id,int seconds)
    {
        if(id!=pending){pending=id;ClosePopup();}
        expires=Time.unscaledTime+Mathf.Clamp(seconds,0,60);
    }
    void ClosePopup()
    {
        if(popup==null)return;
        var instance=popupInstance.GetValue(null) as UnifiedPopup;
        if(!instance){popup=null;return;}
        var entries=stack(instance);
        if(entries.Count>0 && ReferenceEquals(entries.Peek(),popup)){UnifiedPopup.Pop();popup=null;}
        else if(!entries.Contains(popup))popup=null;
    }
    void Answer(int id,bool yes)
    {
        pending=0;ClosePopup();
        if(preview){preview=false;Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft,"Sleep Vote preview: "+(yes?"approved":"declined")+". World time unchanged.");return;}
        if(yes && Player.m_localPlayer)Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft,"Agreed. Head to bed for rest; skipping waits 30 seconds after everyone agrees.");
        if(!ZNet.instance)return;
        if(ZNet.instance.IsServer())
        {RefreshBallot();ballot.Respond(ZNet.GetUID(),id,yes,Time.realtimeSinceStartup);Broadcast();}
        else ZNet.instance.GetServerPeer()?.m_rpc.Invoke("SC_SleepVoteAnswer",id,yes);
    }
    internal void RefreshBallot()
    {
        if(!ZNet.instance || !ZNet.instance.IsServer() || !EnvMan.instance)return;
        var people=new Dictionary<long,bool>();
        foreach(var peer in ZNet.instance.GetPeers())
        {
            if(!peer.IsReady())continue;
            var zdo=ZDOMan.instance.GetZDO(peer.m_characterID);
            people[peer.m_uid]=zdo!=null && zdo.GetBool(ZDOVars.s_inBed,false);
        }
        if(Player.m_localPlayer)people[ZNet.GetUID()]=Player.m_localPlayer.InBed();
        ballot.Tick(people,!EnvMan.instance.IsTimeSkipping() && (EnvMan.IsAfternoon() || EnvMan.IsNight()),Time.realtimeSinceStartup);
    }
    void Broadcast()
    {
        int seconds=(int)Math.Ceiling(ballot.Deadline-Time.realtimeSinceStartup);
        foreach(var peer in ZNet.instance.GetPeers())if(peer.IsReady())
            peer.m_rpc.Invoke("SC_SleepVotePrompt",ballot.NeedsVote(peer.m_uid)?ballot.Id:0,Math.Max(0,seconds));
        if(Player.m_localPlayer && !preview)Receive(ballot.NeedsVote(ZNet.GetUID())?ballot.Id:0,seconds);
    }
    internal bool Approved(){RefreshBallot();return ballot.Approved;}
}
[HarmonyPatch(typeof(ZNet),"OnNewConnection")]
static class RegisterSleepRpc{static void Postfix(ZNetPeer peer)=>Plugin.Instance?.Register(peer);}
[HarmonyPatch(typeof(ZNet),"OnDestroy")]
static class EndSleepSession{static void Prefix()=>Plugin.Instance?.Reset();}
[HarmonyPatch(typeof(Game),"EverybodyIsTryingToSleep")]
static class AllowAgreedNight
{
    static void Postfix(ref bool __result)
    {
        if(!__result && ZNet.instance && ZNet.instance.IsServer())__result=Plugin.Instance?.Approved()==true;
    }
}
[HarmonyPatch(typeof(Game),"SleepStart")]
static class OnlyBedSleepersRest
{
    static bool Prefix()=>!Player.m_localPlayer || Player.m_localPlayer.InBed();
}
[HarmonyPatch(typeof(Game),"SleepStop")]
static class LeaveAwakePlayersAlone
{
    internal static bool SuppressDetach;
    // Keep native world saving and OnSleep callbacks, but don't detach awake
    // voters from ships/chairs. SetSleeping(false) is already a no-op for them.
    static void Prefix()=>SuppressDetach=Player.m_localPlayer && !Player.m_localPlayer.IsSleeping();
    static void Finalizer()=>SuppressDetach=false;
}
[HarmonyPatch(typeof(Player),nameof(Player.AttachStop))]
static class KeepAwakeAttachment
{
    static bool Prefix(Player __instance)=>!LeaveAwakePlayersAlone.SuppressDetach || __instance!=Player.m_localPlayer;
}
