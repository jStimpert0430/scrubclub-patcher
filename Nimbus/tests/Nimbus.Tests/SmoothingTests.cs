using Nimbus.Core;
using Xunit;
namespace Nimbus.Tests;
public class SmoothingTests
{
    [Fact] public void ClearanceCannotJumpOnSingleSteepSample()
        =>Assert.Equal(.74f,MotionSmoothing.Step(.7f,3,2,1,.02f),3);
    [Fact] public void ClearanceSettlesMoreSlowlyThanItRises()
    {Assert.Equal(2.98f,MotionSmoothing.Step(3,.7f,2,1,.02f),3);Assert.Equal(.72f,MotionSmoothing.Step(.7f,.72f,2,1,.02f),3);}
    [Fact] public void LiftTransitionsHaveBoundedJerk()
        =>Assert.Equal(11.21f,MotionSmoothing.Step(9.81f,35,70,70,.02f),3);
    [Fact] public void RepeatedStepsReachTargetWithoutOvershoot()
    {float v=.7f;for(int i=0;i<200;i++){v=MotionSmoothing.Step(v,3,2,1,.02f);Assert.InRange(v,.7f,3);}Assert.Equal(3,v);}
    [Fact] public void DifferentFramePartitionsHaveSameRate()
    {float v=0;for(int i=0;i<10;i++)v=MotionSmoothing.Step(v,3,2,1,.01f);Assert.Equal(MotionSmoothing.Step(0,3,2,1,.1f),v,4);}
    [Fact] public void ZeroTimeDoesNotMove()=>Assert.Equal(1,MotionSmoothing.Step(1,3,2,1,0));
}
