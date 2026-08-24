# Changelog

## 2.0.4
**The boat fix that never was.** 1.1.4 announced that the wings had stopped snapping open on a ship's deck. They had not, and this is the release where they actually do. Nothing else changes -- no balance, recipe or config value is touched -- and because the whole decision is made on your own machine, **updating your own game is enough**: a 2.0.4 client behaves itself on a 2.0.2 or 2.0.3 server with nothing to change at the server end.

- **The wings no longer open when a swell drops the deck out from under you.** The check 1.1.4 added could never fire. Valheim's own `GetStandingOnShip()` gives up the instant your feet leave the planks -- which is precisely the instant a wave throws you -- so the guard went blank exactly when it was needed, and the deck falling away read as a fall. The mod now asks whether you are **aboard** rather than whether you are **standing**, and that stays true right through the bob. Flapping from a deck still works: taking off from your own longship is intent, not an accident.
- **Your shipmates will keep flaring until they update.** Whether someone's wings are open is decided on their machine and reported to yours, so a sailor still on 2.0.3 or older opens theirs on every swell no matter which build you are on. (The guess the mod falls back to for clients too old to report at all got the same correction.)

## 2.0.3
**A costume is not a pair of wings.** A player reported that transforming any back item into the Wings of the Valkyrie through AzuExtendedPlayerInventory's vanity system handed over real flight -- gliding, flapping, wingbeats, the lot -- while an ordinary Deer Cape stayed on their shoulders and the wings themselves went uncrafted. It does not any more. Nothing about flying itself changes, no balance, recipe or config value is touched, and a 2.0.3 client still connects to a 2.0.2 server.

- **Flight is granted by what you have equipped, never by what you look like.** The mod was asking the player *model* what was on its back, and telling the model to lie is exactly what a vanity slot is for. It now asks the shoulder slot itself. That closes the loophole from both ends at once: a costume of the wings grants nothing, and real wings worn underneath somebody else's costume fly exactly as they always did.
- **Other fliers are drawn from what their own game reports**, rather than from what their character appears to be wearing, so a costume across the server no longer sprouts rune wings on your screen -- and a pilot hiding real wings under a cape keeps their trails. Anyone still on 2.0.2 or older does not report it, and is drawn the old way.
- **Choosing the wings as a costume now shows nothing on your back**, which is what the wings look like whenever you are not flying: they are woven out of runes in the air, not cloth on your shoulders.

## 2.0.2
**Nothing changes in the air.** This release is entirely about the flight statistics file the server publishes for BarrkBOT — the numbers were being written correctly all along, and almost none of them were reaching the bot. No flight behaviour, balance, recipe or config value is touched, and because version enforcement is per minor version, a 2.0.2 client and a 2.0.1 server still connect happily. Update the server to get the benefit.

- **The export is now four files instead of one.** BarrkBOT budgets each file by how *wide* its rows are, and one file carrying twenty-one fields per pilot spent that budget on three pilots' worth of columns — so only two of five fliers were ever listed, and the bot could not name who had flown furthest. The same data is now split by subject: `barrkbot_flight.json` (career totals), `_records` (personal bests), `_counters` (event tallies) and `_tiers` (wing grades and biomes). **Every file holds every pilot**, so any question answered from any one of them is answered over the whole server.
- **Five new things the bot can rank.** Time on each grade of wings used to be one nested block that BarrkBOT dropped from every listing it appeared in, so nobody could ask about it. It is now four plain numbers — `tier_time_crude_seconds`, `_troll_`, `_lox_`, `_dragon_` — and "who has the most time on dragon wings" is a question with an answer for the first time. `distinct_biomes_flown` joins the biome list for the same reason: a list cannot be ranked, a count can.
- **The export grows on its own now.** Past a certain roster the files continue into `_2`, `_3` and so on without anyone releasing a new version of this mod. Each continuation carries a leaderboard computed over *every* pilot on the server, not just the ones in that file, so a question answered from part two still names the true record holder. Nothing rolls over until it has to — a five-pilot server stays at four files for a long while yet.
- **Old exports are cleaned up rather than left to rot.** A statistics file this version no longer writes is deleted, because the bot has no way of knowing a file has been abandoned: it reads it as current data with an old timestamp and ranks it accordingly. Files that are not ours are never touched.
- Pilots who share a character name are told apart on a leaderboard by character id, instead of appearing as one person with two different records.

## 2.0.1
**Earn the sky.** 2.0.0 made flight something you learn; 2.0.1 makes that mean something. The numbers printed on a set of wings are no longer what you get the moment you put them on — they are what mastery buys. A 2.0.1 client will still connect to a 2.0.0 server, but the new balance knobs do not exist there to be synced, so update both ends together.

- **Altitude is now gated by skill.** Each tier's `FlightCeiling` is what a flier at Valkyrie Flight 100 reaches; at level 0 you get 35% of it, rising steadily with every level. Dragon wings carry a master to 1300m and a beginner to about 455m. The new `CeilingAtNovice` setting controls the floor — set it to 1 to switch the whole idea off.
- **The higher tiers must be earned before they will beat.** Troll, Lox and Dragon wings now require Valkyrie Flight 15, 30 and 50 respectively before they will flap. **They always unfurl and always glide**, at every tier and every level — and glide time is what pays for the skill, so nobody can be stranded by this, only slowed down. Per-tier `MinSkillToFlap`, 0 to disable.
- **Every tier rebalanced around novice and master.** Base flap lift, glide speed and stamina cost came down, and the skill bonus curves went up to meet them: at level 100 a flap now costs 45% of the listed stamina (was 50%), lands 50% more lift (was 30%), sinks 60% slower on a level glide (was 50%) and carries 35% more glide speed (was 15%). A master flies better than they ever did in 2.0.0. A novice does not.
- **Ceilings raised at the top end**, since they now have to be climbed to: 120→130m, 135→150m, 160→190m, 1100→1300m.
- **New flight logbook.** The mod now keeps a saga of your time in the air — time flown, distance covered, wingbeats, flights, and records for highest altitude, longest flight, top speed and steepest dive. Plus the odd corners: hours flown by night, times you ran out of stamina mid-air, times the wings refused you, whether you land on ground, water or a ship's deck, which biomes you have crossed, and how long you have spent in each tier of wings. Read it in game with the `wov` console command (`wov log`, `wov oddities`, `wov export`, `wov where`).
- The logbook lives on your character in vanilla per-character save data, so it travels with you across worlds, servers and reinstalls, and each player keeps their own.
- **Server flight statistics, and BarrkBOT.** Everything the logbook measures is client-side physics — a server never sees your glide time or your altitude, because your own game is the only machine simulating them. So each client now sends its running totals up to the server about once a minute, and the server gathers them into `BepInEx/config/WingsOfTheValkyrie/barrkbot_flight.json`: every player's time flown, distance, records and oddities, keyed by character with the display name inside. That file is what lets BarrkBOT answer questions about your flying on Discord. Set `PublishFlightStats` to false on a client to keep your logbook out of it, or on a server to switch the whole export off. Everyone needs 2.0.1 — a 2.0.0 client never reports and never appears, with no error anywhere to tell you so.
- **Two magic numbers became settings.** Level glide sink (previously a hardcoded 2 m/s, now `BaseGlideSinkRate` at 2.5) and full-dive speed (previously a hardcoded 20 m/s, now `MaxDiveSpeed`, still 20).
- **Config migration handles all of it.** Files from 1.1.x, 2.0.0 or anywhere in between are carried forward in one pass: values still at their old defaults move to the new balance, values you changed are kept exactly as they are, and a backup is written first. Existing configs need no attention at all.

## 2.0.0
**The v2 overhaul.** Flight is no longer just a thing you craft — it is a thing you learn. Note that 2.0.0 clients and servers must both be on 2.0.0: version enforcement is per minor version, so 1.1.x peers cannot connect (and vice versa).

- **New skill: Valkyrie Flight.** Flapping and gliding now level a real Valheim skill, right in the skills panel with its own icon. Higher levels cut the stamina each flap costs (up to half at level 100), put more lift into every wingbeat (up to +30%), flatten your glide so you sink up to half as slowly for far longer flights, and add a touch of horizontal glide speed. Diving is untouched — pointing yourself at the ground is intent, not something practice should soften. Death docks the skill like any other. XP rates (0.4 per flap, 0.2 per glide-second) and every bonus curve are server-configurable in the new "Valkyrie Flight Skill" section.
- **Wings are much harder to earn.** Crude Wings are now a Bronze Age project: bronze nails plus a mix of hides from the Meadows and Black Forest (deer hide, leather scraps, troll hide, feathers) at a level 3 workbench. Every later tier also costs significantly more — iron nails on Troll wings, linen thread on Lox wings, dragon tears on Dragon wings.
- **Your config upgrades itself.** The mod stamps a layout version on its config file and migrates old files automatically: any setting still sitting at its old default moves to the new 2.0 default, while any value you or your server admin actually changed is preserved untouched. The previous file is backed up beside the config before anything is modified, and the log states exactly what was carried and what was kept. No config deleting required.
- **Custom item icons.** Each wing tier and the new skill bears its own hand-forged icon instead of borrowing the vanilla cape art. The art ships embedded inside the mod DLL, so the mod is still a single file.

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
