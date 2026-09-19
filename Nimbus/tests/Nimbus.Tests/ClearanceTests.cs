using Nimbus.Core;
using Xunit;
namespace Nimbus.Tests;
public class ClearanceTests
{
    [Fact] public void FlatGroundKeepsNormalHoverGap()=>Assert.Equal(.7f,HoverRules.HullClearance(1,.12f,.02f),3);
    [Fact] public void SteepGroundAccountsForHullThicknessNormalToSlope()
    {float clearance=HoverRules.HullClearance(.11f,.12f,.02f);Assert.InRange(clearance,1.8f,1.83f);Assert.True(clearance*.11f+.02f>=.22f-.0001f);}
    [Fact] public void TiltTransitionIncreasesClearanceWithoutUnboundedLift()
    {Assert.Equal(3,HoverRules.HullClearance(.11f,.9f,0));Assert.True(HoverRules.HullClearance(.5f,.6f,0)>HoverRules.HullClearance(.5f,.12f,0));}
    [Fact] public void SteepExtendedClearanceDoesNotLoseSupport()
    {Assert.True(HoverRules.CanSupport(0,3.2f,.11f));Assert.False(HoverRules.CanSupport(0,3.2f,1));Assert.False(HoverRules.CanSupport(0,4,.11f));}
}
