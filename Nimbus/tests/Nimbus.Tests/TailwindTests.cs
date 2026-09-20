using Nimbus.Core;
using Xunit;
namespace Nimbus.Tests;
public class TailwindTests
{
    [Theory][InlineData(5,6.25f)][InlineData(7.5f,9.375f)]
    public void BothMovementModesGainExactly25Percent(float speed,float expected)
        =>Assert.Equal(expected,speed*TailwindRules.Multiplier(true,true,0,speed,0,1,.5f));
    [Theory][InlineData(0,-1)][InlineData(1,0)][InlineData(-1,0)]
    public void HeadwindAndCrosswindNeverSlowTheCloud(float wx,float wz)
        =>Assert.Equal(1,TailwindRules.Multiplier(true,true,0,5,wx,wz,1));
    [Fact] public void UsesTravelDirectionInsteadOfHullFacing()
        =>Assert.Equal(1.25f,TailwindRules.Multiplier(true,true,-5,0,-1,0,1));
    [Fact] public void LandIdleAndCalmNeverGetBonus()
    {
        Assert.Equal(1,TailwindRules.Multiplier(false,true,0,5,0,1,1));
        Assert.Equal(1,TailwindRules.Multiplier(true,false,0,5,0,1,1));
        Assert.Equal(1,TailwindRules.Multiplier(true,true,0,0,0,1,1));
        Assert.Equal(1,TailwindRules.Multiplier(true,true,0,5,0,1,0));
    }
    [Fact] public void InvalidAndMissingWindFallBackToNormalSpeed()
    {
        Assert.Equal(1,TailwindRules.Multiplier(true,true,0,5,0,0,1));
        Assert.Equal(1,TailwindRules.Multiplier(true,true,0,5,float.NaN,1,1));
        Assert.Equal(1,TailwindRules.Multiplier(true,true,0,float.PositiveInfinity,0,1,1));
    }
}
