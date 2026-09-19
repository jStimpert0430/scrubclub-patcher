namespace Nimbus.Core;
public static class MountRules
{
    public static bool CanGrant(long player,long sender,long playerOwner,float distance,bool alive,bool encumbered,long currentUser,bool currentValid)
        =>player!=0 && sender==playerOwner && alive && !encumbered && distance>=0 && distance<=3f
            && (!currentValid || currentUser==player);
}
