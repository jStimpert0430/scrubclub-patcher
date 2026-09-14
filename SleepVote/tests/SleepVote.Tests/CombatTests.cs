using System.Collections.Generic;
using SleepVote.Core;
using Xunit;
namespace SleepVote.Tests;
public class CombatTests
{
    static Dictionary<long,bool> Pair()=>new Dictionary<long,bool>{{1,true},{2,false}};
    [Fact] public void CombatDefersPromptAndPreservesFullVoteCountdown()
    {
        var b=new Ballot();b.Tick(Pair(),new HashSet<long>{2},true,0);
        Assert.Equal(SleepPhase.WaitingForCombat,b.Phase);Assert.False(b.NeedsVote(2));
        b.Tick(Pair(),new HashSet<long>{2},true,120);
        Assert.True(b.Active);Assert.Equal(60,b.Remaining);
        Assert.False(b.Respond(2,b.Id,true,120));
        b.Tick(Pair(),new HashSet<long>(),true,121);
        Assert.True(b.NeedsVote(2));Assert.Equal(60,b.Remaining);
        b.Tick(Pair(),true,131);Assert.Equal(50,b.Remaining);
    }
    [Fact] public void CombatBeginningMidVotePausesWithoutResettingElapsedTime()
    {
        var b=new Ballot();b.Tick(Pair(),true,0);b.Tick(Pair(),true,10);
        b.Tick(Pair(),new HashSet<long>{2},true,11);
        b.Tick(Pair(),new HashSet<long>{2},true,90);Assert.Equal(50,b.Remaining);
        b.Tick(Pair(),true,91);Assert.Equal(50,b.Remaining);
        b.Tick(Pair(),true,141);Assert.Equal(SleepPhase.Overridden,b.Phase);
    }
    [Fact] public void PeacefulPlayerCanApproveWhileSomeoneElseFights()
    {
        var people=Pair();people[3]=false;var b=new Ballot();b.Tick(people,new HashSet<long>{3},true,0);
        Assert.True(b.NeedsVote(2));Assert.True(b.Respond(2,b.Id,true,1));
        Assert.False(b.Respond(3,b.Id,true,1));Assert.False(b.Approved);
        Assert.True(b.HasAgreed(2));Assert.True(b.IsInCombat(3));
    }
    [Fact] public void CombatAlsoPausesTheGetToBedCountdown()
    {
        var b=new Ballot();b.Tick(Pair(),true,0);b.Respond(2,b.Id,true,1);b.Tick(Pair(),true,2);
        Assert.Equal(SleepPhase.GetToBed,b.Phase);Assert.Equal(14,b.Remaining);
        b.Tick(Pair(),new HashSet<long>{2},true,3);b.Tick(Pair(),new HashSet<long>{2},true,100);
        Assert.False(b.Approved);Assert.Equal(14,b.Remaining);
        b.Tick(Pair(),true,101);b.Tick(Pair(),true,114);Assert.False(b.Approved);
        b.Tick(Pair(),true,115);Assert.True(b.Approved);
    }
    [Fact] public void DeclineHasAnOverriddenStateAndIdentity()
    {
        var b=new Ballot();b.Tick(Pair(),true,0);b.Respond(2,b.Id,false,1);
        Assert.Equal(SleepPhase.Overridden,b.Phase);Assert.Equal(2,b.DeclinedBy);Assert.Equal("Vote declined",b.Outcome);
    }
    [Fact] public void DisconnectedCombatantCannotHoldTheVoteForever()
    {
        var people=Pair();people[3]=false;var b=new Ballot();b.Tick(people,new HashSet<long>{3},true,0);
        b.Respond(2,b.Id,true,1);b.Tick(Pair(),new HashSet<long>{3},true,20);
        Assert.False(b.HasCombat);b.Tick(Pair(),true,35);Assert.True(b.Approved);
    }
}
