# Changelog

## 1.9.1
- **Upgrading no longer requires deleting your config.** The mod now stamps a layout version on its config file and migrates old files automatically: any setting still sitting at its old default is moved to the new v2 default, while any value you or your server admin actually changed is preserved untouched. The previous file is backed up beside the config before anything is modified, and the log states exactly what was carried and what was kept.
- **Valkyrie Flight levels 20% slower.** Testing showed the skill climbing faster than intended, so the default XP rates dropped from 0.5 to 0.4 per flap and from 0.25 to 0.2 per glide-second. The migration applies the new rates to existing files too (unless you had already tuned them yourself).

## 1.9.0
**v2 test line.** 1.9.x builds are test versions for the upcoming 2.0.0 release. They deliberately will not connect to 1.1.x peers — version enforcement is per minor version, and the test line is meant to stay isolated.

- **New skill: Valkyrie Flight.** Flapping and gliding now level a real Valheim skill. Higher levels cut the stamina each flap costs (up to half at level 100), put more lift into every wingbeat (up to +30%), flatten your glide so you sink up to half as slowly for far longer flights, and add a touch of horizontal glide speed. Diving is untouched — pointing yourself at the ground is intent, not something practice should soften. XP rates and every bonus curve are server-configurable in the new "Valkyrie Flight Skill" section.
- **Wings are much harder to earn.** Crude Wings are now a Bronze Age project: bronze nails plus a mix of hides from the Meadows and Black Forest (deer hide, leather scraps, troll hide, feathers) at a level 3 workbench. Every later tier also costs significantly more — iron nails on Troll wings, linen thread on Lox wings, dragon tears on Dragon wings. Note for upgraders: BepInEx keeps your existing config values, so the new recipe defaults only apply to fresh configs. Delete `wubarrk.wingsofthevalkyrie.cfg` (or update the values by hand) to pick up the new balance.
- **Custom item icons.** Each wing tier and the new skill has its own icon instead of borrowing the vanilla cape art. The art ships embedded inside the mod DLL, so the mod is still a single file.

## 1.1.5
- **Test implementation: controller support.** Valheim keeps keyboard and gamepad on two separate Jump bindings, and the mod only ever read the keyboard one -- so controller players could jump normally but could never unfurl their wings or flap. Both bindings are now read, and any rebinding you have set in the game's own controls menu is respected. Controller feedback is welcome.
- Jump presses the game itself is ignoring no longer cost you a flap. Typing a space in chat, or pressing the gamepad's confirm button in the inventory, map, store, build menu or radial menu while airborne, used to trigger a flap and burn stamina.

## 1.1.4
- Fixed wings wrongly deploying while standing on or steering a moving ship. A player on deck is never `IsOnGround()` (the deck is a rigidbody, not terrain), so wave pitch and bob could spike vertical velocity past the auto-glide fall-speed threshold. The flight controller now checks whether you're standing on a ship, the same way it already checks for ground, water, and swimming.

## 1.1.3
- Fixed the vanilla cape reappearing over the runic wings. The 1.1.2 approach relied on `forceRenderingOff`, a runtime-only flag that Unity does not copy when the game instantiates the cape onto your skeleton. The cape attachment is now never built in the first place, so no other mod can bring it back and the dropped-item model stays visible.
- Fixed remote players' wings being completely invisible in multiplayer. Wing tier was being read from a field that Valheim only ever populates on the owning client; it is now read from the networked equipment hash.
- Glide state and wing flaps are now synchronised over the network, so other players see your wings unfurl and beat exactly when they should instead of being guessed from your fall speed.

## 1.1.2
- Improved how wing item prefabs handle visual components. Renderers are now kept active but use Unity's `forceRenderingOff` to safely guarantee invisibility
- Fixed a bug where adjusting the wingspan would cause the skeleton lines to blow out to blinding white due to additive shader scaling.
- Fixed multiplayer synchronization where remote players' wings were invisible or displayed incorrect tier colors.
- Implemented robust fail-safes in the flight controller to ensure multiplayer network latency or missing data never interrupts the core Valheim game loop.

## 1.1.1
- Internal

## 1.1.0
- Complete overhaul of wing animation to feature hyper-realistic, dynamic procedural folding and drag mechanics on the downstroke/upstroke
- Re-engineered the runic particle trail logic with custom distance tracking to guarantee flawless emission, completely bypassing Unity's built-in limitations
- Fixed a bug causing the glowing rune membrane to vanish due to camera frustum culling
- Added a new `GlobalWingSpan` configuration option (defaults to 1.0) allowing players to scale their wings to any size

## 1.0.1
- Added compatibility for third-party backpack and equipment/inventory mods (resolved shoulder slot conflict)

## 1.0.0
- Initial Release
- Added 4 craftable tiers of wings (Crude, Troll, Lox, Dragon)
- Implemented physics-based flight controller and auto-gliding mechanics
- Implemented configurable stamina costs per tier
- Added procedural runic dragon wing VFX with dynamic mesh generation
- Disabled fall damage when actively flying or gliding
