namespace Nimbus.Core;
public static class RiderDeckRules
{
    public static bool Supports(long reservedUser,long player,bool attached)
        =>reservedUser!=0 && player==reservedUser && !attached;
}
