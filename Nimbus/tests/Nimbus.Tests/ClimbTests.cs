using System;
using Nimbus.Core;
using Xunit;
namespace Nimbus.Tests;
public class ClimbTests
{
    [Theory][InlineData(50)][InlineData(65)][InlineData(75)][InlineData(84)]
    public void SteepSlopeHasUpwardPowerFromStandstillWithoutSprint(float angle)
    {
        float nx=-(float)Math.Sin(angle*Math.PI/180),ny=(float)Math.Cos(angle*Math.PI/180);
        Assert.True(ClimbRules.CanPower(ny,nx,1));
        var move=ClimbRules.Motion(nx,ny,0,HoverRules.MoveSpeed,0);
        Assert.InRange(move.y,3,4.001f);Assert.True(move.x>0);Assert.Equal(0,move.z);
        Assert.True(ClimbRules.Lift(move.y,0)>9.81f);
        float y=0,vy=0;
        for(int i=0;i<100;i++){vy+=(ClimbRules.Lift(move.y,vy)-9.81f)*.02f;y+=vy*.02f;}
        Assert.True(y>4);Assert.InRange(vy,move.y-.05f,move.y+.05f);
    }
    [Theory][InlineData(0,-1,1)][InlineData(.1f,-1,1)][InlineData(.9f,-1,1)][InlineData(.2f,1,1)][InlineData(.2f,-1,1.41f)][InlineData(.2f,-1,-1)]
    public void WallsGentleGroundMovingAwayAndDistantSurfacesCannotPowerClimb(float y,float into,float distance)
        =>Assert.False(ClimbRules.CanPower(y,into,distance));
    [Fact] public void NoInputCannotProduceClimb()=>Assert.Equal((0f,0f,0f),ClimbRules.Motion(-.96f,.28f,0,0,0));
    [Fact] public void SprintCannotExceedClimbRiseLimit()
    {var v=ClimbRules.Motion(-.96f,.28f,0,7.5f,0);Assert.InRange(v.y,0,4);}
    [Fact] public void HorizontalPushWaitsForLiftThenRecovers()
    {Assert.InRange(ClimbRules.HorizontalFraction(4,0),.1f,.2f);Assert.Equal(1,ClimbRules.HorizontalFraction(4,4));}
}
