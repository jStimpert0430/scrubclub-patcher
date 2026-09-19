using MonkStyle.Core;
using Xunit;
namespace MonkStyle.Tests;
public class BearAuraTests
{
    [Theory]
    [InlineData(1,2,3,true)][InlineData(0,2,3,false)][InlineData(1,0,3,false)][InlineData(1,2,0,false)]
    [InlineData(2,1,3,false)][InlineData(1,2,4,false)][InlineData(0,0,0,false)]
    public void RequiresAllThreeCorrectEquipmentSlots(int helmet,int chest,int legs,bool expected)
        =>Assert.Equal(expected,BearAuraRules.FullSet(helmet,chest,legs,1,2,3));
    [Theory]
    [InlineData(true,true,false,true,0,true)][InlineData(true,true,false,true,60,true)]
    [InlineData(false,true,false,true,2,false)][InlineData(true,false,false,true,2,false)]
    [InlineData(true,true,true,true,2,false)][InlineData(true,true,false,false,2,false)]
    [InlineData(true,true,false,true,61,false)][InlineData(true,true,false,true,-1,false)]
    public void CosmeticVisibilityRespectsEquipmentDeathPreferenceAndDistance(bool set,bool enabled,bool dead,bool graphics,float distance,bool expected)
        =>Assert.Equal(expected,BearAuraRules.Visible(set,enabled,dead,graphics,distance));
}
