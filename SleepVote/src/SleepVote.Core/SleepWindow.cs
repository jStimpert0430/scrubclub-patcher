using System;

namespace SleepVote.Core;

// Use the network clock, not the environment's smoothed visual day fraction.
public static class SleepWindow
{
    public static bool IsOpen(double worldTime, double dayLength, double morning, double noon)
    {
        if (dayLength <= 0 || double.IsNaN(worldTime) || double.IsInfinity(worldTime)) return false;
        double fraction = (worldTime % dayLength + dayLength) % dayLength;
        return fraction < morning || fraction >= noon;
    }
}
