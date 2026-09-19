namespace Nimbus.Core;
public sealed class SprintGate
{
    bool exhausted;
    public bool Tick(bool held,bool moving,bool supported,bool hasStamina)
    {
        if(!held)exhausted=false;
        else if(!hasStamina)exhausted=true;
        return held && moving && supported && hasStamina && !exhausted;
    }
    public const float Multiplier=1.5f;
}
