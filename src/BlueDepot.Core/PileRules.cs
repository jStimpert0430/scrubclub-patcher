using System;
namespace BlueDepot.Core;
public static class PileRules
{
    public static bool IsResourcePile(string prefab,int resourceKinds,bool recoverable,int amount,bool material) =>
        (prefab.EndsWith("_pile",StringComparison.Ordinal) || prefab.EndsWith("_stack",StringComparison.Ordinal)) &&
        resourceKinds==1 && recoverable && amount>1 && material;
    public static bool Accepts(string resource,string incoming,int capacity,int existing,int incomingAmount) =>
        resource==incoming && incomingAmount>0 && existing>=0 && incomingAmount<=capacity-existing;
    public static bool NeedsInitialization(bool placed,bool hasInventory,bool owner,bool freeBuild)=>placed && !hasInventory && owner && !freeBuild;
}
