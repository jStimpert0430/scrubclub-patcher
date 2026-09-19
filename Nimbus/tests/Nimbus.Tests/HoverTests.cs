using Nimbus.Core;
using Xunit;
namespace Nimbus.Tests;
public class HoverTests
{
    [Theory][InlineData(0,0,0,0)][InlineData(1,0,5,0)][InlineData(0,1,0,5)][InlineData(0,-1,0,-5)][InlineData(-1,0,-5,0)][InlineData(.5f,0,2.5f,0)]
    public void DirectMovementRespectsDirectionAndAnalogInput(float x,float z,float expectedX,float expectedZ)
    {var result=HoverRules.MoveVelocity(x,z,true);Assert.Equal(expectedX,result.x,3);Assert.Equal(expectedZ,result.z,3);}
    [Fact] public void DiagonalAndOversizedInputCannotExceedSpeedCap()
    {foreach(var input in new[]{(1f,1f),(100f,100f),(float.MaxValue,float.MaxValue)}){var result=HoverRules.MoveVelocity(input.Item1,input.Item2,true);Assert.InRange(System.Math.Sqrt(result.x*result.x+result.z*result.z),4.999,5.001);}}
    [Fact] public void InactiveInputStopsPropulsion()=>Assert.Equal((0f,0f),HoverRules.MoveVelocity(1,1,false));
    [Theory][InlineData(float.NaN,0)][InlineData(0,float.PositiveInfinity)][InlineData(float.NegativeInfinity,1)]
    public void MalformedInputStopsPropulsion(float x,float z)=>Assert.Equal((0f,0f),HoverRules.MoveVelocity(x,z,true));
    [Theory][InlineData(1,1,10,10,true)][InlineData(1.5f,1,10,10,true)][InlineData(1.51f,1,10,10,false)][InlineData(1,2,10,10,false)][InlineData(1,1,0,0,false)][InlineData(1,1,10,11,false)]
    public void StaleOrDifferentRiderInputCannotDrive(float now,float received,long user,long inputUser,bool expected)
        =>Assert.Equal(expected,HoverRules.FreshInput(now,received,user,inputUser));
    [Theory][InlineData(0,.55f,1,true)][InlineData(.8f,0,1,false)][InlineData(-3,0,1,false)][InlineData(0,.55f,.6f,true)]
    public void OnlyNearbyWalkableSurfacesProvideLift(float surface,float body,float normal,bool expected)=>Assert.Equal(expected,HoverRules.CanSupport(surface,body,normal));
    [Fact] public void WaterRemainsSupportAfterDescendingBelowSurface()
    {Assert.True(HoverRules.WaterSupport(30,29));Assert.False(HoverRules.WaterSupport(30,35));Assert.False(HoverRules.WaterSupport(-10000,0));}
    [Fact] public void InvalidSurfacesCannotLift()
    {Assert.False(HoverRules.WaterSupport(float.NaN,0));Assert.False(HoverRules.CanSupport(float.PositiveInfinity,0,1));}
    [Fact] public void SpringSupportsWeightAndDampsMovement()
    {Assert.Equal(9.81f,HoverRules.Lift(0,0),2);Assert.True(HoverRules.Lift(0,1)<9.81f);Assert.Equal(25,HoverRules.Lift(10,-30));Assert.Equal(-15,HoverRules.Lift(-10,30));}
}
