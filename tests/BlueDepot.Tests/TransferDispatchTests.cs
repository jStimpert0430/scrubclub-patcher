using System;
using BlueDepot.Core;
using Xunit;
namespace BlueDepot.Tests;
public class TransferDispatchTests
{
    [Fact] public void LocallyOwnedMoveCompletesBeforeSendReturnsWithoutLockingDepot()
    {
        var gate=new TransferGate();
        Assert.True(gate.BeginDispatch());
        // Valheim's local RPC invokes the owner and response immediately on this stack.
        Assert.True(gate.Complete(42));
        gate.EndDispatch(42);
        Assert.False(gate.Busy);Assert.Null(gate.Pending);
        Assert.True(gate.BeginDispatch());gate.EndDispatch(null);Assert.False(gate.Busy);
    }
    [Fact] public void RemoteMoveRemainsLockedUntilMatchingResponse()
    {
        var gate=new TransferGate();Assert.True(gate.BeginDispatch());gate.EndDispatch(42);
        Assert.True(gate.Busy);Assert.False(gate.Complete(99));Assert.True(gate.Busy);
        Assert.True(gate.Complete(42));Assert.False(gate.Busy);
    }
    [Fact] public void UnrelatedEarlyResponseCannotUnlockOutstandingRequest()
    {
        var gate=new TransferGate();gate.BeginDispatch();gate.Complete(99);gate.EndDispatch(42);
        Assert.True(gate.Busy);Assert.Equal(42,gate.Pending);
    }
    [Fact] public void ActualTimeoutNeverAutomaticallyRefundsRetriesOrUnlocks()
    {
        var gate=new TransferGate();gate.BeginDispatch();gate.EndDispatch(42);gate.Timeout();
        Assert.True(gate.Uncertain);Assert.False(gate.BeginDispatch());Assert.False(gate.Complete(99));
        Assert.True(gate.Complete(42));Assert.False(gate.Busy);
    }
    [Fact] public void ExceptionsDuringSendRemainUncertain()
    {
        var gate=new TransferGate();gate.BeginDispatch();gate.Fault();gate.EndDispatch(null);
        Assert.True(gate.Busy);Assert.True(gate.Uncertain);Assert.False(gate.Dispatching);
    }
    [Fact] public void LocalSynchronousHelpersReturningNullRemainUsable()
    {
        var gate=new TransferGate();gate.BeginDispatch();gate.Complete(7);gate.EndDispatch(null);
        Assert.False(gate.Busy);Assert.False(gate.Complete(7));
    }
    [Fact] public void DispatchRejectsReentrantWorkAndDoesNotReuseEarlierReceipts()
    {
        var gate=new TransferGate();gate.BeginDispatch();Assert.False(gate.BeginDispatch());
        gate.Complete(42);gate.EndDispatch(42);gate.BeginDispatch();gate.EndDispatch(42);
        Assert.True(gate.Busy);
    }
    [Theory] [InlineData(true,false,true)] [InlineData(true,true,false)] [InlineData(false,false,false)]
    public void CarriedFuelCanUseVanillaButInventoryLocksRemainProtected(bool carried,bool locked,bool vanilla)
        =>Assert.Equal(vanilla,StationSupplyRules.UseCarriedFirst(carried,locked));
}
