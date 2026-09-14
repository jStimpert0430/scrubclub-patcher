# Sleep Vote 0.2.1

Version 0.2.1 is included in modpack 0.2.7. See [the audit](AUDIT.md) for findings, fixes and validation limits.

Separate mod requiring BepInEx and Jotunn on the server and every client.
Version 0.2.0 is included in modpack 0.2.6. Local mock previews and automated tests
cover the new UI/state flow; real multiplayer combat behavior still needs playtesting.

When a player successfully enters a bed at night/afternoon, awake players receive
Valheim's native Yes/No prompt. Bed eligibility (roof, fire, enemies, wetness,
ownership and time) stays vanilla. Everyone awake must agree. No or no answer
within 60 seconds cancels; a player can get up and lie down again to start a fresh ballot.
Players already in bed count as agreeing only while they stay there.

After everyone agrees, a 15-second grace period lets nearby players get into bed.
If everyone is already in bed, normal sleep can proceed immediately. Only bed
sleepers receive native sleep/rested effects; awake voters keep their position and
attachments. Native server time skipping, sleep saving and OnSleep callbacks are
retained. Nothing directly grants status effects or changes bed ownership.

The server owns the ballot, derives identities from connected peers and reads bed
state from character ZDOs. New players must agree; disconnects are removed. Stale,
duplicate, unknown and expired replies are ignored. State is session-only.

## Local validation

Run `scripts/build-test.sh`. Tests exercise consent, the full grace period,
late approvals, expiry, retries, joining/leaving, and installed-game Harmony
contracts. Two real clients are still required to verify remote voting, wakeup
and rested behavior.

After installing the staged two DLLs in a closed local client, `sleepvote_preview`
in F5 displays the native dialog using a mock request in a solo local world.
It never changes world time or forces sleep. Test Yes, No and the timeout. Bed
sleep with one real player remains vanilla, so use two clients for actual voting.

## Combat and live status (0.2.0)

A wood-panel readout shows non-combat players the current stage, seconds remaining, agreement
count, and each player's In bed / Yes / Waiting / In combat / No state. It remains
visible to the initiating player while in bed. Declines, timeouts and everyone
leaving bed show Overridden and a reason for eight seconds; night completion has
a separate finished state.

The 60-second vote clock does not run while anyone is in combat. Combat resuming
pauses the remaining vote or get-to-bed time instead of resetting it. The 15-second
get-to-bed clock begins after unanimous consent, and also pauses for combat.
An unaffected player may vote while another player fights.

Affected players receive a top-left message naming the initiator: "PLAYER has initiated a sleep vote." Both their status panel and modal dialog
are suppressed until combat ends. Local combat detection hides them immediately,
without waiting for the next server update. Sleeping players see "Waiting for votes, player in
combat" and the live participant list. Combat uses the game's enemy-awareness or
targeting or actual attacker damage followed by eight quiet seconds. Tool animations alone do not count as combat. Clients report their own state over
authenticated peer RPCs; missing/stale reports pause the ballot conservatively.
This is cooperative client reporting, not an anti-cheat proof of combat state.

All clients and the server need this version together (0.2.1 adds sequenced state and answer receipts).


Local UI previews (solo world, no real time skip):
- `sleepvote_preview votes`
- `sleepvote_preview combat`
- `sleepvote_preview bed`
- `sleepvote_preview overridden`

Multiplayer validation: test a real two-player vote: one fights while the other is in bed,
check that the full vote duration remains, finish combat and approve, then verify
the 15-second grace, early all-in-bed sleep, and restored normal rested benefits.
Also test combat restarting during grace, No, timeout, disconnect and reconnect.

## Both local perspectives

` sleepvote_preview sleeper votes ` shows the initiating player's live readout
with mock players, without attaching your real character to a bed.
` sleepvote_preview voter votes ` shows the awake player's Yes/No dialog and
readout. Answering Yes previews the 15-second get-to-bed countdown; No previews
the overridden state. Close F5 after issuing a command to see the UI.

Replace `votes` with `combat`, `bed`, or `overridden` to inspect either role in
those states. `sleepvote_preview stop` closes the preview. Solo-local-world only;
no network votes, time skipping, bed attachment or rested effects are generated.
