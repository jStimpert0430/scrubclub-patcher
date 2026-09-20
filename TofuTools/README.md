# TofuTools 0.1.0

Small bespoke gameplay features for scrubclub. The first feature protects earned skill levels on death.

## Death skill protection

A penalized death clears each skill's XP toward its next level but preserves its exact saved level. For example, level 30 with 80% progress becomes level 30 with 0% progress. Fractional levels remaining from old vanilla deaths are preserved too; no rounding or retroactive restoration is performed.

The normal repeat-death grace period still protects progress. A zero skill-loss world multiplier also preserves progress. The hardcore death setting that normally clears all skills instead clears only current-level progress. Grave creation, equipment rules, respawn, skill gains, and explicit administrative skill changes retain their normal behavior.

The Harmony patch replaces only `Skills.OnDeath` and `Skills.Clear` calls inside `Player.OnDeath`. It validates both call sites before applying, preserving native ownership and death-condition branches. It writes only `Skill.m_accumulator`, never `Skill.m_level`, and suppresses the misleading vanilla “skills lowered” message. No new saved fields, network messages, or world objects are introduced. Future TofuTools features should use separate patches.

## Installation and testing

Requires BepInEx on each player's client. Install `TofuTools.dll` under `BepInEx/plugins/TofuTools`. The same managed DLL supports Windows and Linux. Server installation alone cannot protect player-owned character skills. The DLL can also load on the server, but it does not enforce client installation or synchronize configuration.

Build and regression tests: `bash mods/TofuTools/scripts/build-test.sh`.

Tests execute production-transformed death instructions against simulated players, covering ordinary/hardcore deaths, multiple skills, repeated deaths, fractional/capped levels, zero penalties, ownership, grace periods, and unchanged administrative resets. Contract checks inspect the installed game's death method and skill fields. Tests do not edit real character saves. In-game load and death playtesting are still required before release.

Local test procedure: use a disposable test character/world, note two skill levels and progress bars, and die outside the repeat-death grace period. Levels must remain identical and progress reset. Gain progress and die during the grace period; progress should survive. Check grave/respawn behavior as usual. Do not use a main character to test deaths.

Included in modpack 0.2.11. Install on every client for death protection; server installation alone is insufficient.
