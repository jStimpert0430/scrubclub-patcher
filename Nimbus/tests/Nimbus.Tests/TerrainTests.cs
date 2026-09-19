using Nimbus.Core;
using Xunit;
namespace Nimbus.Tests;
public class TerrainTests
{
    [Theory][InlineData("beech_small1")][InlineData("beech_small2")][InlineData("FirTree_small")][InlineData("Bush01")][InlineData("Heath")]
    public void SmallVegetationCanBePassed(string prefab)=>Assert.True(VegetationRules.PassThrough(prefab,false));
    [Theory][InlineData("Beech1")][InlineData("FirTree")][InlineData("Oak1")][InlineData("rock2")][InlineData("beech_log")][InlineData("wood_wall")][InlineData("Greydwarf")]
    public void SolidObstaclesRemainSolid(string prefab)=>Assert.False(VegetationRules.PassThrough(prefab,false));
    [Theory][InlineData(.65f,true)][InlineData(.64f,true)][InlineData(.1001f,true)][InlineData(.1f,false)][InlineData(0,false)]
    public void GroundSlopeLimitMatchesPlayerGroundContact(float normal,bool expected)=>Assert.Equal(expected,HoverRules.CanSupport(0,.7f,normal));
    [Fact] public void LooseCollectibleStonesCanBePassedButMineableRocksStaySolid()
    {Assert.True(VegetationRules.PassThrough("Pickable_Stone",false,true));Assert.False(VegetationRules.PassThrough("rock2",false,false));}
    [Fact] public void GrowingSaplingCanBePassed()=>Assert.True(VegetationRules.PassThrough("sapling",true));
    [Theory][InlineData(1,1.6f,true)][InlineData(1,1.7f,false)][InlineData(1,0,true)][InlineData(float.NegativeInfinity,1,false)][InlineData(1,float.NaN,false)]
    public void LookAheadNeedsCurrentSupportAndSmallStep(float current,float next,bool expected)=>Assert.Equal(expected,HoverRules.CanStep(current,next));
    [Theory][InlineData(0,1,0,5,0,0)][InlineData(-.6f,.8f,0,4,0,3)][InlineData(-.6f,.8f,0,-4,0,-3)][InlineData(-.6f,.8f,0,0,4,0)][InlineData(-.6f,.8f,0,20,0,10)][InlineData(-1,0,0,5,0,0)]
    public void SlopeLiftTracksTravelWithoutWallClimbing(float nx,float ny,float nz,float vx,float vz,float expected)
        =>Assert.Equal(expected,HoverRules.SurfaceRise(nx,ny,nz,vx,vz),3);
}
