using System;
namespace Nimbus.Core;
public static class HoverRules
{
    public const float MoveSpeed=5f;
    public const float MinGroundNormal=.1f; // Character.OnCollisionStay: normal.y > 0.1
    public static bool FreshInput(float now,float received,long controller,long inputController)
        =>controller!=0 && controller==inputController && now>=received && now-received<=.5f;
    public static (float x,float z) MoveVelocity(float x,float z,bool active)
    {
        if(!active || float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(z) || float.IsInfinity(z))return (0,0);
        double magnitude=Math.Sqrt((double)x*x+(double)z*z);
        double scale=MoveSpeed/Math.Max(1,magnitude);
        return ((float)(x*scale),(float)(z*scale));
    }
    public const float Clearance=.7f;
    public const float SupportReach=1.5f;
    public static bool CanSupport(float height,float positionY,float normalY)
        =>!float.IsNaN(height) && !float.IsInfinity(height) && normalY>MinGroundNormal && height<=positionY+.65f && height>=positionY-(normalY<.7f?3.8f:SupportReach);
    public static float HullClearance(float normalY,float extent,float centerOffset)
        =>Math.Max(Clearance,Math.Min(3f,(extent+.1f-centerOffset)/Math.Max(MinGroundNormal,normalY)));
    public static bool WaterSupport(float height,float positionY)
        =>!float.IsNaN(height) && !float.IsInfinity(height) && height> -1000 && height>=positionY-SupportReach;
    public static (float x,float y,float z) WaveNormal(float left,float right,float back,float front,float width,float length)
    {
        if(width<=0 || length<=0)return (0,1,0);
        double x=-(right-left)/width,z=-(front-back)/length;
        if(double.IsNaN(x)||double.IsInfinity(x)||double.IsNaN(z)||double.IsInfinity(z))return (0,1,0);
        double magnitude=Math.Sqrt(x*x+1+z*z);return ((float)(x/magnitude),(float)(1/magnitude),(float)(z/magnitude));
    }
    public static float SurfaceVelocity(float previous,float current,float dt)
    {
        if(dt<=0 || float.IsNaN(previous)||float.IsInfinity(previous)||float.IsNaN(current)||float.IsInfinity(current))return 0;
        return Math.Max(-5,Math.Min(5,(current-previous)/dt));
    }
    public static bool CanStep(float currentSurface,float nextSurface)
        =>!float.IsNaN(currentSurface) && !float.IsInfinity(currentSurface) && !float.IsNaN(nextSurface) && !float.IsInfinity(nextSurface)
            && nextSurface-currentSurface<=.65f;
    public static float SurfaceRise(float nx,float ny,float nz,float vx,float vz)
        =>ny>MinGroundNormal ? Math.Max(-10,Math.Min(10,-(nx*vx+nz*vz)/ny)) : 0;
    public static float Lift(float error,float verticalSpeed)=>Math.Max(-15,Math.Min(25,30*error-10*verticalSpeed+9.81f));
}
