using System.Collections.Generic;
using SleepVote.Core;
using Xunit;
namespace SleepVote.Tests;

public class LifecycleTests
{
    static Dictionary<long,bool> Pair()=>new Dictionary<long,bool>{{1,true},{2,false}};
    static HashSet<long> None()=>new HashSet<long>();

    [Fact] public void SleepIsNotFinishedUntilTheNativeTransitionEnds()
    {
        var b=new Ballot();b.Tick(Pair(),true,0);b.Respond(2,b.Id,true,1);
        b.Tick(Pair(),true,16);Assert.True(b.Approved);
        int id=b.Id;
        b.Tick(Pair(),None(),false,17,true);
        Assert.Equal(SleepPhase.Sleeping,b.Phase);Assert.False(b.Active);Assert.False(b.NeedsVote(2));
        b.Tick(Pair(),None(),true,30,true);
        Assert.Equal(SleepPhase.Sleeping,b.Phase);Assert.Equal(id,b.Id);
        b.Tick(Pair(),true,31);
        Assert.Equal(SleepPhase.Finished,b.Phase);Assert.Equal("Sleep completed",b.Outcome);
        b.Tick(Pair(),true,50);Assert.False(b.Active);Assert.Equal(id,b.Id);
    }

    [Fact] public void WakeupBedReplicationCannotRearmTheCompletedVote()
    {
        var b=new Ballot();b.Tick(Pair(),true,0);
        b.Tick(Pair(),None(),false,1,true);
        var lingering=Pair();lingering[2]=true;
        b.Tick(lingering,false,14);b.Tick(Pair(),true,15);
        Assert.False(b.Active);
        b.Tick(new Dictionary<long,bool>{{1,false},{2,false}},true,16);
        b.Tick(Pair(),true,17);Assert.True(b.Active);
    }

    [Fact] public void DawnCancelsApprovalAndDoesNotReopenOnALaggingNightFlag()
    {
        var b=new Ballot();b.Tick(Pair(),true,0);b.Respond(2,b.Id,true,1);
        b.Tick(Pair(),false,15);
        Assert.Equal("Morning arrived",b.Outcome);Assert.False(b.Approved);
        b.Tick(Pair(),true,20);Assert.False(b.Active);
    }

    [Fact] public void DawnAlsoReleasesAnAllInBedAttemptThatNeverStartedABallot()
    {
        var b=new Ballot();var all=new Dictionary<long,bool>{{1,true},{2,true}};
        b.Tick(all,true,0);Assert.False(b.Active);
        b.Tick(all,false,1);Assert.Equal(SleepPhase.Finished,b.Phase);Assert.Equal("Morning arrived",b.Outcome);
        int id=b.Id;b.Tick(all,false,2);Assert.Equal(id,b.Id);
    }

    [Fact] public void LateJoinDuringGraceGetsARealVoteWindow()
    {
        var b=new Ballot();b.Tick(Pair(),true,0);b.Respond(2,b.Id,true,59);
        var three=Pair();three[3]=false;b.Tick(three,true,65);
        Assert.True(b.Active);Assert.Equal(60,b.Remaining);Assert.True(b.Respond(3,b.Id,true,66));
        b.Tick(three,true,81);Assert.True(b.Approved);
    }

    [Fact] public void FormerBedSleeperGetsFreshConsentWindowDuringGrace()
    {
        var people=Pair();people[3]=true;
        var b=new Ballot();b.Tick(people,true,0);b.Respond(2,b.Id,true,59);
        people[3]=false;b.Tick(people,true,65);
        Assert.True(b.NeedsVote(3));Assert.True(b.Respond(3,b.Id,true,66));
        b.Tick(people,true,81);Assert.True(b.Approved);
    }

    [Fact] public void MissingHeartbeatsPauseWithoutInventingCombat()
    {
        var b=new Ballot();b.Tick(Pair(),None(),true,0,false,new HashSet<long>{2});
        b.Tick(Pair(),None(),true,90,false,new HashSet<long>{2});
        Assert.Equal(SleepPhase.WaitingForPlayers,b.Phase);Assert.Equal(60,b.Remaining);
        Assert.False(b.IsInCombat(2));Assert.False(b.NeedsVote(2));
        b.Tick(Pair(),true,91);Assert.True(b.NeedsVote(2));Assert.Equal(60,b.Remaining);
    }

    [Fact] public void AllVotesDuringCombatDoNotStartGraceUntilCombatEnds()
    {
        var b=new Ballot();b.Tick(Pair(),new HashSet<long>{1},true,0);
        Assert.True(b.Respond(2,b.Id,true,1));Assert.Equal(0,b.ReadyAt);
        b.Tick(Pair(),new HashSet<long>{1},true,100);Assert.Equal(0,b.ReadyAt);
        b.Tick(Pair(),true,101);Assert.Equal(15,b.Remaining);
        b.Tick(Pair(),true,116);Assert.True(b.Approved);
    }

    [Fact] public void MissingCharacterDoesNotRearmADeniedVote()
    {
        var b=new Ballot();b.Tick(Pair(),true,0);b.Respond(2,b.Id,false,1);
        var noBed=new Dictionary<long,bool>{{1,false},{2,false}};
        b.Tick(noBed,None(),true,2,false,new HashSet<long>{1});
        b.Tick(Pair(),true,3);Assert.False(b.Active);
    }

    [Theory]
    [InlineData(449.9,true)]
    [InlineData(450,false)]
    [InlineData(451,false)]
    [InlineData(1499.9,false)]
    [InlineData(1500,true)]
    [InlineData(2999,true)]
    [InlineData(3000,true)]
    [InlineData(3450,false)]
    public void RawClockWindowClosesExactlyAtMorning(double time,bool expected)
        =>Assert.Equal(expected,SleepWindow.IsOpen(time,3000,450,1500));
}

public class RetryAndSuccessTests
{
    static Dictionary<long,bool> People()=>new Dictionary<long,bool>{{1,true},{2,true},{3,false}};

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OneSleeperCanRestartCancelledOrExpiredVoteWhileAnotherStaysInBed(bool declined)
    {
        var b=new Ballot();var people=People();b.Tick(people,true,0);int old=b.Id;
        if(declined)b.Respond(3,old,false,1);
        else b.Tick(people,true,60);
        b.Tick(people,true,61);Assert.False(b.Active);Assert.Equal(old,b.Id);
        people[1]=false;b.Tick(people,true,62);Assert.False(b.Active);
        people[1]=true;b.Tick(people,true,63);
        Assert.True(b.Active);Assert.Equal(old+1,b.Id);Assert.Equal(60,b.Remaining);
        Assert.False(b.Respond(3,old,true,64));Assert.True(b.NeedsVote(3));
        b.Tick(people,true,65);Assert.Equal(old+1,b.Id);
    }

    [Fact] public void SimultaneousFreshBedEntriesCreateOnlyOneNewVote()
    {
        var b=new Ballot();var people=People();b.Tick(people,true,0);int old=b.Id;
        b.Respond(3,old,false,1);
        people[1]=people[2]=false;b.Tick(people,true,2);
        people[1]=people[2]=true;b.Tick(people,true,3);
        Assert.Equal(old+1,b.Id);Assert.True(b.Active);
        b.Tick(people,true,3);b.Tick(people,true,4);Assert.Equal(old+1,b.Id);
    }

    [Fact] public void SuccessfulSleepBlocksFreshBedEntriesUntilTheNextSleepWindow()
    {
        var b=new Ballot();var people=People();b.Tick(people,true,0);
        b.Respond(3,b.Id,true,1);b.Tick(people,true,16);Assert.True(b.Approved);
        int old=b.Id;b.Tick(people,new HashSet<long>(),true,17,true);
        people[1]=people[2]=false;b.Tick(people,true,30);
        people[1]=true;b.Tick(people,true,31);Assert.False(b.Active);Assert.Equal(old,b.Id);
        people[1]=false;b.Tick(people,false,32);
        people[1]=true;b.Tick(people,false,33);Assert.False(b.Active);Assert.Equal(old,b.Id);
        people[1]=false;b.Tick(people,false,34);
        // The actual clock opens the next window at afternoon, not visual sunrise.
        b.Tick(people,true,1000);people[1]=true;b.Tick(people,true,1001);
        Assert.True(b.Active);Assert.Equal(old+1,b.Id);
    }

    [Fact] public void BedMovementsDuringAnActiveVoteDoNotCreateAdditionalBallots()
    {
        var b=new Ballot();var people=People();b.Tick(people,true,0);int id=b.Id;
        for(int t=1;t<50;t++)
        {
            people[1]=!people[1];b.Tick(people,true,t);
            Assert.True(b.Active);Assert.Equal(id,b.Id);
        }
    }
}
