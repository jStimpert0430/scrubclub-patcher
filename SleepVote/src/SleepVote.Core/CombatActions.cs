namespace SleepVote.Core;
public static class CombatActions
{
    // Axes are dual-purpose: harvesting must not block sleep. They enter
    // combat when an attack connects with a character instead of on swing.
    public static bool CountsSwing(string itemType,string skill,float chopDamage)
        =>itemType!="Tool" && skill!="Pickaxes" && skill!="WoodCutting" && !(skill=="Axes" && chopDamage>0);
}
