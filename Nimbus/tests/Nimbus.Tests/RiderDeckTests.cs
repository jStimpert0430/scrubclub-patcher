using Nimbus.Core;
using Xunit;
namespace Nimbus.Tests;
public class RiderDeckTests
{
    [Fact] public void WalkingOutOfAttachmentRestoresDeckSupport()
    {Assert.False(RiderDeckRules.Supports(7,7,true));Assert.True(RiderDeckRules.Supports(7,7,false));Assert.False(RiderDeckRules.Supports(7,7,true));}
    [Theory][InlineData(0,0)][InlineData(0,7)][InlineData(7,8)]
    public void DeckDoesNotAdmitAnUnreservedPassenger(long reserved,long player)=>Assert.False(RiderDeckRules.Supports(reserved,player,false));
}
