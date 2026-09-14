using SleepVote.Core;
using Xunit;
namespace SleepVote.Tests;

public class ClientVoteTests
{
    [Fact] public void InFlightSnapshotCannotReopenASubmittedVote()
    {
        var c=new ClientVote();c.Apply(1,1,true,true);Assert.True(c.CanPrompt);
        Assert.True(c.Submit(1));Assert.False(c.Submit(1));
        c.Apply(1,2,true,true);Assert.False(c.CanPrompt);Assert.True(c.WaitingForReceipt);
        c.Receipt(1,true);c.Apply(1,3,true,true);Assert.False(c.CanPrompt);
        c.Apply(1,4,false,true);Assert.False(c.WaitingForReceipt);
    }

    [Fact] public void RejectedCombatRaceCanBeVotedOnAfterCombat()
    {
        var c=new ClientVote();c.Apply(1,1,true,true);c.Submit(1);
        c.Receipt(1,false);c.Apply(1,2,false,true);Assert.False(c.CanPrompt);
        c.Apply(1,3,true,true);Assert.True(c.CanPrompt);Assert.True(c.Submit(1));
    }

    [Fact] public void OldBallotsAndSnapshotsCannotResurrectThePopup()
    {
        var c=new ClientVote();c.Apply(1,1,true,true);c.Apply(1,3,false,false);
        Assert.False(c.Apply(1,2,true,true));Assert.False(c.CanPrompt);
        c.Apply(2,4,true,true);Assert.True(c.CanPrompt);
        Assert.False(c.Apply(1,5,true,true));Assert.Equal(2,c.Id);
    }

    [Fact] public void NewBallotDoesNotInheritOldAnswersOrReceipts()
    {
        var c=new ClientVote();c.Apply(1,1,true,true);c.Submit(1);
        c.Apply(2,2,true,true);c.Receipt(1,true);Assert.True(c.CanPrompt);Assert.False(c.Submitted);
    }

    [Fact] public void NativeSleepRpcClosesThePromptBeforeAStatusSnapshotArrives()
    {
        var c=new ClientVote();c.Apply(1,1,true,true);c.SleepTransition();
        c.Apply(1,2,true,true);Assert.False(c.CanPrompt);
        c.Apply(1,3,false,false);Assert.False(c.CanPrompt);
        c.Apply(2,4,true,true);Assert.True(c.CanPrompt);
    }

    [Fact] public void CombatDeferralDoesNotCountAsAnswering()
    {
        var c=new ClientVote();c.Apply(1,1,false,true);Assert.False(c.CanPrompt);Assert.False(c.Submit(1));
        c.Apply(1,2,true,true);Assert.True(c.CanPrompt);
    }
    [Fact] public void CompletedBallotCannotBeReopenedEvenByANewerActiveSnapshot()
    {
        var c=new ClientVote();c.Apply(1,1,true,true);c.Apply(1,2,false,false);
        Assert.False(c.Apply(1,3,true,true));Assert.False(c.CanPrompt);
        Assert.True(c.Apply(2,4,true,true));Assert.True(c.CanPrompt);
    }
}
