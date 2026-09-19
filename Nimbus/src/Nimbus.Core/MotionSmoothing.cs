using System;
namespace Nimbus.Core;
public static class MotionSmoothing
{
    public static float Step(float current,float target,float riseRate,float fallRate,float dt)
    {
        if(dt<=0)return current;
        float delta=target-current;
        float limit=Math.Max(0,delta>=0?riseRate:fallRate)*dt;
        return current+Math.Max(-limit,Math.Min(limit,delta));
    }
}
