using System.Collections.Generic;
using System.Linq;
namespace SleepVote.Core;
public sealed class Ballot
{
    readonly HashSet<long> yes=new HashSet<long>();
    Dictionary<long,bool> people=new Dictionary<long,bool>();
    bool armed=true;
    public int Id{get;private set;}
    public bool Active{get;private set;}
    public double Deadline{get;private set;}
    public double ReadyAt{get;private set;}
    double time;
    bool Unanimous=>people.Count>0 && people.Values.Any(b=>b) && people.All(p=>p.Value || yes.Contains(p.Key));
    public bool Approved=>Active && ReadyAt>0 && time>=ReadyAt && Unanimous;
    public bool NeedsVote(long id)=>Active && people.TryGetValue(id,out var bed) && !bed && !yes.Contains(id);
    public void Tick(Dictionary<long,bool> current,bool night,double now)
    {
        people=new Dictionary<long,bool>(current);
        time=now;
        yes.RemoveWhere(id=>!people.ContainsKey(id));
        bool sleepers=people.Values.Any(b=>b);
        if(!night || !sleepers){Active=false;ReadyAt=0;yes.Clear();armed=true;return;}
        if(Active)
        {
            if(Unanimous){if(ReadyAt==0)ReadyAt=now+30;}
            else {ReadyAt=0;if(now>=Deadline){Active=false;yes.Clear();return;}}
        }
        if(!Active && armed && people.Values.Any(b=>!b))
        {Id++;Active=true;armed=false;yes.Clear();Deadline=now+60;ReadyAt=0;}
    }
    public bool Respond(long player,int ballot,bool approve,double now)
    {
        if(ballot!=Id || !NeedsVote(player) || now>=Deadline)return false;
        if(approve){yes.Add(player);if(Unanimous && ReadyAt==0)ReadyAt=now+30;}
        else{Active=false;ReadyAt=0;yes.Clear();}
        return true;
    }
}
