using System.Linq;
using MonkStyle.Core;
using Xunit;
namespace MonkStyle.Tests;
public class WeaponTests
{
    [Theory][InlineData(0,25,0,25)][InlineData(0,60,0,60)][InlineData(0,20,60,80)][InlineData(2,4,6,12)]
    public void PhysicalConversionPreservesTotal(float blunt,float slash,float pierce,float expected)
        =>Assert.Equal(expected,Weapons.Blunt(blunt,slash,pierce));
    [Theory][InlineData("FistBjornClaw",2)][InlineData("FistFenrirClaw",3)][InlineData("FistBjornUndeadClaw",4)]
    public void HarvestingTierIsExplicitlyMapped(string id,int tier)=>Assert.Equal(tier,Weapons.ForClaw(id)!.Tier);
    [Fact] public void BearHarvestingUsesBronzeAxe()=>Assert.Equal("AxeBronze",Weapons.ForClaw("FistBjornClaw")!.Axe);
    [Theory][InlineData("PlayerUnarmed")][InlineData("FistGold")][InlineData("FistGold_FrostFire")][InlineData("MonkStyle_TimberKnuckles")][InlineData("AxeIron")][InlineData("FistBjornClaw(Clone)")]
    public void UnrelatedWeaponsNeverGainChopping(string id)=>Assert.Null(Weapons.ForClaw(id));
    [Fact] public void ExactlyThreeUniqueNewItemsWithDistinctSourceWeapons()
    {
        Assert.Equal(3,Weapons.All.Count);Assert.Equal(3,Weapons.All.Select(w=>w.Id).Distinct().Count());
        Assert.Equal(3,Weapons.All.Select(w=>w.Source).Distinct().Count());
    }
    [Theory][InlineData(1,30)][InlineData(2,33)][InlineData(4,39)]
    public void HarvestDamageFollowsAxeQualityAndHitSkillFactor(int quality,float axeDamage)
        =>Assert.Equal(axeDamage*.65f*.5f,Weapons.Chop(30,3,quality,12.5f,25),3);
    [Fact] public void ComboMultiplierIsAppliedOnce()=>Assert.Equal(39,Weapons.Chop(30,3,1,50,25),3);
    [Theory][InlineData(0,25,25)][InlineData(1,0,25)][InlineData(1,25,0)]
    public void InvalidOrZeroDamageCannotCreateChop(int quality,float hit,float raw)=>Assert.Equal(0,Weapons.Chop(30,3,quality,hit,raw));
}
