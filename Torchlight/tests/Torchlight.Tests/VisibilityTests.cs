using Torchlight.Core;
using Xunit;
namespace Torchlight.Tests;
public class VisibilityTests
{
    [Theory]
    [InlineData("piece_groundtorch_wood")][InlineData("piece_groundtorch")]
    [InlineData("piece_groundtorch_blue")][InlineData("piece_groundtorch_green")]
    [InlineData("piece_groundtorch_mist")][InlineData("piece_walltorch")]
    public void TorchesUseRequestedVisibility(string name)
        =>Assert.Equal(125,Visibility.Distance(name+"(Clone)",true,80,125));
    [Theory]
    [InlineData("hearth")][InlineData("Torch")]
    [InlineData("piece_brazierfloor01")][InlineData("fx_torch_equip")][InlineData(null)]
    public void UnrelatedLightsUnchanged(string? name)
        =>Assert.Equal(40,Visibility.Distance(name,true,40,100));
    [Theory][InlineData(100)][InlineData(50)]
    public void BothCampfireBurnStatesUse125Metres(float original)
        =>Assert.Equal(125,Visibility.Distance("fire_pit(Clone)",true,original,125));
    [Fact] public void DisabledLodRemainsUntouched()=>Assert.Equal(80,Visibility.Distance("piece_groundtorch_wood",false,80,100));
    [Fact] public void NeverShrinksAnotherModsLongerDistance()=>Assert.Equal(200,Visibility.Distance("piece_groundtorch",true,200,100));
    [Fact] public void ReapplyingNeverCompoundsDistance()
    {float d=80;for(int i=0;i<50;i++)d=Visibility.Distance("piece_groundtorch",true,d,125);Assert.Equal(125,d);}
    [Theory][InlineData(10000,160)][InlineData(-1,80)]
    public void ConfigurationIsBounded(float requested,float expected)=>Assert.Equal(expected,Visibility.Distance("piece_groundtorch",true,80,requested));
    [Fact] public void InvalidValuesDoNotPoisonLightState()
    {
        Assert.Equal(80,Visibility.Distance("piece_groundtorch",true,80,float.NaN));
        Assert.Equal(80,Visibility.Distance("piece_groundtorch",true,80,float.PositiveInfinity));
        Assert.Equal(0,Visibility.Distance("piece_groundtorch",true,0,100));
    }
}
