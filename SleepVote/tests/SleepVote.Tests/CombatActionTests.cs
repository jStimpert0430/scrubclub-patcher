using SleepVote.Core;
using Xunit;
namespace SleepVote.Tests;
public class CombatActionTests
{
    [Theory]
    [InlineData("Tool","None",0,false)]
    [InlineData("OneHandedWeapon","Pickaxes",0,false)]
    [InlineData("OneHandedWeapon","Axes",40,false)]
    [InlineData("TwoHandedWeapon","Axes",50,false)]
    [InlineData("OneHandedWeapon","WoodCutting",10,false)]
    [InlineData("OneHandedWeapon","Swords",0,true)]
    [InlineData("TwoHandedWeapon","Unarmed",0,true)]
    [InlineData("TwoHandedWeapon","Unarmed",30,true)]
    [InlineData("Bow","Bows",0,true)]
    public void UtilitySwingsDoNotStartCombat(string type,string skill,float chop,bool expected)
        =>Assert.Equal(expected,CombatActions.CountsSwing(type,skill,chop));
}
