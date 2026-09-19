using Nimbus.Core;
using Xunit;
namespace Nimbus.Tests;
public class SprintTests
{
    [Fact] public void SprintIsOneAndAHalfNormalSpeed()=>Assert.Equal(7.5f,HoverRules.MoveSpeed*SprintGate.Multiplier);
    [Fact] public void MovingSupportedRiderWithStaminaCanSprint()=>Assert.True(new SprintGate().Tick(true,true,true,true));
    [Theory][InlineData(false,true,true,true)][InlineData(true,false,true,true)][InlineData(true,true,false,true)][InlineData(true,true,true,false)]
    public void ReleaseIdleAirborneOrEmptyStaminaStopsSprint(bool held,bool moving,bool supported,bool stamina)
        =>Assert.False(new SprintGate().Tick(held,moving,supported,stamina));
    [Fact] public void ExhaustionRequiresReleaseBeforeSprintCanResume()
    {var s=new SprintGate();Assert.True(s.Tick(true,true,true,true));Assert.False(s.Tick(true,true,true,false));Assert.False(s.Tick(true,true,true,true));s.Tick(false,true,true,true);Assert.True(s.Tick(true,true,true,true));}
    [Fact] public void StoppingMovementDoesNotLatchExhaustion()
    {var s=new SprintGate();Assert.False(s.Tick(true,false,true,true));Assert.True(s.Tick(true,true,true,true));}
}
