namespace BlueDepot.Core;

public enum DepositMode { Automatic, Intake, Internal }
public static class DepositRules
{
    // A category filter never changes the incoming item's category or its routing.
    public static DepositMode Mode(bool intake,bool native,bool immediate)=>immediate?DepositMode.Automatic:
        intake?DepositMode.Intake:native?DepositMode.Internal:DepositMode.Automatic;
}
