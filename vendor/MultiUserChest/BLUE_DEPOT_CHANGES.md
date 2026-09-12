# Local compatibility build

MIT source from https://github.com/MSchmoecker/No-Chest-Block; see ../../dependencies.lock.json for revision and ../../licenses/MultiUserChest-MIT.txt for notice.

Changes: SDK-style project with workspace-local build references; plugin version changed from 0.6.1 to 0.6.2 to distinguish this local build. The upstream checkout already uses InventoryElement instead of the obsolete InventoryGrid.Element in the published 0.6.1 DLL. It is not an upstream 0.6.2 release.

No game binaries are included. Publicized reference assemblies are generated into an ignored build directory only.
