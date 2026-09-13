using System.Collections.Generic;
using SleepVote.Core;
using Xunit;
namespace SleepVote.Tests;
public class BallotTests
{
    static Dictionary<long,bool> Pair()=>new Dictionary<long,bool>{{1,true},{2,false}};
    [Fact] public void UnanimousApprovalStillWaitsThirtySeconds()
    {
        var b=new Ballot();b.Tick(Pair(),true,0);
        Assert.True(b.NeedsVote(2));Assert.False(b.NeedsVote(1));
        Assert.True(b.Respond(2,b.Id,true,5));
        b.Tick(Pair(),true,34.9);Assert.False(b.Approved);
        b.Tick(Pair(),true,35);Assert.True(b.Approved);
    }
    [Fact] public void LastApprovalStartsGraceEvenNearVoteDeadline()
    {
        var b=new Ballot();b.Tick(Pair(),true,0);b.Respond(2,b.Id,true,59);
        b.Tick(Pair(),true,65);Assert.True(b.Active);Assert.False(b.Approved);
        b.Tick(Pair(),true,89);Assert.True(b.Approved);
    }
    [Fact] public void NoAndTimeoutDoNotImmediatelyReprompt()
    {
        foreach(bool decline in new[]{false,true})
        {
            var b=new Ballot();b.Tick(Pair(),true,0);
            if(decline)b.Respond(2,b.Id,false,1);
            b.Tick(Pair(),true,60);Assert.False(b.Active);
            b.Tick(Pair(),true,100);Assert.False(b.Active);
            b.Tick(new Dictionary<long,bool>{{1,false},{2,false}},true,101);
            b.Tick(Pair(),true,102);Assert.True(b.Active);
        }
    }
    [Fact] public void JoiningPlayerMustAgreeAndRestartsGrace()
    {
        var b=new Ballot();b.Tick(Pair(),true,0);b.Respond(2,b.Id,true,1);
        var three=Pair();three.Add(3,false);b.Tick(three,true,20);
        Assert.False(b.Approved);Assert.True(b.NeedsVote(3));
        b.Respond(3,b.Id,true,25);b.Tick(three,true,40);Assert.False(b.Approved);
        b.Tick(three,true,55);Assert.True(b.Approved);
    }
    [Fact] public void DepartingVoterIsRemovedButAtLeastOneBedRequired()
    {
        var b=new Ballot();b.Tick(Pair(),true,0);
        b.Tick(new Dictionary<long,bool>{{1,true}},true,5);
        b.Tick(new Dictionary<long,bool>{{1,true}},true,35);Assert.True(b.Approved);
        b.Tick(new Dictionary<long,bool>(),true,36);Assert.False(b.Active);
    }
    [Fact] public void LeavingBedRequiresConsentAndDaylightCancels()
    {
        var b=new Ballot();var three=Pair();three.Add(3,true);b.Tick(three,true,0);b.Respond(2,b.Id,true,1);
        three[3]=false;b.Tick(three,true,31);Assert.False(b.Approved);Assert.True(b.NeedsVote(3));
        b.Tick(three,false,32);Assert.False(b.Active);
    }
    [Fact] public void RejectsUnknownDuplicateStaleAndExpiredReplies()
    {
        var b=new Ballot();b.Tick(Pair(),true,0);
        Assert.False(b.Respond(999,b.Id,true,1));Assert.False(b.Respond(2,b.Id+1,true,1));
        Assert.False(b.Respond(1,b.Id,true,1));Assert.False(b.Respond(2,b.Id,true,60));
        Assert.True(b.Respond(2,b.Id,true,2));Assert.False(b.Respond(2,b.Id,false,3));
    }
    [Fact] public void NoVoteDuringDayOrWithoutAnyoneInBed()
    {
        var b=new Ballot();b.Tick(Pair(),false,0);Assert.False(b.Active);
        b.Tick(new Dictionary<long,bool>{{1,false},{2,false}},true,1);Assert.False(b.Active);
    }
    [Fact] public void AllAlreadyInBedLeavesVanillaSleepUntouched()
    {
        var b=new Ballot();b.Tick(new Dictionary<long,bool>{{1,true},{2,true}},true,0);Assert.False(b.Active);
    }
}
