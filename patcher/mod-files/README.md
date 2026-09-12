# Adding future server mods

Place additional client mod files in `common/` using their paths relative to the Valheim game folder, for example:

```
common/BepInEx/plugins/AnotherMod/AnotherMod.dll
common/BepInEx/plugins/AnotherMod/assets.bundle
common/BepInEx/config/AnotherMod.cfg
```

Use `windows/` or `linux/` for platform-specific files. Platform files override common files. Keep required dependency DLLs and the mod's license notices alongside it. New modules appear automatically in the launcher's release list. No launcher code change is necessary.

Run `prepare-release.py` with a higher bundle version and publish the signed manifests and packages together. The existing launcher then discovers and installs the new files. Removing a previously managed unchanged file from the package retires it on update. User-edited retired files stop the update for review, and existing configuration is preserved.

Do not copy your whole live server folder: server credentials, admin lists, logs and saves do not belong in client releases. Package only redistributable client files. These directories are excluded from source export; release assets contain their intended contents.

The patcher's limits remain 200 MiB compressed, 500 MiB expanded, 2,000 files, with paths confined to approved mod/loader areas. This supports normal BepInEx plugins, patchers, configuration and assets. Mods requiring an unrelated installer or files elsewhere need explicit integration. Compatibility with the game and other mods still needs testing.
