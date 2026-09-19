# MonkStyle 0.1.0

Three craftable wooden blunt fist weapons plus tree harvesting with vanilla claws. Requires BepInEx and Jötunn on every participating client/server. This build is local; it is not included in the public patcher or installed on the live server.

| Weapon / spawn ID | Based on | Base physical damage | Wood |
|---|---|---:|---|
| Timber Knuckles / `MonkStyle_TimberKnuckles` | Paws of the Bear | 25 blunt | Wood |
| Silverwood Knuckles / `MonkStyle_SilverwoodKnuckles` | Flesh Rippers | 60 blunt | Fine wood |
| Ironwood Knuckles / `MonkStyle_IronwoodKnuckles` | Vilebone Maulclaws | 80 blunt | Fine wood |

Names describe reinforced wooden knuckles; Silverwood/Ironwood are not new harvestable resources. Each uses 20 wood to craft, with a wood upgrade requirement of 10 per recipe upgrade level. The source recipe's claw/paw/ribcage requirement is replaced with wood. Other ingredients, idol gates, crafting station and station level are retained. Thus fine wood alone cannot unlock the later weapons. Recipe quantities come from the installed vanilla recipes.

Physical slash + pierce + blunt damage and its per-quality growth are converted into blunt on separate cloned items. Stamina, Fists skill, animations, kick, block, parry, backstab, durability, quality limit and movement characteristics remain those of the source weapon. Wooden models and distinct inventory icons are generated locally using game materials, without external asset downloads. Hand fit and appearance need an in-world visual test.

## Bear-set aura

Equipping Headdress of the Bear (`HelmetBerserkerHood`), Patterns of the Bear (`ArmorBerserkerChest`) and Loincloth of the Bear (`ArmorBerserkerLegs`) enables a cosmetic golden rising aura, soft warm light and three animated pale wind ribbons. No specific weapon is required. Armor quality does not affect activation.

The effect reads the game's replicated visual equipment hashes, so it works for local and remote players without a custom network message or persistent world data. It disappears on removing/replacing any piece, death, disabling the component, or leaving rendering range (60 m from the camera). There are no stat bonuses, hitboxes, force effects, sound loops or changes to the vanilla bear-set bonus. Dedicated servers do not create effects. Rendering is capped at 64 particles per player, with one short-range light that casts no shadows.

Local preference: `[Visuals] BearAura = false` in `BepInEx/config/scrubclub.monkstyle.cfg` hides these effects on that client. First-person brightness, day/night visibility, moving armor fit and remote-player equip/unequip behavior require in-world visual testing.

## Tree harvesting and unchanged native tool tiers

**No native weapon's stored tool tier or damage is changed.** During a normal primary melee attack with an explicitly supported vanilla claw, only hits against TreeBase, TreeLog or Destructible objects categorized by the game as Tree receive chopping damage and an axe-equivalent hit tier:

- Paws of the Bear: bronze axe, tier 2. Can chop birch and oak.
- Flesh Rippers: iron axe, tier 3. Yggdrasil shoots remain too hard.
- Vilebone Maulclaws: black metal axe, tier 4.

The three underlying claw items retain their vanilla tier 3. Tree minimum tiers, world-level rules and the native damage RPC remain untouched. Creature, building and mining hits are not modified. Bare fists, new blunt knuckles, Nord Knucklechains, other mods' items, kicks and falling-tree damage get no new chopping behavior.

Chop damage starts at 65% of the corresponding axe's chop damage at the same quality, retaining the attack's skill and combo scaling. This is an initial balance choice for faster punches, not a claim of identical harvesting throughput; test time-to-fell and stamina cost before release. Existing combat effects are preserved.

## Local validation

Run `bash mods/MonkStyle/scripts/build-test.sh` from the workspace. The project uses the existing local .NET toolchain and dependencies under BlueDepot. Tests verify damage conversion, explicit tier mapping, unsupported-weapon exclusion, upgrade/skill scaling, game hook compatibility, and absence of writes to native weapon tiers or tree hardness.

A separate headless Unity probe checks real prefab registration, recipes, models, icons, native stat preservation, and tree-vs-non-tree hit preparation. It does not open a world. The final build passed 40 unit/contract tests on Valheim 1.0.15. Weapon checks passed in a headless probe; aura construction, particle limits, wind, shadow-free lighting and cleanup passed in an OpenGL-enabled probe. No-graphics mode cannot resolve the required particle material, and the Vulkan diagnostic exited during native graphics initialization; OpenGL completed successfully. These checks do not replace visual review on an equipped character. Native tier-3 item values remained unchanged; bronze/iron/black-metal tiers appeared only on tree hits. The probe also exercised vanilla birch/Yggdrasil hardness checks, non-tree and unscoped exclusions, kick exclusion, nested attack scopes and exception cleanup. Recipes were registered against a disposable menu database using Jötunn's world-entry registration method. No saved world was opened. Runtime probe source lives in `tests/MonkStyle.RuntimeProbe`; do not distribute that diagnostic DLL.

In a disposable modded test world, use `devcommands`, then:

```
spawn MonkStyle_TimberKnuckles 1
spawn MonkStyle_SilverwoodKnuckles 1
spawn MonkStyle_IronwoodKnuckles 1
spawn FistBjornClaw 1
spawn FistFenrirClaw 1
spawn FistBjornUndeadClaw 1
```

Check both fists while moving/punching/kicking/blocking; compare damage and stamina against source weapons. Check crafting/upgrading, inventory/save-load, dropped-item appearance, trees/logs/stumps, birch/oak harvesting by bear claws, Yggdrasil rejection by flesh rippers, and successful higher-tier harvesting. Multiplayer owner/client checks remain necessary before release.

Do not load a world containing the new custom items without MonkStyle installed; vanilla cannot retain unknown custom item prefabs. Back up test saves before testing. This mod performs no chest/world migrations.
