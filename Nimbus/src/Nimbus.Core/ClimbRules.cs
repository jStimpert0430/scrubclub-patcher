using System;
namespace Nimbus.Core;
public static class ClimbRules
{
    public static bool CanPower(float normalY,float intoSlope,float distance)
        =>normalY>HoverRules.MinGroundNormal && normalY<.788011f && intoSlope<-.05f && distance>=0 && distance<=1.4f;
    public static (float x,float y,float z) Motion(float nx,float ny,float nz,float x,float z)
    {
        double speed=Math.Sqrt((double)x*x+(double)z*z);
        if(ny<=HoverRules.MinGroundNormal || speed<.0001)return (0,0,0);
        double dot=x*nx+z*nz;
        double px=x-nx*dot,py=-ny*dot,pz=z-nz*dot;
        double length=Math.Sqrt(px*px+py*py+pz*pz);
        if(length<.0001)return (0,0,0);
        double scale=speed/length;
        if(py*scale>4)scale=4/py;
        return ((float)(px*scale),(float)(py*scale),(float)(pz*scale));
    }
    public static float HorizontalFraction(float desiredRise,float actualRise)
        =>desiredRise<=0 ? 1 : Math.Max(.1f,Math.Min(1,(Math.Max(0,actualRise)+.5f)/desiredRise));
    public static float Lift(float desiredRise,float actualRise)
        =>Math.Max(-15,Math.Min(35,(desiredRise-actualRise)*8+9.81f));
}
