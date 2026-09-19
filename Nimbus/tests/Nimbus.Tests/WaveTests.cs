using Nimbus.Core;
using Xunit;
namespace Nimbus.Tests;
public class WaveTests
{
    [Fact] public void FlatWaterStaysLevel()=>Assert.Equal((0f,1f,0f),HoverRules.WaveNormal(10,10,10,10,.9f,1.3f));
    [Fact] public void RaisedRightSideTiltsUpRight()
    {var n=HoverRules.WaveNormal(0,.9f,0,0,.9f,1.3f);Assert.Equal(-.7071f,n.x,3);Assert.Equal(.7071f,n.y,3);Assert.Equal(0,n.z);}
    [Fact] public void RaisedBowTiltsUpForward()
    {var n=HoverRules.WaveNormal(0,0,0,1.3f,.9f,1.3f);Assert.Equal(0,n.x);Assert.Equal(.7071f,n.y,3);Assert.Equal(-.7071f,n.z,3);}
    [Fact] public void HeightOffsetDoesNotChangeTilt()
    {Assert.Equal(HoverRules.WaveNormal(0,1,2,3,1,1),HoverRules.WaveNormal(100,101,102,103,1,1));}
    [Fact] public void InvalidWaveDataFallsBackToLevel()
    {Assert.Equal((0f,1f,0f),HoverRules.WaveNormal(float.NaN,0,0,0,1,1));Assert.Equal((0f,1f,0f),HoverRules.WaveNormal(0,1,0,1,0,1));}
    [Theory][InlineData(10,10.1f,.1f,1)][InlineData(10,9.9f,.1f,-1)][InlineData(0,100,.02f,5)][InlineData(100,0,.02f,-5)][InlineData(float.NegativeInfinity,10,.02f,0)][InlineData(10,11,0,0)]
    public void WaveRiseTracksMotionWithBoundedRecovery(float previous,float current,float dt,float expected)
        =>Assert.Equal(expected,HoverRules.SurfaceVelocity(previous,current,dt),3);
}
