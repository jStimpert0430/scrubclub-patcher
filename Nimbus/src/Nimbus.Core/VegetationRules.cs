using System;
namespace Nimbus.Core;
public static class VegetationRules
{
    public static bool PassThrough(string prefab,bool isPlant,bool isLooseStone=false)
    {
        if(isPlant || isLooseStone)return true;
        switch(prefab)
        {
            case "beech_small1":case "beech_small2":case "FirTree_small":case "FirTree_small_dead":
            case "Bush01":case "Bush02":case "Heath":return true;
            default:return false;
        }
    }
}
