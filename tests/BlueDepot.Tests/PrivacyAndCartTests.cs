using BlueDepot.Core;
using Xunit;
namespace BlueDepot.Tests;
public class PrivacyAndCartTests
{
    static Stack Item(string id="CopperOre",int count=5,string slot="0")=>new Stack(slot,id,id,Category.Materials,count,50,id);
    static Chest Box(string id,bool cart=false,bool privacy=false,params Stack[] items)=>new Chest(id,2,2,true,items,privacy,cart);
    [Fact] public void PrivateChestIsAbsentFromTheAggregateButCanBeOpenedDirectly()
    {
        var privateBox=Box("private",false,true,Item());var depot=Box("depot");
        Assert.Single(new StorageView(depot,new[]{privateBox},20).Chests);
        Assert.Single(new StorageView(privateBox,new Chest[0],20).Chests);
    }
    [Fact] public void PrivateDepotShowsOnlyOwnItemsAndCapacityDespitePublicNeighbors()
    {
        var own=Box("private-depot",false,true,Item("Wood"));
        var view=new StorageView(own,new[]{Box("public-depot",false,false,Item()),Box("cart",true),Box("private-box",false,true,Item())},20);
        Assert.Equal("private-depot",Assert.Single(view.Chests).Id);
        Assert.Equal(2,view.Capacity);Assert.Equal(1,view.Occupied);
        Assert.Equal("Wood",Assert.Single(view.Rows()).Item.ItemId);
        Assert.Empty(view.Rows(search:"CopperOre"));
    }
    [Fact] public void PublicDepotExcludesPrivateBoxesAndDepotsIncludingTheirCapacity()
    {
        var view=new StorageView(Box("public-depot"),new[]{Box("private-box",false,true,Item()),Box("private-depot",false,true,Item()),Box("public-box",false,false,Item("Wood"))},20);
        Assert.Equal(2,view.Chests.Count);Assert.Equal(4,view.Capacity);
        Assert.Equal("Wood",Assert.Single(view.Rows()).Item.ItemId);
    }
    [Fact] public void ClearingPrivacyRestoresPublicNeighbors()
    {
        var neighbors=new[]{Box("public-box",false,false,Item()),Box("private-box",false,true,Item())};
        Assert.Single(new StorageView(Box("depot",false,true),neighbors,20).Chests);
        Assert.Equal(2,new StorageView(Box("depot"),neighbors,20).Chests.Count);
    }
    [Fact] public void OrePrefersEmptyCartOverMatchingPartialBox()
    {
        var next=Routing.Next(Item(),"player",new[]{Box("box",false,false,Item(count:45)),Box("cart",true)});
        Assert.Equal("cart",next!.ChestId);
    }
    [Fact] public void OreFillsPartialCartBeforeAnEmptyCart()
    {
        var next=Routing.Next(Item(),"player",new[]{Box("empty",true),Box("partial",true,false,Item(count:49))});
        Assert.Equal("partial",next!.ChestId);Assert.Equal(1,next.Amount);
    }
    [Fact] public void FullCartFallsBackToBoxWithoutReplacingItems()
    {
        var next=Routing.Next(Item(),"player",new[]{Box("cart",true,false,Item(count:50),Item(count:50,slot:"1")),Box("box")});
        Assert.Equal("box",next!.ChestId);
    }
    [Fact] public void PrivateCartIsNeverAnAutomaticDestination()
    {
        Assert.Equal("box",Routing.Next(Item(),"player",new[]{Box("cart",true,true),Box("box")})!.ChestId);
    }
    [Fact] public void NonOreKeepsNormalStackingPriority()
    {
        Assert.Equal("box",Routing.Next(Item("Wood"),"player",new[]{Box("cart",true),Box("box",false,false,Item("Wood",45))})!.ChestId);
    }
    [Fact] public void PrivateOnlyDestinationLeavesOverflowAtSource()
        =>Assert.Null(Routing.Next(Item(),"player",new[]{Box("private",false,true)}));
    [Fact] public void SortDoesNotPullOreOutOfCartToFillBox()
    {
        var boxes=new[]{Box("a-box",false,false,Item(count:45)),Box("z-cart",true,false,Item(count:5))};
        Assert.Null(Consolidation.Next(boxes,"z-cart"));
        Assert.Equal("z-cart",Consolidation.Next(boxes,"a-box")!.Target.ChestId);
    }
    [Fact] public void SortingExcludesPrivateInventories()
        =>Assert.Null(Consolidation.Next(new[]{Box("a",false,true,Item(count:45)),Box("b",false,false,Item())},"b"));
    [Theory]
    [InlineData("CopperOre",true)] [InlineData("IronScrap",true)] [InlineData("CopperScrap",true)]
    [InlineData("BlackMetalScrap",true)] [InlineData("FlametalOre",true)] [InlineData("Copper",false)]
    [InlineData("Wood",false)] [InlineData("Stone",false)]
    public void RawOresAndScrapArePrioritizedButIngotsAreNot(string id,bool ore)=>Assert.Equal(ore,StorageRules.IsOre(id));
    [Fact] public void ModdedNonTeleportableItemUsesCartEvenIfItIsNotOre()
    {
        var item=new Stack("0","sealed","Sealed cargo",Category.Miscellaneous,5,50,"ModdedCargo",true);
        Assert.Equal("cart",Routing.Next(item,"player",new[]{Box("box",false,false,item),Box("cart",true)})!.ChestId);
    }
    [Fact] public void NonTeleportableCartPriorityAlsoSurvivesConsolidation()
    {
        var small=new Stack("0","egg","Dragon egg",Category.Materials,1,10,"DragonEgg",true);
        var large=new Stack("0","egg","Dragon egg",Category.Materials,5,10,"DragonEgg",true);
        var boxes=new[]{Box("a-box",false,false,large),Box("z-cart",true,false,small)};
        Assert.Null(Consolidation.Next(boxes,"z-cart"));
        Assert.Equal("z-cart",Consolidation.Next(boxes,"a-box")!.Target.ChestId);
    }
}
