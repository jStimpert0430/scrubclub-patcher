using System;
using BlueDepot.Core;
using Xunit;

namespace BlueDepot.Tests;

public class CraftingTests
{
    [Fact] public void PreferCarriedQualityOverFetchingLowerQuality()
    {
        var chosen=CraftingPlan.ChooseQuality("Fish",3,3,(_,q)=>q==2?3:0,(_,_)=>10);
        Assert.Equal(2,chosen!.Quality);
    }
    [Fact] public void QualitySelectionNeverCombinesInsufficientQualities()
    {Assert.Null(CraftingPlan.ChooseQuality("Fish",3,3,(_,_)=>0,(_,_)=>2));}
    [Fact] public void QualitySelectionUsesAnAvailableChestQuality()
    {Assert.Equal(3,CraftingPlan.ChooseQuality("Fish",3,3,(_,_)=>0,(_,q)=>q==3?5:0)!.Quality);}
    [Fact] public void PullsOnlyMissingMaterials()
    {
        var wood=new Ingredient("Wood",20);
        Assert.Equal(13,CraftingPlan.Missing(wood,(_,_)=>7));
        Assert.Equal(0,CraftingPlan.Missing(wood,(_,_)=>25));
    }
    [Fact] public void DuplicateRequirementsCannotSpendTheSameStackTwice()
    {
        var recipes=new[]{new[]{new Ingredient("Wood",10),new Ingredient("Wood",15)}};
        Assert.Null(CraftingPlan.Select(recipes,(_,_)=>20));
        var plan=CraftingPlan.Select(recipes,(_,_)=>25);
        Assert.Equal(25,Assert.Single(plan!).Amount);
    }
    [Fact] public void AlternativesNeverCombinePartialIngredients()
    {
        var alternatives=new[]{new[]{new Ingredient("Wood",10)},new[]{new Ingredient("Stone",10)}};
        Assert.Null(CraftingPlan.Select(alternatives,(_,_)=>5));
    }
    [Fact] public void SelectsTheFirstCompleteAlternative()
    {
        var alternatives=new[]{new[]{new Ingredient("Wood",10)},new[]{new Ingredient("Stone",10)}};
        Assert.Equal("Stone",Assert.Single(CraftingPlan.Select(alternatives,(name,_)=>name=="Stone"?10:0)!).Name);
    }
    [Fact] public void DistinctQualityRequirementsStayDistinct()
    {
        var requirements=new[]{new Ingredient("Fish",3,1),new Ingredient("Fish",3,2)};
        Assert.Null(CraftingPlan.Select(new[]{requirements},(_,quality)=>quality==1?6:0));
        Assert.Equal(2,CraftingPlan.Select(new[]{requirements},(_,_)=>3)!.Length);
    }
    [Fact] public void RechecksInventoryAfterPartialReceiptInsteadOfRepeatingOriginalAmount()
    {
        var ingredient=new Ingredient("Wood",20);
        int held=2;
        Assert.Equal(18,CraftingPlan.Missing(ingredient,(_,_)=>held));
        held+=10;
        Assert.Equal(8,CraftingPlan.Missing(ingredient,(_,_)=>held));
        held+=8;
        Assert.Equal(0,CraftingPlan.Missing(ingredient,(_,_)=>held));
    }
    [Fact] public void AnyMissingRequiredIngredientRejectsThePlan()
    {
        Assert.Null(CraftingPlan.Select(new[]{new[]{new Ingredient("Wood",20),new Ingredient("Blueberries",5)}},(name,_)=>name=="Wood"?100:4));
    }
    [Fact] public void NoCostRecipeRequiresNoTransfers()
    {Assert.Empty(CraftingPlan.Select(new[]{Array.Empty<Ingredient>()},(_,_)=>0)!);}
    [Fact] public void EmptyAlternativeListCannotBecomeAFreeRecipe()
    {Assert.Null(CraftingPlan.Select(Array.Empty<Ingredient[]>(),(_,_)=>100));}
    [Fact] public void InvalidAmountsFailInsteadOfWrappingIntoFreeResources()
    {
        Assert.Throws<ArgumentException>(()=>new Ingredient("Wood",-1));
        Assert.Throws<OverflowException>(()=>CraftingPlan.Select(new[]{new[]{new Ingredient("Wood",int.MaxValue),new Ingredient("Wood",1)}},(_,_)=>int.MaxValue));
    }
}
