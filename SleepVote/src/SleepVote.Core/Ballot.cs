using System;
using System.Collections.Generic;
using System.Linq;

namespace SleepVote.Core;

public enum SleepPhase { Idle, WaitingForVotes, WaitingForCombat, GetToBed, Overridden, Finished, Sleeping, WaitingForPlayers }

public sealed class Ballot
{
    readonly HashSet<long> yes = new HashSet<long>();
    Dictionary<long, bool> people = new Dictionary<long, bool>();
    HashSet<long> combat = new HashSet<long>();
    HashSet<long> unavailable = new HashSet<long>();
    bool armed = true, previousPause, clockStarted, sleeping;
    bool completedSleepLock, observedDaytime;
    double previousTime, time;
    public string Outcome { get; private set; } = "";
    public long DeclinedBy { get; private set; }
    public bool HasCombat => combat.Any(people.ContainsKey);
    public bool HasUnavailable => unavailable.Any(people.ContainsKey);
    bool Paused => HasCombat || HasUnavailable;
    public SleepPhase Phase => sleeping ? SleepPhase.Sleeping : !Active ?
        (Outcome == "Morning arrived" || Outcome == "Sleep completed" ? SleepPhase.Finished :
        Outcome.Length > 0 ? SleepPhase.Overridden : SleepPhase.Idle) :
        HasCombat ? SleepPhase.WaitingForCombat : HasUnavailable ? SleepPhase.WaitingForPlayers :
        ReadyAt > 0 ? SleepPhase.GetToBed : SleepPhase.WaitingForVotes;
    public double Remaining => !Active ? 0 : Math.Max(0, (ReadyAt > 0 ? ReadyAt : Deadline) - time);
    public bool HasAgreed(long id) => yes.Contains(id);
    public bool IsInCombat(long id) => combat.Contains(id);
    public bool IsUnavailable(long id) => unavailable.Contains(id);
    public int Id { get; private set; }
    public bool Active { get; private set; }
    public double Deadline { get; private set; }
    public double ReadyAt { get; private set; }
    bool Unanimous => people.Count > 0 && people.Values.Any(b => b) && people.All(p => p.Value || yes.Contains(p.Key));
    public bool Approved => Active && !Paused && ReadyAt > 0 && time >= ReadyAt && Unanimous;
    public bool NeedsVote(long id) => Active && people.TryGetValue(id, out var bed) && !bed &&
        !yes.Contains(id) && !combat.Contains(id) && !unavailable.Contains(id);

    public void SleepStarted()
    {
        if (!sleeping && !Active) Id++;
        sleeping = true;
        completedSleepLock = true;
        observedDaytime = false;
        Active = false;
        armed = false;
        ReadyAt = 0;
        Outcome = "";
        DeclinedBy = 0;
        yes.Clear();
    }

    public void Tick(Dictionary<long, bool> current, bool night, double now)
        => Tick(current, new HashSet<long>(), night, now);

    public void Tick(Dictionary<long, bool> current, HashSet<long> fighting, bool night, double now,
        bool sleepInProgress = false, HashSet<long>? missing = null)
    {
        double elapsed = clockStarted ? Math.Max(0, now - previousTime) : 0;
        previousTime = now; clockStarted = true; time = now;
        // A cancelled ballot may be retried by a fresh bed entry, even if
        // another sleeper stayed in bed. Unknown/missing data is not an entry.
        bool freshBedEntry = current.Any(p => p.Value && people.TryGetValue(p.Key, out var wasInBed) &&
            !wasInBed && !unavailable.Contains(p.Key) && !(missing?.Contains(p.Key) ?? false));
        people = new Dictionary<long, bool>(current);
        combat = new HashSet<long>(fighting);
        unavailable = missing == null ? new HashSet<long>() : new HashSet<long>(missing);
        if (Active && (Paused || previousPause))
        {
            Deadline += elapsed;
            if (ReadyAt > 0) ReadyAt += elapsed;
        }
        previousPause = Paused;
        yes.RemoveWhere(id => !people.ContainsKey(id));
        if (sleepInProgress)
        {
            if (!sleeping) SleepStarted();
            return;
        }
        if (sleeping)
        {
            sleeping = false;
            Outcome = "Sleep completed";
            // SleepStop and character ZDO updates arrive at different times.
            // Keep the latch closed until we observe everybody out of bed.
        }
        if (completedSleepLock)
        {
            if (!night) observedDaytime = true;
            else if (observedDaytime) completedSleepLock = false;
        }
        bool retryable = Outcome == "Vote declined" || Outcome == "Vote timed out" || Outcome == "Everyone left their beds";
        if (!Active && !completedSleepLock && retryable && freshBedEntry) armed = true;
        bool sleepers = people.Values.Any(b => b);
        if (!night || !sleepers)
        {
            if (!night && sleepers && !Active && Outcome.Length == 0) { Id++; Outcome = "Morning arrived"; }
            if (Active) Outcome = !night ? "Morning arrived" : "Everyone left their beds";
            Active = false; ReadyAt = 0; yes.Clear();
            if (!sleepers && !HasUnavailable) armed = true;
            else armed = false;
            return;
        }
        if (Active)
        {
            if (Unanimous)
            {
                if (ReadyAt == 0 && !Paused) ReadyAt = now + 15;
            }
            else
            {
                // A new participant or a former bed sleeper now needs consent.
                // Give that electorate a fresh vote window, not an expired grace deadline.
                if (ReadyAt > 0) Deadline = now + 60;
                ReadyAt = 0;
                if (!Paused && now >= Deadline)
                {
                    Active = false; Outcome = "Vote timed out"; yes.Clear(); return;
                }
            }
        }
        if (!Active && armed && !completedSleepLock) Outcome = "";
        if (!Active && armed && !completedSleepLock && people.Values.Any(b => !b))
        {
            Id++; Active = true; armed = false; Outcome = ""; DeclinedBy = 0;
            yes.Clear(); Deadline = now + 60; ReadyAt = 0;
        }
    }

    public bool Respond(long player, int ballot, bool approve, double now)
    {
        if (ballot != Id || !NeedsVote(player) || (!Paused && now >= Deadline)) return false;
        if (approve)
        {
            yes.Add(player);
            if (Unanimous && ReadyAt == 0 && !Paused) ReadyAt = now + 15;
        }
        else
        {
            Active = false; ReadyAt = 0; DeclinedBy = player; Outcome = "Vote declined";
        }
        return true;
    }
}
