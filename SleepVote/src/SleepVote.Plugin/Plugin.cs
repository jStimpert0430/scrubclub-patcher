using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using Jotunn.Utils;
using SleepVote.Core;
using UnityEngine;
namespace SleepVote;
[BepInPlugin("scrubclub.sleepvote","Sleep Vote","0.2.2")]
[BepInDependency(Jotunn.Main.ModGuid)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod,VersionStrictness.Patch)]
public sealed class Plugin:BaseUnityPlugin
{
    internal static Plugin Instance;
    Ballot ballot=new Ballot();
    ClientVote clientVote=new ClientVote();
    long revision;
    static readonly AccessTools.FieldRef<Game,bool> gameSleeping=AccessTools.FieldRefAccess<Game,bool>("m_sleeping");
    SleepPhase loggedPhase;
    int loggedId;
    readonly HashSet<ZNetPeer> registered=new HashSet<ZNetPeer>();
    readonly Dictionary<long,(bool fighting,float received)> combat=new Dictionary<long,(bool,float)>();
    readonly Dictionary<long,string> names=new Dictionary<long,string>();
    Dictionary<long,bool> beds=new Dictionary<long,bool>();
    Harmony harmony;
    VoteHud hud;
    float nextTick,expires,terminalSince;
    readonly RecentCombat recentCombat=new RecentCombat();
    int pending,lastNotified,lastOutcome,initiatorVote;
    string initiator="A player";
    YesNoPopup popup;
    bool preview,combatEpisode,reportedCombat;
    // Local detection suppresses UI immediately, without waiting for a server
    // round trip. The received flag also supports the combat-only mock preview.
    internal bool HideVoteUi=>!Player.m_localPlayer || Player.m_localPlayer.IsDead() || LocalCombat() || (reportedCombat && Time.unscaledTime<expires);
    static readonly AccessTools.FieldRef<UnifiedPopup,Stack<PopupBase>> stack=AccessTools.FieldRefAccess<UnifiedPopup,Stack<PopupBase>>("popupStack");
    static readonly System.Reflection.FieldInfo popupInstance=AccessTools.Field(typeof(UnifiedPopup),"instance");
    void Awake()
    {
        Instance=this;hud=gameObject.AddComponent<VoteHud>();
        harmony=new Harmony("scrubclub.sleepvote");harmony.PatchAll();
        new Terminal.ConsoleCommand("sleepvote_status","Print local sleep-vote timing and state diagnostics.",args=>
        {
            if(!ZNet.instance || !EnvMan.instance){args.Context.AddString("No active world.");return;}
            args.Context.AddString($"Sleep Vote 0.2.2: server={ZNet.instance.IsServer()}, phase={hud.Phase}, ballot={clientVote.Id}, revision={clientVote.Revision}, awaitingReceipt={clientVote.WaitingForReceipt}, hidden={HideVoteUi}");
            args.Context.AddString($"Recent attack window: {recentCombat.Remaining(Time.unscaledTime):F1}s remaining (20s after last attack or incoming hit). Nearby enemies do not extend it.");
            args.Context.AddString($"World time={ZNet.instance.GetTimeSeconds():F2}, sleepWindow={WindowOpen()}, visualNight={EnvMan.IsNight()}, timeSkipping={EnvMan.instance.IsTimeSkipping()}, serverSleeping={(Game.instance && gameSleeping(Game.instance))}");
        });
        new Terminal.ConsoleCommand("sleepvote_preview","Solo mock: sleepvote_preview sleeper|voter [votes|combat|bed|sleeping|finished|connection|overridden], or stop. World time unchanged.",args=>
        {
            if(!ZNet.instance || !ZNet.instance.IsServer() || ZNet.instance.GetPeers().Count!=0 || !Player.m_localPlayer)
            {args.Context.AddString("Use this preview in a solo local world.");return;}
            string role=args.Args.Length>1?args.Args[1].ToLowerInvariant():"voter";
            if(role=="stop"){preview=false;pending=0;ClosePopup();hud.Clear();return;}
            string mode=args.Args.Length>2?args.Args[2].ToLowerInvariant():"votes";
            // Preserve the earlier single-argument preview commands.
            if(new[]{"votes","combat","bed","sleeping","finished","connection","overridden"}.Contains(role)){mode=role;role="voter";}
            if((role!="sleeper" && role!="voter") || !new[]{"votes","combat","bed","sleeping","finished","connection","overridden"}.Contains(mode))
            {args.Context.AddString("Use: sleepvote_preview sleeper|voter votes|combat|bed|sleeping|finished|connection|overridden");return;}
            ShowPreview(role=="sleeper",mode);
            args.Context.AddString("Mock "+role+" perspective. Close F5 to inspect it; no bed or real players are needed.");
        });
    }
    bool previewSleeper;
    void ShowPreview(bool sleeper,string mode)
    {
        pending=0;ClosePopup();preview=true;previewSleeper=sleeper;
        lastOutcome=0;lastNotified=0;combatEpisode=false;
        var phase=mode=="combat"?SleepPhase.WaitingForCombat:mode=="bed"?SleepPhase.GetToBed:mode=="overridden"?SleepPhase.Overridden:mode=="sleeping"?SleepPhase.Sleeping:mode=="finished"?SleepPhase.Finished:mode=="connection"?SleepPhase.WaitingForPlayers:SleepPhase.WaitingForVotes;
        string voterState=mode=="combat"?"In combat":mode=="bed"?"Yes":mode=="overridden"?"No":"Waiting";
        string rows="Preview: "+(sleeper?"You initiated the vote from bed":"You are an awake voter")+
            "\nVotes: "+(mode=="bed"?"3/3":"2/3")+" agreed\n"+
            (sleeper?"You: In bed\nAsta: Yes\nBjorn: "+voterState:"Asta: In bed\nBjorn: Yes\nYou: "+voterState);
        Receive(-1,!sleeper && phase==SleepPhase.WaitingForVotes,phase,phase==SleepPhase.GetToBed?15:60,
            sleeper?"You":"Asta",mode=="finished"?"Sleep completed":sleeper?"Bjorn declined":"You declined",!sleeper && mode=="combat",rows);
        if(sleeper && mode=="combat")Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft,"Waiting for votes, player in combat");
    }

    void OnDestroy(){Reset();harmony?.UnpatchSelf();Instance=null;}
    internal void Reset()
    {
        ballot=new Ballot();clientVote=new ClientVote();revision=0;nextTick=0;loggedId=0;loggedPhase=SleepPhase.Idle;registered.Clear();combat.Clear();names.Clear();beds.Clear();
        pending=0;preview=false;lastOutcome=lastNotified=initiatorVote=0;combatEpisode=false;reportedCombat=false;
        recentCombat.Reset();ClosePopup();if(hud)hud.Clear();
    }
    internal bool LocalCombat()
    {
        var p=Player.m_localPlayer;
        if(!p || p.IsDead())return false;
        return recentCombat.Active(Time.unscaledTime);
    }
    internal void RecordCombatHit()=>recentCombat.Record(Time.unscaledTime);
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
            bool accepted=ballot.Respond(peer.m_uid,id,yes,Time.realtimeSinceStartup);
            // Duplicate delivery acknowledges the already accepted decision.
            accepted|=id==ballot.Id && (yes?ballot.HasAgreed(peer.m_uid):ballot.DeclinedBy==peer.m_uid);
            peer.m_rpc.Invoke("SC_SleepVoteReceipt",id,accepted);
            Broadcast();
        });
        peer.m_rpc.Register<int,bool>("SC_SleepVoteReceipt",(rpc,id,accepted)=>
        {
            if(!ZNet.instance || ZNet.instance.IsServer() || peer!=ZNet.instance.GetServerPeer())return;
            clientVote.Receipt(id,accepted);
        });
        peer.m_rpc.Register<ZPackage>("SC_SleepVoteState",(rpc,pkg)=>
        {
            if(!ZNet.instance || ZNet.instance.IsServer() || peer!=ZNet.instance.GetServerPeer())return;
            try
            {
                long sequence=pkg.ReadLong();int id=pkg.ReadInt();bool prompt=pkg.ReadBool();var phase=(SleepPhase)pkg.ReadInt();int seconds=pkg.ReadInt();
                string source=Safe(pkg.ReadString()),reason=Safe(pkg.ReadString());bool fighting=pkg.ReadBool();
                int count=pkg.ReadInt();if(count<0 || count>64 || !Enum.IsDefined(typeof(SleepPhase),phase))return;
                var rows=new List<string>();for(int i=0;i<count;i++)rows.Add(Safe(pkg.ReadString())+": "+Safe(pkg.ReadString()));
                if(!clientVote.Apply(id,sequence,prompt,IsVoting(phase)))return;
                preview=false;Receive(id,clientVote.CanPrompt,phase,seconds,source,reason,fighting,string.Join("\n",rows));
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
        if(pending!=0 && (!Player.m_localPlayer || Player.m_localPlayer.InBed() || Player.m_localPlayer.IsSleeping() || (!preview && !clientVote.CanPrompt)))pending=0;
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
        if(phase==SleepPhase.Idle){hud.Clear();combatEpisode=false;return;}
        if(!preview && phase==SleepPhase.Finished && reason=="Morning arrived")ReleaseWaitingBed();
        if(phase==SleepPhase.Overridden || phase==SleepPhase.Finished)
        {
            if(id!=lastOutcome){lastOutcome=id;terminalSince=Time.unscaledTime;}
            hud.Until=terminalSince+8;
        }
        else hud.Until=Time.unscaledTime+(preview?60:3);
        if(IsVoting(phase) && fighting && lastNotified!=id)
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
        else
        {
            // Remove only our buried popup, preserving other mods' stack order.
            // Otherwise it can resurface after combat or after the vote ended.
            var keep=entries.Where(item=>!ReferenceEquals(item,popup)).Reverse().ToArray();
            entries.Clear();foreach(var item in keep)entries.Push(item);
            popup=null;
        }
    }
    void Answer(int id,bool yes)
    {
        pending=0;ClosePopup();
        if(preview)
        {
            ShowPreview(previewSleeper,yes?"bed":"overridden");return;
        }
        if(!ZNet.instance || HideVoteUi || !clientVote.Submit(id))return;
        if(ZNet.instance.IsServer())
        {RefreshBallot();bool accepted=ballot.Respond(ZNet.GetUID(),id,yes,Time.realtimeSinceStartup);clientVote.Receipt(id,accepted);Broadcast();}
        else ZNet.instance.GetServerPeer()?.m_rpc.Invoke("SC_SleepVoteAnswer",id,yes);
    }
    internal void RefreshBallot()
    {
        if(!ZNet.instance || !ZNet.instance.IsServer() || !EnvMan.instance)return;
        var previousBeds=beds;
        beds=new Dictionary<long,bool>();names.Clear();var fighting=new HashSet<long>();var missing=new HashSet<long>();
        foreach(var peer in ZNet.instance.GetPeers())
        {
            if(!peer.IsReady())continue;
            var zdo=ZDOMan.instance.GetZDO(peer.m_characterID);
            beds[peer.m_uid]=zdo!=null?zdo.GetBool(ZDOVars.s_inBed,false):previousBeds.TryGetValue(peer.m_uid,out var wasInBed) && wasInBed;
            names[peer.m_uid]=Safe(peer.m_playerName);
            if(zdo==null)missing.Add(peer.m_uid);
            // Missing/stale reports pause, never silently count as safe approval.
            if(!combat.TryGetValue(peer.m_uid,out var status) || Time.unscaledTime-status.received>3)missing.Add(peer.m_uid);
            else if(status.fighting)fighting.Add(peer.m_uid);
        }
        foreach(var id in combat.Keys.Where(id=>!beds.ContainsKey(id)).ToArray())combat.Remove(id);
        if(Player.m_localPlayer)
        {
            long id=ZNet.GetUID();beds[id]=Player.m_localPlayer.InBed();names[id]=Safe(Player.m_localPlayer.GetPlayerName());
            if(LocalCombat())fighting.Add(id);
        }
        bool sleeping=EnvMan.instance.IsTimeSkipping() || (Game.instance && gameSleeping(Game.instance));
        ballot.Tick(beds,fighting,WindowOpen(),Time.realtimeSinceStartup,sleeping,missing);
        if(loggedId!=ballot.Id || loggedPhase!=ballot.Phase)
        {
            Logger.LogInfo($"Sleep vote {ballot.Id}: {ballot.Phase}; remaining={Math.Ceiling(ballot.Remaining)}s; players={beds.Count}; combat={fighting.Count}; unavailable={missing.Count}; {ballot.Outcome}");
            loggedId=ballot.Id;loggedPhase=ballot.Phase;
        }
        if(ballot.Active && initiatorVote!=ballot.Id)
        {initiatorVote=ballot.Id;initiator=names[beds.First(p=>p.Value).Key];}
    }
    string PlayerStatus(long id)=>ballot.IsUnavailable(id)?"Connection / character data pending":id==ballot.DeclinedBy?"No":ballot.IsInCombat(id)?"In combat":beds[id]?"In bed":ballot.HasAgreed(id)?"Yes":"Waiting";
    string VoteSummary()=>beds.Count(p=>p.Value || ballot.HasAgreed(p.Key))+"/"+beds.Count+" agreed (including players in bed)";
    void Broadcast()
    {
        long sequence=++revision;
        int seconds=(int)Math.Ceiling(ballot.Remaining);
        foreach(var peer in ZNet.instance.GetPeers())if(peer.IsReady())
        {
            var p=new ZPackage();p.Write(sequence);p.Write(ballot.Id);p.Write(ballot.NeedsVote(peer.m_uid));p.Write((int)ballot.Phase);p.Write(seconds);
            p.Write(initiator);p.Write(ballot.Outcome);p.Write(ballot.IsInCombat(peer.m_uid));
            var ids=beds.Keys.Take(63).ToArray();p.Write(ids.Length+1);p.Write("Votes");p.Write(VoteSummary());foreach(var id in ids){p.Write(names[id]);p.Write(PlayerStatus(id));}
            peer.m_rpc.Invoke("SC_SleepVoteState",p);
        }
        if(Player.m_localPlayer && !preview)
        {
            clientVote.Apply(ballot.Id,sequence,ballot.NeedsVote(ZNet.GetUID()),IsVoting(ballot.Phase));
            Receive(ballot.Id,clientVote.CanPrompt,ballot.Phase,seconds,initiator,ballot.Outcome,ballot.IsInCombat(ZNet.GetUID()),"Votes: "+VoteSummary()+"\n"+string.Join("\n",beds.Keys.Select(id=>names[id]+": "+PlayerStatus(id))));
        }
    }
    static bool IsVoting(SleepPhase phase)=>phase==SleepPhase.WaitingForVotes || phase==SleepPhase.WaitingForCombat || phase==SleepPhase.WaitingForPlayers || phase==SleepPhase.GetToBed;
    internal static bool WindowOpen()
    {
        if(!EnvMan.instance || !ZNet.instance)return false;
        var env=EnvMan.instance;double now=ZNet.instance.GetTimeSeconds();
        double length=env.m_dayLengthSec;
        int day=env.GetDay(now);
        return SleepWindow.IsOpen(now,length,env.GetMorningStartSec(day)-day*length,length*0.5);
    }
    // Dawn cancellation is not sleep: release the bed without SetSleeping(false)
    // or any rested effect. Actual sleepers are left to native SleepStop.
    static void ReleaseWaitingBed()
    {
        var player=Player.m_localPlayer;
        if(player && player.InBed() && !player.IsSleeping())player.AttachStop();
    }
    internal bool AllowSleep(bool allInBed)
    {
        RefreshBallot();
        return WindowOpen() && !ballot.HasCombat && !ballot.HasUnavailable && (allInBed || ballot.Approved);
    }
    internal void NativeSleepTransition()
    {
        clientVote.SleepTransition();pending=0;ClosePopup();
        if(ZNet.instance && ZNet.instance.IsServer()){RefreshBallot();Broadcast();}
    }
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
        if(ZNet.instance && ZNet.instance.IsServer() && Plugin.Instance)__result=Plugin.Instance.AllowSleep(__result);
    }
}
[HarmonyPatch(typeof(Game),"SleepStart")]
static class OnlyBedSleepersRest
{
    static bool Prefix(){Plugin.Instance?.NativeSleepTransition();return !Player.m_localPlayer || Player.m_localPlayer.InBed();}
}
[HarmonyPatch(typeof(Game),"SleepStop")]
static class LeaveAwakePlayersAlone
{
    internal static bool SuppressDetach;
    // Keep native world saving and OnSleep callbacks, but don't detach awake
    // voters from ships/chairs. SetSleeping(false) is already a no-op for them.
    static void Prefix()
    {
        Plugin.Instance?.NativeSleepTransition();
        SuppressDetach=Player.m_localPlayer && !Player.m_localPlayer.IsSleeping() && !Player.m_localPlayer.InBed();
    }
    static void Finalizer()=>SuppressDetach=false;
}
[HarmonyPatch(typeof(Player),nameof(Player.AttachStop))]
static class KeepAwakeAttachment
{
    static bool Prefix(Player __instance)=>!LeaveAwakePlayersAlone.SuppressDetach || __instance!=Player.m_localPlayer;
}

// The visual night flag lags behind the network clock at dawn. Refuse a new
// bed attempt after today's morning target; native SkipToMorning would then
// target tomorrow. Ownership, roof, fuel, wetness and enemies stay vanilla.
[HarmonyPatch(typeof(Bed),nameof(Bed.Interact))]
static class GuardDawnBedEntry
{
    static bool Prefix(Bed __instance,Humanoid human,bool repeat,ref bool __result)
    {
        if(repeat || human!=Player.m_localPlayer || !__instance.IsCurrent() || Plugin.WindowOpen())return true;
        human.Message(MessageHud.MessageType.Center,"$msg_cantsleep");
        __result=false;return false;
    }
}
[HarmonyPatch(typeof(Game),"UpdateSleeping")]
static class TrackNativeSleep
{
    static void Postfix()
    {
        if(ZNet.instance && ZNet.instance.IsServer())Plugin.Instance?.RefreshBallot();
    }
}

[HarmonyPatch(typeof(Character),"RPC_Damage")]
static class TrackCombatDamage
{
    static void Prefix(Character __instance,HitData hit)
    {
        // Observe incoming attacks before vanilla block/dodge damage reduction.
        // Weather, falls and other environmental damage have no character attacker.
        if(__instance==Player.m_localPlayer && !__instance.IsDead() && hit!=null &&
            hit.GetTotalDamage()>0 && hit.GetAttacker() && hit.GetAttacker()!=__instance)
            Plugin.Instance?.RecordCombatHit();
    }
}
[HarmonyPatch(typeof(Attack),"Start")]
static class TrackCombatSwing
{
    static void Postfix(Humanoid character,ItemDrop.ItemData weapon,bool __result)
    {
        // Ordinary weapon swings count on start; utility swings only count
        // through TrackOutgoingCombat when they connect with a character.
        if(!__result || character!=Player.m_localPlayer || character.IsDead() || weapon==null)return;
        var data=weapon.m_shared;
        if(CombatActions.CountsSwing(data.m_itemType.ToString(),data.m_skillType.ToString(),data.m_damages.m_chop))Plugin.Instance?.RecordCombatHit();
    }
}

[HarmonyPatch(typeof(Bed),"CheckEnemies")]
static class RecentCombatBedCheck
{
    static bool Prefix(Player human,ref bool __result)
    {
        if(!Plugin.Instance || human!=Player.m_localPlayer)return true;
        __result=!Plugin.Instance.LocalCombat();
        if(!__result)human.Message(MessageHud.MessageType.Center,"Wait until 20 seconds have passed since your last attack or incoming hit.");
        return false;
    }
}

[HarmonyPatch(typeof(Character),"Damage")]
static class TrackOutgoingCombat
{
    static void Prefix(Character __instance,HitData hit)
    {
        var player=Player.m_localPlayer;
        if(player && !player.IsDead() && __instance!=player && !__instance.IsDead() && hit!=null && hit.GetTotalDamage()>0 && hit.GetAttacker()==player)
            Plugin.Instance?.RecordCombatHit();
    }
}
