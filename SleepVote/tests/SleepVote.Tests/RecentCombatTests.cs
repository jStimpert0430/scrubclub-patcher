using SleepVote.Core;
using Xunit;
namespace SleepVote.Tests;
public class RecentCombatTests
{
    [Fact] public void NoAttacksMeansNoCombatEvenDuringRepeatedChecks()
    {var c=new RecentCombat();for(int i=0;i<100;i++)Assert.False(c.Active(i));}
    [Fact] public void WindowExpiresAtExactlyTwentySeconds()
    {var c=new RecentCombat();c.Record(100);Assert.True(c.Active(100));Assert.True(c.Active(119.999));Assert.False(c.Active(120));Assert.False(c.Active(150));}
    [Fact] public void SubsequentAttackRestartsTheWindow()
    {var c=new RecentCombat();c.Record(100);c.Record(115);Assert.True(c.Active(134.99));Assert.False(c.Active(135));}
    [Fact] public void PollingDoesNotRefreshTheTimer()
    {var c=new RecentCombat();c.Record(0);for(int i=0;i<20;i++)Assert.True(c.Active(i));Assert.False(c.Active(20));}
    [Fact] public void DisconnectResetClearsOldCombat()
    {var c=new RecentCombat();c.Record(100);c.Reset();Assert.False(c.Active(101));Assert.False(c.Active(0));}
    [Fact] public void ClockRewindDoesNotCreateAnExtendedLock()
    {var c=new RecentCombat();c.Record(100);Assert.False(c.Active(50));}
}
