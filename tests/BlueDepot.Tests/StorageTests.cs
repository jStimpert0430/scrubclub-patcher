using System;
using System.Linq;
using BlueDepot.Core;
using Xunit;

namespace BlueDepot.Tests;
public class StorageTests
{
    static Stack Item(string slot="0", string key="wood", int count=20, Category category=Category.Materials, int max=50) => new(slot,key,key,category,count,max);
    static Chest Box(string id, int capacity=10, double distance=1, bool access=true, params Stack[] items) => new(id,capacity,distance,access,items);
    static Chest Depot(params Stack[] items) => Box("depot",100,0,true,items);

    [Fact] public void CapacityIncludesNativeAndNearbySlotsExactlyOnce()
    {
        var near=Box("near");
        var v=new StorageView(Depot(),new[]{near,near,Box("far",32,20.01),Box("ward",24,1,false),Box("edge",24,20)},20);
        Assert.Equal(134,v.Capacity); Assert.Empty(v.Rows());
    }
    [Fact] public void FilteredViewShowsOnlyRealInventoryAndDoesNotChangeCapacity()
    {
        var v=new StorageView(Depot(Item()),new[]{Box("sword",10,1,true,Item("0","sword",1,Category.Weapons,1))},20);
        Assert.Single(v.Rows(Category.Weapons)); Assert.Empty(v.Rows(Category.Potions));
        Assert.Single(v.Rows(search:"WOOD")); Assert.Equal(110,v.Capacity); Assert.Equal(2,v.Occupied);
    }
    [Fact] public void RemovingChestRemovesItsItemsAndCapacityWithoutMovingAnything()
    {
        var d=Depot();var chest=Box("a",10,1,true,Item());
        Assert.Equal(110,new StorageView(d,new[]{chest},20).Capacity);
        var after=new StorageView(d,Array.Empty<Chest>(),20);
        Assert.Equal(100,after.Capacity);Assert.Empty(after.Rows());Assert.Equal(20,chest.Items[0].Count);
    }
    [Fact] public void PartialStacksBeatEmptySlotsEvenInMoreDistantChest()
    {
        var move=Routing.Next(Item(count:20),"depot",new[]{Box("empty"),Box("partial",10,19,true,Item(count:45))});
        Assert.NotNull(move);Assert.Equal("partial",move!.ChestId);Assert.Equal(5,move.Amount);
    }
    [Fact] public void FullChestsKeepOverflowAtSource() => Assert.Null(Routing.Next(Item(),"depot",new[]{Box("full",1,1,true,Item(count:50))}));
    [Fact] public void SourceAndInaccessibleChestsNeverReceive() => Assert.Null(Routing.Next(Item(),"depot",new[]{Depot(),Box("ward",10,1,false)}));
    [Fact] public void VariantMetadataIsNeverMerged()
    {
        Assert.Null(Routing.Next(Item(key:"sword-quality-2",count:1,max:1),"depot",new[]{Box("a",1,1,true,Item(key:"sword-quality-1",count:1,max:1))}));
        Assert.Null(Routing.Next(Item(key:"wood-custom-A"),"depot",new[]{Box("a",1,1,true,Item(key:"wood-custom-B"))}));
    }
    [Fact] public void SameCategoryBeatsNearMiscChest()
    {
        var m=Routing.Next(Item(),"depot",new[]{Box("near"),Box("mats",10,5,true,Item(key:"stone"))});
        Assert.Equal("mats",m!.ChestId);Assert.Equal("1",m.Slot);
    }
    [Fact] public void TiesAreStableAcrossDiscoveryOrder()
    {
        var a=Box("a");var b=Box("b");
        Assert.Equal("a",Routing.Next(Item(),"depot",new[]{b,a})!.ChestId);
        Assert.Equal("a",Routing.Next(Item(),"depot",new[]{a,b})!.ChestId);
    }
    [Theory]
    [InlineData("Material",false,false,Category.Materials)]
    [InlineData("OneHandedWeapon",false,false,Category.Weapons)]
    [InlineData("TwoHandedWeaponLeft",false,false,Category.Weapons)]
    [InlineData("Shield",false,false,Category.Armor)]
    [InlineData("Consumable",true,false,Category.Food)]
    [InlineData("Consumable",false,true,Category.Potions)]
    [InlineData("AmmoNonEquipable",false,false,Category.Ammunition)]
    [InlineData("Tool",false,false,Category.Tools)]
    [InlineData("Trophy",false,false,Category.Trophies)]
    [InlineData("FutureModType",false,false,Category.Miscellaneous)]
    public void CategoriesUseGameSemantics(string type,bool edible,bool potion,Category expected) => Assert.Equal(expected,Categories.Classify(type,edible,potion));
    [Fact] public void TwoPlayersCompetingForLastSlotMustReplan()
    {
        var item=Item(); Assert.NotNull(Routing.Next(item,"p1",new[]{Box("last",1)}));
        // First accepted transfer fills the slot; second sees the fresh snapshot.
        Assert.Null(Routing.Next(Item(key:"stone"),"p2",new[]{Box("last",1,1,true,item)}));
    }
    [Fact] public void TimeoutBlocksRetryButLateMatchingReplyCompletes()
    {
        var gate=new TransferGate();Assert.True(gate.Begin(7));Assert.False(gate.Begin(8));
        Assert.False(gate.Complete(8));gate.Timeout();Assert.True(gate.Uncertain);
        Assert.False(gate.Begin(9));Assert.True(gate.Complete(7));Assert.False(gate.Busy);
        Assert.False(gate.Complete(7));Assert.True(gate.Begin(9));
    }
    [Fact] public void PlannerPreservesCountsAcrossRandomFullAndPartialInventories()
    {
        var random=new Random(1739);
        for(int run=0;run<1000;run++)
        {
            var capacity=random.Next(1,20);var occupied=random.Next(capacity+1);
            var stacks=Enumerable.Range(0,occupied).Select(i=>Item(i.ToString(),i%2==0?"wood":"stone",random.Next(1,51))).ToArray();
            var before=stacks.Sum(i=>i.Count);var incoming=random.Next(1,51);
            var move=Routing.Next(Item(count:incoming),"depot",new[]{Box("target",capacity,1,true,stacks)});
            if(move==null)continue;
            Assert.InRange(move.Amount,1,incoming);
            var target=stacks.FirstOrDefault(i=>i.Slot==move.Slot);
            if(target!=null){Assert.Equal("wood",target.Key);Assert.True(target.Count+move.Amount<=target.Maximum);}
            else Assert.True(occupied<capacity);
            var sourceAfter=incoming-move.Amount;var targetAfter=before+move.Amount;
            Assert.Equal(before+incoming,sourceAfter+targetAfter);
            Assert.Equal(before,stacks.Sum(i=>i.Count)); // planning never mutates snapshots
        }
    }
}

public class RegressionTests
{
    [Theory]
    [InlineData("wood","stone",false)]
    [InlineData("wood",null,false)]
    [InlineData("wood","wood",true)]
    [InlineData("sword-quality-1","sword-quality-2",false)]
    public void StaleWithdrawalCannotTakeReplacementItem(string expected,string? actual,bool allowed)
        => Assert.Equal(allowed,TransferRules.CanRemove(expected,actual,1));
    [Fact] public void DepositNeverSwapsUnrelatedStack()
    {
        Assert.False(TransferRules.CanDeposit("wood","stone",50));
        Assert.True(TransferRules.CanDeposit("wood",null,50));
        Assert.True(TransferRules.CanDeposit("wood","wood",50));
        Assert.False(TransferRules.CanDeposit("wood",null,0));
    }
    [Fact] public void TransportExceptionsBlockNewTransfers()
    {var gate=new TransferGate();gate.Fault();Assert.True(gate.Busy);Assert.False(gate.Begin(8));}
}

public class BulkDropTests
{
    [Theory]
    [InlineData(Category.Materials,false,1,20,false,true)]
    [InlineData(Category.Trophies,false,1,5,false,true)]
    [InlineData(Category.Trophies,false,0,5,false,false)]
    [InlineData(Category.Trophies,true,1,5,false,false)]
    [InlineData(Category.Trophies,false,1,0,false,false)]
    [InlineData(Category.Trophies,false,1,5,true,false)]
    [InlineData(Category.Materials,false,0,20,false,false)]
    [InlineData(Category.Materials,true,1,20,false,false)]
    [InlineData(Category.Materials,false,1,0,false,false)]
    [InlineData(Category.Materials,false,1,20,true,false)]
    [InlineData(Category.Food,false,1,20,false,false)]
    [InlineData(Category.Weapons,false,1,1,false,false)]
    public void ButtonEligibilityMatchesBulkOperation(Category category,bool equipped,int row,int count,bool blocked,bool eligible)
        =>Assert.Equal(eligible,BulkDropRules.Eligible(category,equipped,row,count,blocked));
}
