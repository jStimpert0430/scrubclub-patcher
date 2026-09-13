using System;
using System.Collections.Generic;

namespace RoadLights.Core;

public static class LightingPolicy
{
    // Exact prefab IDs, never all Fireplaces or a broad "has Light" component test.
    static readonly HashSet<string> lights=new HashSet<string>(StringComparer.Ordinal)
    {
        "piece_groundtorch_wood","piece_groundtorch","piece_groundtorch_blue",
        "piece_groundtorch_green","piece_walltorch","piece_groundtorch_mist",
        "piece_dvergr_lantern","piece_dvergr_lantern_pole","piece_hoodedlantern",
        "piece_Lavalantern","Candle_resin","piece_jackoturnip",
        "piece_brazierceiling01","piece_brazierfloor01","piece_brazierfloor02"
    };
    public static IEnumerable<string> Prefabs=>new List<string>(lights).AsReadOnly();
    public static bool Includes(string? prefab)=>prefab!=null && lights.Contains(prefab);
    public static bool CanToggle(bool targeted,bool valid,bool canTurnOff,bool hold,bool alt,bool access)
        =>targeted && valid && canTurnOff && !hold && !alt && access;
}
