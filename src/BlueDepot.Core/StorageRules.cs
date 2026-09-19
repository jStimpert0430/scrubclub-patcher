using System;
using System.Collections.Generic;
namespace BlueDepot.Core;
public static class StorageRules
{
    static readonly HashSet<string> Scrap = new HashSet<string>(StringComparer.Ordinal)
        { "IronScrap", "CopperScrap", "BlackMetalScrap" };
    public static bool IsOre(string id) => id.EndsWith("Ore", StringComparison.Ordinal) || Scrap.Contains(id);
}
