using System;
using System.Linq;
using BlueDepot.Core;
using Xunit;
namespace BlueDepot.Tests;
public class WorkbenchNetworkTests
{
    static WorkbenchArea Bench(string id,double x,double z=0,double radius=20)=>new(id,x,z,radius);
    [Fact] public void OverlappingChainReachesStorageBeyondPlayerRadius()
    {
        var connected=WorkbenchNetwork.Connected(new[]{Bench("a",0),Bench("b",35),Bench("c",70)},0,0);
        Assert.Equal(3,connected.Length);Assert.True(WorkbenchNetwork.Covers(connected,85,0));Assert.False(WorkbenchNetwork.Covers(connected,95,0));
    }
    [Fact] public void BreakingRelayRemovesRemoteStorageAccess()
    {
        var connected=WorkbenchNetwork.Connected(new[]{Bench("a",0),Bench("c",70)},0,0);
        Assert.Single(connected);Assert.False(WorkbenchNetwork.Covers(connected,70,0));
    }
    [Fact] public void PlayerMustActuallyStandInsideTheNetwork()
    {
        Assert.Empty(WorkbenchNetwork.Connected(new[]{Bench("a",0)},21,0));
        Assert.Empty(WorkbenchNetwork.Connected(new[]{Bench("a",0)},20,0));
    }
    [Fact] public void TouchingEdgesDoNotBridgeButPositiveOverlapDoes()
    {
        Assert.Single(WorkbenchNetwork.Connected(new[]{Bench("a",0),Bench("b",40)},0,0));
        Assert.Equal(2,WorkbenchNetwork.Connected(new[]{Bench("a",0),Bench("b",39.99)},0,0).Length);
    }
    [Fact] public void UnequalUpgradedRangesUseSumOfRadii()
    {
        Assert.Equal(2,WorkbenchNetwork.Connected(new[]{Bench("a",0,radius:30),Bench("b",45)},0,0).Length);
        Assert.Single(WorkbenchNetwork.Connected(new[]{Bench("a",0),Bench("b",45)},0,0));
    }
    [Fact] public void CyclesAndDuplicateNodesDoNotDuplicateCoverage()
    {
        var a=Bench("a",0);var nodes=new[]{a,a,Bench("b",25),Bench("c",12,20)};
        Assert.Equal(3,WorkbenchNetwork.Connected(nodes,0,0).Length);
    }
    [Fact] public void DisconnectedBasesDoNotShareMaterials()
    {
        var network=WorkbenchNetwork.Connected(new[]{Bench("home",0),Bench("remote",1000)},0,0);
        Assert.Single(network);Assert.False(WorkbenchNetwork.Covers(network,1000,0));
    }
    [Fact] public void ReorderingBenchesDoesNotChangeCoverage()
    {
        var nodes=new[]{Bench("a",0),Bench("b",35),Bench("c",70)};
        Assert.Equal(WorkbenchNetwork.Connected(nodes,0,0).Select(n=>n.Id).OrderBy(x=>x),WorkbenchNetwork.Connected(nodes.Reverse(),0,0).Select(n=>n.Id).OrderBy(x=>x));
    }
    [Fact] public void PlayerMovementReevaluatesWhichComponentIsReachable()
    {
        var nodes=new[]{Bench("a",0),Bench("b",100)};
        Assert.Equal("a",Assert.Single(WorkbenchNetwork.Connected(nodes,0,0)).Id);
        Assert.Equal("b",Assert.Single(WorkbenchNetwork.Connected(nodes,100,0)).Id);
    }
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void InvalidRadiiAreRejected(double radius)=>Assert.Throws<ArgumentException>(()=>Bench("a",0,radius:radius));
    [Fact] public void LargeChainHasNoArbitraryHopLimit()
    {
        var nodes=Enumerable.Range(0,100).Select(i=>Bench(i.ToString(),i*35)).ToArray();
        Assert.Equal(100,WorkbenchNetwork.Connected(nodes,0,0).Length);
    }
}
