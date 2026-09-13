# Sleep Vote 0.1.0

Separate staged mod requiring BepInEx and Jotunn on the server and every client.
Client startup and the prompt preview were tested locally. Included in modpack 0.2.5.
Real multiplayer voting still needs a live gameplay check.

When a player successfully enters a bed at night/afternoon, awake players receive
Valheim's native Yes/No prompt. Bed eligibility (roof, fire, enemies, wetness,
ownership and time) stays vanilla. Everyone awake must agree. No or no answer
within 60 seconds cancels; all sleepers must leave their beds before retrying.
Players already in bed count as agreeing only while they stay there.

After everyone agrees, a 30-second grace period lets nearby players get into bed.
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
and rested behavior before deployment.

After installing the staged two DLLs in a closed local client, `sleepvote_preview`
in F5 displays the native dialog using a mock request in a solo local world.
It never changes world time or forces sleep. Test Yes, No and the timeout. Bed
sleep with one real player remains vanilla, so use two clients for actual voting.
