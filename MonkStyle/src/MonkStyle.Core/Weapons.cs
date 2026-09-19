using System;
using System.Collections.Generic;
namespace MonkStyle.Core;
public sealed class Weapon
{
    public string Id {get;} public string Source {get;} public string Name {get;} public string Wood {get;}
    public string Binding {get;} public int Tier {get;} public string Axe {get;}
    public Weapon(string id,string source,string name,string wood,string binding,int tier,string axe)
    {Id=id;Source=source;Name=name;Wood=wood;Binding=binding;Tier=tier;Axe=axe;}
}
public static class Weapons
{
    public static readonly IReadOnlyList<Weapon> All=Array.AsReadOnly(new[]{
        new Weapon("MonkStyle_TimberKnuckles","FistBjornClaw","Timber Knuckles","Wood","LeatherScraps",2,"AxeBronze"),
        new Weapon("MonkStyle_SilverwoodKnuckles","FistFenrirClaw","Silverwood Knuckles","FineWood","Silver",3,"AxeIron"),
        new Weapon("MonkStyle_IronwoodKnuckles","FistBjornUndeadClaw","Ironwood Knuckles","FineWood","BlackMetal",4,"AxeBlackMetal")});
    public static Weapon? ForClaw(string id)
    {foreach(var w in All)if(w.Source==id)return w;return null;}
    public static float Blunt(float blunt,float slash,float pierce)=>blunt+slash+pierce;
    public static float Chop(float axeChop,float axeGrowth,int quality,float hitPhysical,float weaponPhysical)
    {
        if(quality<1 || weaponPhysical<=0 || hitPhysical<=0)return 0;
        // Fist attacks are faster and have a combo finisher. Retain their skill/
        // combo multiplier but start at 65% of equivalent axe chop per hit.
        return Math.Max(0,axeChop+axeGrowth*(quality-1))*.65f*hitPhysical/weaponPhysical;
    }
}
