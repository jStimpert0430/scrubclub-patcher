using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using Jotunn.Utils;
using SleepVote.Core;
using UnityEngine;
namespace SleepVote;
[BepInPlugin("scrubclub.sleepvote","Sleep Vote","0.2.0")]
[BepInDependency(Jotunn.Main.ModGuid)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod,VersionStrictness.Patch)]
public sealed class Plugin:BaseUnityPlugin
{
    internal static Plugin Instance;
    Ballot ballot=new Ballot();
    readonly HashSet<ZNetPeer> registered=new HashSet<ZNetPeer>();
    readonly Dictionary<long,(bool fighting,float received)> combat=new Dictionary<long,(bool,float)>();
    readonly Dictionary<long,string> names=new Dictionary<long,string>();
    Dictionary<long,bool> beds=new Dictionary<long,bool>();
    Harmony harmony;
    VoteHud hud;
    float nextTick,lastThreat=-100,expires,terminalSince;
    int pending,lastNotified,lastOutcome,initiatorVote;
    string initiator="A player";
    YesNoPopup popup;
    bool preview,combatEpisode,reportedCombat;
    // Local detection suppresses UI immediately, without waiting for a server
    // round trip. The received flag also supports the combat-only mock preview.
    internal bool HideVoteUi=>LocalCombat() || (reportedCombat && Time.unscaledTime<expires);
    static readonly AccessTools.FieldRef<UnifiedPopup,Stack<PopupBase>> stack=AccessTools.FieldRefAccess<UnifiedPopup,Stack<PopupBase>>("popupStack");
    static readonly System.Reflection.FieldInfo popupInstance=AccessTools.Field(typeof(UnifiedPopup),"instance");
    void Awake()
    {
        Instance=this;hud=gameObject.AddComponent<VoteHud>();
        harmony=new Harmony("scrubclub.sleepvote");harmony.PatchAll();
        new Terminal.ConsoleCommand("sleepvote_preview","Solo mock: sleepvote_preview sleeper|voter [votes|combat|bed|overridden], or stop. World time unchanged.",args=>
        {
            if(!ZNet.instance || !ZNet.instance.IsServer() || ZNet.instance.GetPeers().Count!=0 || !Player.m_localPlayer)
            {args.Context.AddString("Use this preview in a solo local world.");return;}
            string role=args.Args.Length>1?args.Args[1].ToLowerInvariant():"voter";
            if(role=="stop"){preview=false;pending=0;ClosePopup();hud.Clear();return;}
            string mode=args.Args.Length>2?args.Args[2].ToLowerInvariant():"votes";
            // Preserve the earlier single-argument preview commands.
            if(new[]{"votes","combat","bed","overridden"}.Contains(role)){mode=role;role="voter";}
            if((role!="sleeper" && role!="voter") || !new[]{"votes","combat","bed","overridden"}.Contains(mode))
            {args.Context.AddString("Use: sleepvote_preview sleeper|voter votes|combat|bed|overridden");return;}
            ShowPreview(role=="sleeper",mode);
            args.Context.AddString("Mock "+role+" perspective. Close F5 to inspect it; no bed or real players are needed.");
        });
    }
    bool previewSleeper;
    void ShowPreview(bool sleeper,string mode)
    {
        pending=0;ClosePopup();preview=true;previewSleeper=sleeper;
        lastOutcome=0;lastNotified=0;combatEpisode=false;
        var phase=mode=="combat"?SleepPhase.WaitingForCombat:mode=="bed"?SleepPhase.GetToBed:mode=="overridden"?SleepPhase.Overridden:SleepPhase.WaitingForVotes;
        string voterState=mode=="combat"?"In combat":mode=="bed"?"Yes":mode=="overridden"?"No":"Waiting";
        string rows="Preview: "+(sleeper?"You initiated the vote from bed":"You are an awake voter")+
            "\nVotes: "+(mode=="bed"?"3/3":"2/3")+" agreed\n"+
            (sleeper?"You: In bed\nAsta: Yes\nBjorn: "+voterState:"Asta: In bed\nBjorn: Yes\nYou: "+voterState);
        Receive(-1,!sleeper && phase==SleepPhase.WaitingForVotes,phase,phase==SleepPhase.GetToBed?15:60,
            sleeper?"You":"Asta",sleeper?"Bjorn declined":"You declined",!sleeper && mode=="combat",rows);
        if(sleeper && mode=="combat")Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft,"Waiting for votes, player in combat");
    }

    void OnDestroy(){Reset();harmony?.UnpatchSelf();Instance=null;}
    internal void Reset()
    {
        ballot=new Ballot();registered.Clear();combat.Clear();names.Clear();beds.Clear();
        pending=0;preview=false;lastOutcome=lastNotified=initiatorVote=0;combatEpisode=false;reportedCombat=false;
        lastThreat=-100;ClosePopup();if(hud)hud.Clear();
    }
    bool LocalCombat()
    {
        var p=Player.m_localPlayer;
        if(!p || p.IsDead())return false;
        if(p.IsSensed() || p.InAttack())lastThreat=Time.unscaledTime;
        return Time.unscaledTime-lastThreat<8;
    }
    bool ValidPeer(ZNetPeer peer)=>ZNet.instance && ZNet.instance.IsServer() && peer.IsReady() && ZNet.instance.GetPeers().Contains(peer);
    internal void Register(ZNetPeer peer)
    {
        if(!registered.Add(peer))return;
        peer.m_rpc.Register<bool>("SC_SleepCombat",(rpc,fighting)=>
        {if(ValidPeer(peer))combat[peer.m_uid]=(fighting,Time.unscaledTime);});
        peer.m_rpc.Register<int,bool>("SC_SleepVoteAnswer",(rpc,id,yes)=>
        {
            if(!ValidPeer(peer))return;
            RefreshBallot();
            if(ballot.Respond(peer.m_uid,id,yes,Time.realtimeSinceStartup))Broadcast();
        });
        peer.m_rpc.Register<ZPackage>("SC_SleepVoteState",(rpc,pkg)=>
        {
            if(!ZNet.instance || ZNet.instance.IsServer() || peer!=ZNet.instance.GetServerPeer())return;
            try
            {
                int id=pkg.ReadInt();bool prompt=pkg.ReadBool();var phase=(SleepPhase)pkg.ReadInt();int seconds=pkg.ReadInt();
                string source=Safe(pkg.ReadString()),reason=Safe(pkg.ReadString());bool fighting=pkg.ReadBool();
                int count=pkg.ReadInt();if(count<0 || count>64 || !Enum.IsDefined(typeof(SleepPhase),phase))return;
                var rows=new List<string>();for(int i=0;i<count;i++)rows.Add(Safe(pkg.ReadString())+": "+Safe(pkg.ReadString()));
                preview=false;Receive(id,prompt,phase,seconds,source,reason,fighting,string.Join("\n",rows));
            }
            catch(Exception error){Logger.LogWarning("Invalid sleep status: "+error.Message);}
        });
    }
    static string Safe(string text)
    {
        text=(text??"").Replace('<','‹').Replace('>','›').Replace('\n',' ').Replace('\r',' ');
        return text.Length>100?text.Substring(0,100):text;
    }
    void Update()
    {
        bool fighting=LocalCombat();
        if(ZNet.instance && Time.unscaledTime>=nextTick)
        {
            nextTick=Time.unscaledTime+0.5f;
            foreach(var peer in ZNet.instance.GetPeers())Register(peer);
            registered.RemoveWhere(p=>!ZNet.instance.GetPeers().Contains(p));
            if(ZNet.instance.IsServer()){RefreshBallot();Broadcast();}
            else ZNet.instance.GetServerPeer()?.m_rpc.Invoke("SC_SleepCombat",fighting);
        }
        if(preview && Time.unscaledTime>=expires){preview=false;pending=0;hud.Clear();}
        if(pending!=0 && (!Player.m_localPlayer || Player.m_localPlayer.InBed()))pending=0;
        if(pending==0 || HideVoteUi){ClosePopup();return;}
        if(Time.unscaledTime>=expires){pending=0;ClosePopup();return;}
        if(popup==null && UnifiedPopup.IsAvailable() && !UnifiedPopup.IsVisible())
        {
            int id=pending;
            popup=new YesNoPopup("Skip the night?",initiator+" has initiated a sleep vote.\nAfter everyone agrees, you have 15 seconds to get into bed for rested benefits. Everyone in bed can sleep sooner.\nThe status box shows live votes and time remaining. Combat pauses the countdown.",()=>Answer(id,true),()=>Answer(id,false),false);
            UnifiedPopup.Push(popup);
        }
    }
    void Receive(int id,bool prompt,SleepPhase phase,int seconds,string source,string reason,bool fighting,string rows)
    {
        reportedCombat=fighting;
        initiator=source;
        int next=prompt && !fighting?id:0;
        if(next!=pending){pending=next;ClosePopup();}
        expires=Time.unscaledTime+(preview?60:3);
        hud.Phase=phase;hud.Remaining=Mathf.Clamp(seconds,0,60);hud.Received=Time.unscaledTime;hud.Rows=rows;hud.Reason=reason;
        if(phase==SleepPhase.Idle){hud.Clear();return;}
        if(phase==SleepPhase.Overridden || phase==SleepPhase.Finished)
        {
            if(id!=lastOutcome){lastOutcome=id;terminalSince=Time.unscaledTime;}
            hud.Until=terminalSince+8;
        }
        else hud.Until=Time.unscaledTime+(preview?60:3);
        if(fighting && (!combatEpisode || lastNotified!=id))
        {
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft,source+" has initiated a sleep vote.");
            lastNotified=id;
        }
        else if(phase==SleepPhase.WaitingForCombat && Player.m_localPlayer && Player.m_localPlayer.InBed() && !combatEpisode)
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft,"Waiting for votes, player in combat");
        combatEpisode=phase==SleepPhase.WaitingForCombat;
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
        if(preview)
        {
            ShowPreview(previewSleeper,yes?"bed":"overridden");return;
        }
        if(!ZNet.instance || LocalCombat())return;
        if(ZNet.instance.IsServer())
        {RefreshBallot();ballot.Respond(ZNet.GetUID(),id,yes,Time.realtimeSinceStartup);Broadcast();}
        else ZNet.instance.GetServerPeer()?.m_rpc.Invoke("SC_SleepVoteAnswer",id,yes);
    }
    internal void RefreshBallot()
    {
        if(!ZNet.instance || !ZNet.instance.IsServer() || !EnvMan.instance)return;
        beds=new Dictionary<long,bool>();names.Clear();var fighting=new HashSet<long>();
        foreach(var peer in ZNet.instance.GetPeers())
        {
            if(!peer.IsReady())continue;
            var zdo=ZDOMan.instance.GetZDO(peer.m_characterID);
            beds[peer.m_uid]=zdo!=null && zdo.GetBool(ZDOVars.s_inBed,false);names[peer.m_uid]=Safe(peer.m_playerName);
            // Missing/stale reports pause, never silently count as safe approval.
            if(!combat.TryGetValue(peer.m_uid,out var status) || Time.unscaledTime-status.received>3 || status.fighting)fighting.Add(peer.m_uid);
        }
        foreach(var id in combat.Keys.Where(id=>!beds.ContainsKey(id)).ToArray())combat.Remove(id);
        if(Player.m_localPlayer)
        {
            long id=ZNet.GetUID();beds[id]=Player.m_localPlayer.InBed();names[id]=Safe(Player.m_localPlayer.GetPlayerName());
            if(LocalCombat())fighting.Add(id);
        }
        ballot.Tick(beds,fighting,!EnvMan.instance.IsTimeSkipping() && (EnvMan.IsAfternoon() || EnvMan.IsNight()),Time.realtimeSinceStartup);
        if(ballot.Active && initiatorVote!=ballot.Id)
        {initiatorVote=ballot.Id;initiator=names[beds.First(p=>p.Value).Key];}
    }
    string PlayerStatus(long id)=>id==ballot.DeclinedBy?"No":ballot.IsInCombat(id)?"In combat":beds[id]?"In bed":ballot.HasAgreed(id)?"Yes":"Waiting";
    string VoteSummary()=>beds.Count(p=>p.Value || ballot.HasAgreed(p.Key))+"/"+beds.Count+" agreed (including players in bed)";
    void Broadcast()
    {
        int seconds=(int)Math.Ceiling(ballot.Remaining);
        foreach(var peer in ZNet.instance.GetPeers())if(peer.IsReady())
        {
            var p=new ZPackage();p.Write(ballot.Id);p.Write(ballot.NeedsVote(peer.m_uid));p.Write((int)ballot.Phase);p.Write(seconds);
            p.Write(initiator);p.Write(ballot.Outcome);p.Write(ballot.IsInCombat(peer.m_uid));
            var ids=beds.Keys.Take(63).ToArray();p.Write(ids.Length+1);p.Write("Votes");p.Write(VoteSummary());foreach(var id in ids){p.Write(names[id]);p.Write(PlayerStatus(id));}
            peer.m_rpc.Invoke("SC_SleepVoteState",p);
        }
        if(Player.m_localPlayer && !preview)
            Receive(ballot.Id,ballot.NeedsVote(ZNet.GetUID()),ballot.Phase,seconds,initiator,ballot.Outcome,ballot.IsInCombat(ZNet.GetUID()),"Votes: "+VoteSummary()+"\n"+string.Join("\n",beds.Keys.Select(id=>names[id]+": "+PlayerStatus(id))));
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
