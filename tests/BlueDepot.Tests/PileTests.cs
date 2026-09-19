using BlueDepot.Core;
using Xunit;
namespace BlueDepot.Tests;
public class PileTests
{
    [Theory]
    [InlineData("wood_stack",true)] [InlineData("stone_pile",true)] [InlineData("wood_fine_stack",true)]
    [InlineData("wood_wall",false)] [InlineData("rock4_coast",false)]
    public void OnlyResourcePilePrefabsQualify(string id,bool expected)=>Assert.Equal(expected,PileRules.IsResourcePile(id,1,true,50,true));
    [Fact] public void MixedRecipesNonMaterialsAndNonRecoverablePiecesAreExcluded()
    {
        Assert.False(PileRules.IsResourcePile("mod_pile",2,true,50,true));
        Assert.False(PileRules.IsResourcePile("mod_pile",1,false,50,true));
        Assert.False(PileRules.IsResourcePile("mod_pile",1,true,50,false));
    }
    [Fact] public void PartialOrEmptySavedPilesAreNeverReseeded()
    {
        Assert.False(PileRules.NeedsInitialization(true,true,true,false));
        Assert.True(PileRules.NeedsInitialization(true,false,true,false));
        Assert.False(PileRules.NeedsInitialization(true,false,false,false));
        Assert.False(PileRules.NeedsInitialization(false,false,true,false));
        Assert.False(PileRules.NeedsInitialization(true,false,true,true));
    }
    [Fact] public void RefillingCannotChangeResourceOrExceedOriginalPileSize()
    {
        Assert.True(PileRules.Accepts("Wood","Wood",50,47,3));
        Assert.False(PileRules.Accepts("Wood","Wood",50,47,4));
        Assert.False(PileRules.Accepts("Wood","Stone",50,47,3));
        Assert.False(PileRules.Accepts("Wood","Wood",50,47,0));
    }
}
