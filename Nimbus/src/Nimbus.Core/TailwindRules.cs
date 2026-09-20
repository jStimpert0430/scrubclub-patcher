using System;
namespace Nimbus.Core;
public static class TailwindRules
{
    public static float Multiplier(bool water,bool active,float x,float z,float windX,float windZ,float strength)
    {
        if(!water || !active || !Finite(strength) || strength<=0 || !Finite(x) || !Finite(z) || !Finite(windX) || !Finite(windZ))return 1;
        double travel=Math.Sqrt((double)x*x+(double)z*z),wind=Math.Sqrt((double)windX*windX+(double)windZ*windZ);
        if(travel<.001 || wind<.001)return 1;
        double alignment=((double)x*windX+(double)z*windZ)/(travel*wind);
        return alignment>0.001 ? 1.25f : 1;
    }
    static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
}
