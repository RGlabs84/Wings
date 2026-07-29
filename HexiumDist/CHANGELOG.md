# Changelog

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
