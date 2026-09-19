using Nimbus.Core;
using Xunit;
namespace Nimbus.Tests;
public class MountTests
{
    [Fact] public void NearbyAuthenticatedPlayerCanMountWithoutBoatTriggerMembership()
        =>Assert.True(MountRules.CanGrant(7,11,11,2,true,false,0,false));
    [Theory][InlineData(3,true)][InlineData(3.01f,false)][InlineData(-1,false)][InlineData(float.NaN,false)][InlineData(float.PositiveInfinity,false)]
    public void BoardingDistanceIsBounded(float distance,bool expected)
        =>Assert.Equal(expected,MountRules.CanGrant(7,11,11,distance,true,false,0,false));
    [Fact] public void DifferentPeerCannotClaimAnotherPlayersSeat()
        =>Assert.False(MountRules.CanGrant(7,12,11,2,true,false,0,false));
    [Fact] public void OccupiedSeatRejectsAnotherRiderButAllowsCurrentRider()
    {Assert.False(MountRules.CanGrant(7,11,11,2,true,false,8,true));Assert.True(MountRules.CanGrant(7,11,11,2,true,false,7,true));}
    [Fact] public void DepartedRiderDoesNotLockTheSeat()
        =>Assert.True(MountRules.CanGrant(7,11,11,2,true,false,8,false));
    [Theory][InlineData(false,false)][InlineData(true,true)]
    public void DeadOrEncumberedPlayerCannotMount(bool alive,bool encumbered)
        =>Assert.False(MountRules.CanGrant(7,11,11,2,alive,encumbered,0,false));
    [Fact] public void MissingPlayerIsRejected()=>Assert.False(MountRules.CanGrant(0,11,11,2,true,false,0,false));
}
