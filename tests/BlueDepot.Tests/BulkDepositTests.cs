using BlueDepot.Core;
using Xunit;
namespace BlueDepot.Tests;
public class BulkDepositTests
{
    [Fact] public void RefundedReplacementStackRemainsEligibleAfterDecline()
    {
        var pass=new BulkDepositPass();pass.Include("wood",50);
        pass.Observe("wood",50,50,true);
        Assert.Equal(50,pass.Allowance("wood"));
        pass.Observe("wood",50,0,true);
        Assert.Equal(0,pass.Allowance("wood"));
    }
    [Fact] public void MultipleStacksAndPartialDepositsDrainOriginalBudget()
    {
        var pass=new BulkDepositPass();pass.Include("ore",30);pass.Include("ore",30);
        pass.Observe("ore",60,50,true);
        Assert.Equal(50,pass.Allowance("ore"));
        pass.Observe("ore",50,20,true);pass.Observe("ore",20,0,true);
        Assert.Equal(0,pass.Allowance("ore"));
    }
    [Fact] public void RepeatedDeclinesAreBoundedWithoutBlockingOtherMaterials()
    {
        var pass=new BulkDepositPass();pass.Include("wood",50);pass.Include("stone",20);
        for(int i=0;i<3;i++)pass.Observe("wood",50,50,true);
        Assert.Equal(0,pass.Allowance("wood"));Assert.Equal(20,pass.Allowance("stone"));
    }
    [Fact] public void UnconfirmedSourceRemovalMustNotBeRetried()
    {
        var pass=new BulkDepositPass();pass.Include("wood",50);
        pass.Observe("wood",50,25,false);
        Assert.Equal(0,pass.Allowance("wood"));
    }
    [Fact] public void ProgressResetsDeclineLimitAndCapacityFailureSkipsOnlyThatItem()
    {
        var pass=new BulkDepositPass();pass.Include("wood",50);pass.Include("stone",10);
        pass.Observe("wood",50,50,true);pass.Observe("wood",50,50,true);
        pass.Observe("wood",50,40,true);pass.Observe("wood",40,40,true);
        Assert.Equal(40,pass.Allowance("wood"));pass.NoSpace("wood");
        Assert.Equal(0,pass.Allowance("wood"));Assert.Equal(10,pass.Allowance("stone"));
    }
}
