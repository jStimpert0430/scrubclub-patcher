namespace SleepVote.Core;

// A clicked answer stays suppressed while its receipt is in flight. An older
// snapshot saying "needs vote" must never undo a local submission.
public sealed class ClientVote
{
    public int Id { get; private set; }
    public long Revision { get; private set; } = -1;
    public bool NeedsVote { get; private set; }
    public bool Submitted { get; private set; }
    public bool WaitingForReceipt { get; private set; }
    bool sleepTransition, terminal;
    public bool CanPrompt => NeedsVote && !Submitted && !sleepTransition;

    public bool Apply(int id, long revision, bool needsVote, bool active)
    {
        if (id < Id || revision <= Revision || (id == Id && terminal && active)) return false;
        if (id != Id)
        {
            Id = id;
            terminal = false;
            Submitted = WaitingForReceipt = false;
        }
        Revision = revision;
        NeedsVote = active && needsVote;
        if (!active) { sleepTransition = false; terminal = true; }
        return true;
    }

    public bool Submit(int id)
    {
        if (id != Id || !CanPrompt) return false;
        Submitted = WaitingForReceipt = true;
        return true;
    }

    public void Receipt(int id, bool accepted)
    {
        if (id != Id || !WaitingForReceipt) return;
        WaitingForReceipt = false;
        if (!accepted) Submitted = false;
    }

    public void SleepTransition()
    {
        sleepTransition = true;
        NeedsVote = false;
    }
}
