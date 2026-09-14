using System.Collections.Generic;
using System.Linq;
namespace SleepVote.Core;
public enum SleepPhase { Idle, WaitingForVotes, WaitingForCombat, GetToBed, Overridden, Finished }
public sealed class Ballot
{
    readonly HashSet<long> yes=new HashSet<long>();
    Dictionary<long,bool> people=new Dictionary<long,bool>();
    bool armed=true;
    HashSet<long> combat=new HashSet<long>();
    bool previousCombat;
    double previousTime;
    bool clockStarted;
    public string Outcome{get;private set;}="";
    public long DeclinedBy{get;private set;}
    public bool HasCombat=>combat.Any(id=>people.ContainsKey(id));
    public SleepPhase Phase=>!Active?(Outcome=="Night ended or sleep started"?SleepPhase.Finished:Outcome.Length>0?SleepPhase.Overridden:SleepPhase.Idle):HasCombat?SleepPhase.WaitingForCombat:ReadyAt>0?SleepPhase.GetToBed:SleepPhase.WaitingForVotes;
    public double Remaining=>!Active?0:System.Math.Max(0,(ReadyAt>0?ReadyAt:Deadline)-time);
    public bool HasAgreed(long id)=>yes.Contains(id);
    public bool IsInCombat(long id)=>combat.Contains(id);
    public int Id{get;private set;}
    public bool Active{get;private set;}
    public double Deadline{get;private set;}
    public double ReadyAt{get;private set;}
    double time;
    bool Unanimous=>people.Count>0 && people.Values.Any(b=>b) && people.All(p=>p.Value || yes.Contains(p.Key));
    public bool Approved=>Active && !HasCombat && ReadyAt>0 && time>=ReadyAt && Unanimous;
    public bool NeedsVote(long id)=>Active && people.TryGetValue(id,out var bed) && !bed && !yes.Contains(id) && !combat.Contains(id);
    public void Tick(Dictionary<long,bool> current,bool night,double now)
        =>Tick(current,new HashSet<long>(),night,now);
    public void Tick(Dictionary<long,bool> current,HashSet<long> fighting,bool night,double now)
    {
        double elapsed=clockStarted?System.Math.Max(0,now-previousTime):0;
        previousTime=now;clockStarted=true;
        people=new Dictionary<long,bool>(current);
        combat=new HashSet<long>(fighting);
        time=now;
        // Freeze both countdowns while anyone is fighting, including the interval
        // in which combat ends. A delayed heartbeat must not spend paused time.
        if(Active && (HasCombat || previousCombat)){Deadline+=elapsed;if(ReadyAt>0)ReadyAt+=elapsed;}
        previousCombat=HasCombat;
        yes.RemoveWhere(id=>!people.ContainsKey(id));
        bool sleepers=people.Values.Any(b=>b);
        if(!night || !sleepers)
        {
            if(Active)Outcome=!night?"Night ended or sleep started":"Everyone left their beds";
            Active=false;ReadyAt=0;yes.Clear();armed=true;return;
        }
        if(Active)
        {
            if(Unanimous){if(ReadyAt==0)ReadyAt=now+15;}
            else {ReadyAt=0;if(!HasCombat && now>=Deadline){Active=false;Outcome="Vote timed out";yes.Clear();return;}}
        }
        if(!Active && armed && people.Values.Any(b=>!b))
        {Id++;Active=true;armed=false;Outcome="";DeclinedBy=0;yes.Clear();Deadline=now+60;ReadyAt=0;}
    }
    public bool Respond(long player,int ballot,bool approve,double now)
    {
        if(ballot!=Id || !NeedsVote(player) || (!HasCombat && now>=Deadline))return false;
        if(approve){yes.Add(player);if(Unanimous && ReadyAt==0)ReadyAt=now+15;}
        else{Active=false;ReadyAt=0;DeclinedBy=player;Outcome="Vote declined";}
        return true;
    }
}
