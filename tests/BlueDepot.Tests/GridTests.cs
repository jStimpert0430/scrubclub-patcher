using System.Collections.Generic;
using BlueDepot.Core;
using Xunit;
namespace BlueDepot.Tests;

public class GridTests
{
    [Theory]
    [InlineData(0,1)] [InlineData(1,1)] [InlineData(24,1)] [InlineData(25,2)] [InlineData(48,2)] [InlineData(100,5)]
    public void EmptyTrayAndOverflowKeepCorrectPages(int items,int pages)=>Assert.Equal(pages,GridRules.Pages(items));
    [Fact] public void RemovingLastStackOrChangingFiltersClampsThePage()
    {Assert.Equal(0,GridRules.ClampPage(4,24));Assert.Equal(0,GridRules.ClampPage(4,0));Assert.Equal(3,GridRules.ClampPage(3,100));}
    [Theory]
    [InlineData(5,20,50,5)] [InlineData(20,20,3,3)] [InlineData(20,4,50,4)]
    [InlineData(20,20,0,0)] [InlineData(-1,20,50,0)]
    public void SplitAndQuickMovesCannotExceedRequestedStackOrDestinationSpace(int requested,int stack,int capacity,int moved)
        =>Assert.Equal(moved,GridRules.MoveAmount(requested,stack,capacity));
    [Fact] public void IntakeSortQueueSurvivesPersistenceWithExactItemIdentity()
    {
        var original=new Dictionary<int,string>{{0,"wood,quality:1\ncustom=♥"},{99,"trophy"}};
        var restored=IntakeQueue.Decode(IntakeQueue.Encode(original));
        Assert.Equal(original.Count,restored.Count);
        foreach(var pair in original)Assert.Equal(pair.Value,restored[pair.Key]);
        Assert.False(restored.ContainsKey(1)); // pre-existing internal items are not queued
    }
    [Fact] public void QueueIgnoresInvalidSlotsAndCorruptEntries()
    {
        Assert.Empty(IntakeQueue.Decode("-1,d29vZA==\n100,d29vZA==\n5,***"));
        Assert.Empty(IntakeQueue.Decode(IntakeQueue.Encode(new Dictionary<int,string>{{100,"wood"}})));
    }
}
