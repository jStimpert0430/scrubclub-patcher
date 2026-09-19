using System;
namespace Nimbus.Core;
public static class TerrainMomentum
{
    // Restore only measured collision loss, never ordinary braking or lift.
    public static float Restore(float before,float current,float lostImpulseSpeed,bool terrain,bool blocked,bool driving)
        =>!terrain || blocked || !driving || before<=0 ? 0 :
            Math.Max(0,Math.Min(Math.Max(0,lostImpulseSpeed),before-current));
}
