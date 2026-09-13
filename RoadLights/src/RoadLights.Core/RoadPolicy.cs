using System;
namespace RoadLights.Core;
public static class RoadPolicy
{
    public const float Radius=12.192f;
    public const float Offset=3f;
    public static bool ShouldPlace(string path,bool local,bool known,bool enabled,bool nearby)
        =>path=="path_v2" && local && known && enabled && !nearby;
    public static bool WithinRadius(float dx,float dy,float dz)
        =>dx*dx+dy*dy+dz*dz<=Radius*Radius;
    public static (float x,float z) Side(float dx,float dz,bool left)
    {
        float length=(float)Math.Sqrt(dx*dx+dz*dz);
        if(length<0.001f){dx=0;dz=1;length=1;}
        float sign=left?-1:1;
        return (dz/length*Offset*sign,-dx/length*Offset*sign);
    }
    public static bool Freestanding(string name)=>LightingPolicy.Includes(name)
        && name!="piece_walltorch" && name!="piece_brazierceiling01" && name!="piece_dvergr_lantern";
}
