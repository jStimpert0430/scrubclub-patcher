using BlueDepot.Core;
using Xunit;
namespace BlueDepot.Tests;
public class AncientSeedTests
{
    [Theory] [InlineData("Material")] [InlineData("Misc")] [InlineData("Trophy")]
    public void AncientSeedRoutesToTrophiesAndRemainsBulkDepositable(string type)
    {
        var category=Categories.Classify(type,prefabId:"AncientSeed");
        Assert.Equal(Category.Trophies,category);
        Assert.True(BulkDropRules.Eligible(category,false,1,3,false));
        Assert.False(BulkDropRules.Eligible(category,false,0,3,false));
        Assert.False(BulkDropRules.Eligible(category,false,1,3,true));
    }
    [Fact] public void TrophyIngredientsKeepTheirCategoryAndUnrelatedSeedsStayMaterials()
    {
        Assert.Equal(Category.Trophies,Categories.Classify("Trophy",true));
        Assert.Equal(Category.Materials,Categories.Classify("Material",prefabId:"CarrotSeeds"));
        Assert.Equal(Category.Materials,Categories.Classify("Material",prefabId:"AncientSeedOtherMod"));
    }
}
