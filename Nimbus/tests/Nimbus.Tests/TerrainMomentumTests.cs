using Nimbus.Core;
using Xunit;
namespace Nimbus.Tests;
public class TerrainMomentumTests
{
    [Fact] public void TerrainBumpRestoresLostMomentum()
        =>Assert.Equal(4,TerrainMomentum.Restore(5,1,4,true,false,true));
    [Fact] public void CannotAddSpeedAbovePreviousMomentum()
        =>Assert.Equal(1,TerrainMomentum.Restore(5,4,9,true,false,true));
    [Fact] public void DoesNotUndoLossUnrelatedToCollision()
        =>Assert.Equal(2,TerrainMomentum.Restore(5,1,2,true,false,true));
    [Theory]
    [InlineData(false,false,true)]
    [InlineData(true,true,true)]
    [InlineData(true,false,false)]
    public void WallsAndReleasedControlsKeepNormalPhysics(bool terrain,bool blocked,bool driving)
        =>Assert.Equal(0,TerrainMomentum.Restore(5,0,5,terrain,blocked,driving));
    [Fact] public void NoBoostWhenStationaryOrAlreadyAccelerating()
    {Assert.Equal(0,TerrainMomentum.Restore(0,0,5,true,false,true));Assert.Equal(0,TerrainMomentum.Restore(5,6,5,true,false,true));}
    [Fact] public void HelpfulImpulseIsNotReversed()
        =>Assert.Equal(0,TerrainMomentum.Restore(5,4,-1,true,false,true));
}
