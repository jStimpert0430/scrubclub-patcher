using System;
using System.Collections.Generic;
namespace Torchlight.Core;
public static class Visibility
{
    static readonly HashSet<string> Lights=new HashSet<string>(StringComparer.Ordinal)
    {
        "piece_groundtorch_wood","piece_groundtorch","piece_groundtorch_blue",
        "piece_groundtorch_green","piece_groundtorch_mist","piece_walltorch","fire_pit"
    };
    public static bool Includes(string? name)
    {
        if(name==null)return false;
        const string suffix="(Clone)";
        if(name.EndsWith(suffix,StringComparison.Ordinal))name=name.Substring(0,name.Length-suffix.Length);
        return Lights.Contains(name);
    }
    public static float Distance(string? prefab,bool lodEnabled,float original,float requested)
    {
        if(!Includes(prefab)||!lodEnabled||float.IsNaN(original)||float.IsInfinity(original)||original<=0)return original;
        if(float.IsNaN(requested)||float.IsInfinity(requested))return original;
        return Math.Max(original,Math.Max(80,Math.Min(160,requested)));
    }
}
