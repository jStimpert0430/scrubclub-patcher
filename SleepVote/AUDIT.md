# Sleep Vote 0.2.0 audit / 0.2.1 local fixes

Scope: all plugin, ballot and HUD code, RPC authentication and lifecycle, popup ownership, session cleanup, existing tests, and decompiled Valheim 1.0.12 game methods. The released 0.2.0 source is preserved in the public repository. This audit did not access live server logs or run a real multiplayer session. Findings below are reproducible code paths, not a claim that every reported incident has the same cause.

## Findings

| Severity | Finding in 0.2.0 | Local correction |
| --- | --- | --- |
| High | Clicking Yes/No clears `pending`, but a status snapshot already in flight can set it again before the server processes the answer. | A client submission latch, explicit authenticated answer receipt, and monotonically numbered snapshots prevent old status from reopening answered votes. Rejected combat-race answers can be offered again after combat. |
| High | `RefreshBallot` passes `night=false` during time skipping. `Ballot.Tick` treats this as completion and rearms immediately, even while sleepers remain attached. | Explicit Sleeping phase lasts through both the time skip and native `Game.m_sleeping`; successful sleep cannot rearm until the actual clock passes through daytime into the next sleep window. Native sleep RPCs close pending dialogs immediately. |
| High | `IsNight()` uses the smoothed visual day fraction; it can still be true after the actual morning target. `SkipToMorning()` then chooses the following morning. | The network clock and the game's morning boundary gate server approval (including native all-in-bed) and new local bed entry. Votes expire at morning. Waiting bed occupants are released without sleeping/rested effects. |
| Medium | The HUD says “Night ended / sleep started” as soon as the skip starts; it looks complete while the world is still advancing. | Separate “Advancing to morning” and “Sleep completed” states. Native time progression remains intact. |
| Medium | Missing character ZDOs look like players leaving bed, potentially cancelling/rearming a vote. Missing or delayed combat reports are described as actual combat. | Retain the last known bed state while character data is unavailable; pause as Waiting for player connection / character data, with no invented combat notification. |
| Medium | A new voter during grace can inherit the old 60-second deadline and time out immediately. | When grace loses unanimity, the new electorate gets a fresh 60-second vote window and a fresh 15-second grace after consent. |
| Medium | A vote popup buried under another mod's popup is kept in the stack and can reappear after cancellation/combat. | Remove only the owned popup while preserving other stack entries and their order. |
| Medium | `InAttack()` includes tool swings, incorrectly treating road work, chopping or mining as combat and repeatedly restarting the eight-second quiet period. | Use native enemy awareness/targeting and actual attacker damage. Keep the eight-second quiet period; no panel or modal during combat. |
| Medium | A player entering bed after SleepStart but before SleepStop can be left attached because the awake-player detach guard covers beds too. | Protect awake chair/boat attachments, but allow native SleepStop to release beds. No extra rested grant. |
| Low | Combat notices can repeat when combat restarts within one vote. | At most one initiator notification per ballot for the affected player. |

## Timing and remaining limits

Valheim checks sleep every two seconds. `EnvMan.SkipToMorning` aims for a twelve-second advance, after which native sleep completion is checked again; cinematics can defer completion. Its time-skip update runs once per rendered frame with a fixed-step delta, so low server frame rates can make this longer. The visual environment clock also smooths sunrise. These native intervals are not a second vote countdown and have not been removed or sped up globally.

The mod retains the requested fifteen-second get-to-bed period after unanimous consent, and pauses voting/grace during combat or missing connection data. Someone newly joining needs consent. After cancellation or timeout, one player getting up and lying down again can start a fresh ballot; other sleepers may remain in bed. Successful sleep instead locks out new ballots until the actual clock passes through daytime into the next sleep window. All-in-bed sleep remains native within the valid window and with available, non-combat participant state.

Combat is cooperatively reported by clients, not an anti-cheat guarantee. The server authenticates peer identity, votes and status direction. Invalid, stale or duplicate replies do not create a new ballot. Client/server versions must match because 0.2.1 adds a status sequence and receipt RPC.

No direct time skip, status-effect grant, forced sleep, world-save migration or inventory mutation is added. Native SleepStop still saves and calls OnSleep. The only direct detach is a player waiting in bed at dawn who is not actually sleeping; actual sleepers use native wakeup.

## Validation and follow-up

Automated regressions cover delayed snapshots/receipts, duplicate responses, combat races, sleep-in-progress versus completion, stale wakeup bed state, dawn boundaries, late joins, grace reset, missing player data, and native hook/effect contracts. The tests exercise the actual core used by the plugin; they do not replace a Unity/network playtest.

Before deployment, test two clients with one sleeper and one voter: answer under latency; let a vote cross dawn; enter bed just before/after dawn; interrupt with combat; join during grace; disconnect/reconnect; put another popup over the vote. Check early all-in-bed sleep and rested effects, and verify awake chair/boat occupants stay attached. No current live server or patcher changes have been made for this audit.

Use `sleepvote_status` to capture vote phase/revision, receipt state, network clock, eligibility, visual night and native sleep flags. On clients the native time-skip/server-sleep fields are local copies; use the received phase for server progress. Server logs emit only state transitions with participant/combat/unavailable counts. Mock UI previews add `sleeping`, `finished` and `connection` alongside the existing modes.
