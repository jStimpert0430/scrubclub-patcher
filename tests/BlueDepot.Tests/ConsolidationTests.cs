using System.Collections.Generic;
using System.Linq;
using BlueDepot.Core;
using Xunit;
using Stack=BlueDepot.Core.Stack;
namespace BlueDepot.Tests;

public class ConsolidationTests
{
    static Stack Item(string slot,int count,string key="wood",Category category=Category.Materials)=>new(slot,key,key,category,count,50);
    static Chest Box(string id,params Stack[] items)=>new(id,10,1,true,items);
    [Fact] public void ConsolidationFillsEarlierStacksWithoutMovingThemBack()
    {
        var chests=new[]{Box("a",Item("0",40)),Box("b",Item("0",30))};
        Assert.Null(Consolidation.Next(chests,"a"));
        var move=Consolidation.Next(chests,"b")!;
        Assert.Equal("a",move.Target.ChestId);Assert.Equal(10,move.Target.Amount);
        Assert.Null(Consolidation.Next(new[]{Box("a",Item("0",50)),Box("b",Item("0",20))},"b"));
    }
    [Fact] public void SameChestPartialStacksCanBeConsolidated()
    {
        var move=Consolidation.Next(new[]{Box("a",Item("0",20),Item("1",25))},"a")!;
        Assert.Equal("1",move.SourceSlot);Assert.Equal("0",move.Target.Slot);Assert.Equal(25,move.Target.Amount);
    }
    [Fact] public void DifferentQualityAndCustomDataNeverMerge()
    {Assert.Null(Consolidation.Next(new[]{Box("a",Item("0",10,"fish-quality1")),Box("b",Item("0",10,"fish-quality2"))},"b"));}
    [Fact] public void InaccessibleInventoriesAreExcluded()
    {
        var locked=new Chest("a",10,1,false,new[]{Item("0",5)});
        Assert.Null(Consolidation.Next(new[]{locked,Box("b",Item("0",10))},"b"));
    }
    [Fact] public void RepeatedConsolidationConservesItemsAndTerminates()
    {
        var quantities=new Dictionary<string,int>{{"a",12},{"b",18},{"c",40},{"d",10}};
        int moves=0;
        for(int pass=0;pass<10;pass++)foreach(var source in quantities.Keys.ToArray())
        {
            var snapshot=quantities.Select(p=>p.Value>0?Box(p.Key,Item("0",p.Value)):Box(p.Key)).ToArray();
            var move=Consolidation.Next(snapshot,source);if(move==null)continue;
            quantities[source]-=move.Target.Amount;quantities[move.Target.ChestId]+=move.Target.Amount;moves++;
        }
        Assert.Equal(80,quantities.Values.Sum());Assert.Equal(2,quantities.Values.Count(n=>n>0));
        Assert.InRange(moves,1,8);
    }
    [Theory]
    [InlineData(false,false,false,DepositMode.Automatic)]
    [InlineData(true,false,false,DepositMode.Intake)]
    [InlineData(false,true,false,DepositMode.Internal)]
    [InlineData(true,false,true,DepositMode.Automatic)]
    [InlineData(false,true,true,DepositMode.Automatic)]
    public void IntakeAndInternalOverrideFiltersButBulkAlwaysRoutesImmediately(bool intake,bool native,bool immediate,DepositMode mode)
        =>Assert.Equal(mode,DepositRules.Mode(intake,native,immediate));
    [Fact] public void MeatRoutesTowardFoodStorageRegardlessOfTheSelectedCategoryTab()
    {
        var meat=Item("0",5,"RawMeat",Category.Food);
        var armor=Box("armor",Item("0",1,"Helmet",Category.Armor));
        var food=Box("food",Item("0",1,"Carrot",Category.Food));
        Assert.Equal("food",Routing.Next(meat,"player",new[]{armor,food})!.ChestId);
    }
    [Fact] public void UnknownItemTypesHaveVisibleStorageAndADestination()
    {
        Assert.Equal(Category.Miscellaneous,Categories.Classify("FutureModItem"));
        Assert.NotNull(Routing.Next(Item("0",1,"ModItem",Category.Miscellaneous),"player",new[]{Box("depot")}));
    }
    [Fact] public void GridUsesCategoryThenPrefabIdInsteadOfTranslatedName()
    {
        var depot=Box("depot",new Stack("0","k1","Apple",Category.Food,1,50,"ZFood"),
            new Stack("1","k2","Zebra",Category.Food,1,50,"AFood"),Item("2",1,"Helmet",Category.Armor));
        var view=new StorageView(depot,new Chest[0],20);
        Assert.Equal(new[]{"Helmet","AFood","ZFood"},view.Rows().Select(r=>r.Item.ItemId));
    }
}
