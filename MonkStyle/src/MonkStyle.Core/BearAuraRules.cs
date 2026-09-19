namespace MonkStyle.Core;
public static class BearAuraRules
{
    public const string Helmet="HelmetBerserkerHood";
    public const string Chest="ArmorBerserkerChest";
    public const string Legs="ArmorBerserkerLegs";
    public static bool FullSet(int helmet,int chest,int legs,int requiredHelmet,int requiredChest,int requiredLegs)
        =>helmet==requiredHelmet && chest==requiredChest && legs==requiredLegs;
    public static bool Visible(bool fullSet,bool enabled,bool dead,bool graphics,float distance)
        =>fullSet && enabled && !dead && graphics && distance>=0 && distance<=60;
}
