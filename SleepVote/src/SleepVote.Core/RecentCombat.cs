using System;
namespace SleepVote.Core;
public sealed class RecentCombat
{
    public const double Duration=20;
    double last=double.NegativeInfinity;
    public void Record(double now){last=now;}
    public void Reset(){last=double.NegativeInfinity;}
    public double Remaining(double now)=>now>=last?Math.Max(0,Duration-(now-last)):0;
    public bool Active(double now)=>Remaining(now)>0;
}
