using System;
using BlueDepot.Core;
using Xunit;

namespace BlueDepot.Tests;

public class DeferredActionTests
{
    [Fact] public void RecipeRefreshSeesChestCountsAfterEveryCraft()
    {
        var execution=new ExecutionWindow();
        int refreshes=0;
        for(int craft=0;craft<3;craft++)
        {
            using(execution.Enter(()=>{Assert.False(execution.Active);refreshes++;}))
                Assert.True(execution.Active);
            Assert.False(execution.Active);
        }
        Assert.Equal(3,refreshes);
    }
    [Fact] public void FailedCraftRestoresCountsAndRefreshes()
    {
        var execution=new ExecutionWindow();bool refreshed=false;
        Assert.Throws<InvalidOperationException>((Action)(()=>
        {
            using(execution.Enter(()=>{Assert.False(execution.Active);refreshed=true;}))
                throw new InvalidOperationException();
        }));
        Assert.True(refreshed);Assert.False(execution.Active);
    }
    [Fact] public void NestedCraftScopeCannotRefreshWithCountsSuppressed()
    {
        var execution=new ExecutionWindow();int refreshed=0;
        var outer=execution.Enter(()=>refreshed++);
        using(execution.Enter(()=>throw new Exception("Premature refresh")))Assert.True(execution.Active);
        Assert.True(execution.Active);Assert.Equal(0,refreshed);
        outer.Dispose();outer.Dispose();
        Assert.False(execution.Active);Assert.Equal(1,refreshed);
    }
    [Fact] public void OneClickCanResumeAnActionOnlyOnce()
    {
        var intent=new DeferredIntent();
        Assert.True(intent.TryConsume(true));
        Assert.False(intent.TryConsume(true));
    }
    [Fact] public void ChangedOrExpiredTargetCannotReviveLater()
    {
        var intent=new DeferredIntent();
        Assert.False(intent.TryConsume(false));
        Assert.False(intent.TryConsume(true));
    }
    [Theory]
    [InlineData(true,true,false,true,false,false,true)]
    [InlineData(false,true,false,true,false,false,false)]
    [InlineData(true,false,false,true,false,false,false)]
    [InlineData(true,true,true,true,false,false,false)]
    [InlineData(true,true,false,false,false,false,false)]
    [InlineData(true,true,false,true,true,false,false)]
    [InlineData(true,true,false,true,false,true,false)]
    public void SuppliesRespectCapacityAccessCarriedItemsExplicitChoiceAndPendingRequests(
        bool valid,bool room,bool carried,bool stored,bool explicitItem,bool busy,bool fetch)
        =>Assert.Equal(fetch,StationSupplyRules.ShouldFetch(valid,room,carried,stored,explicitItem,busy));
}
