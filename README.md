# Wings of the Valkyrie
*By the grace of the Allfather, the skies of the Tenth World are no longer out of reach.*

**Wings of the Valkyrie** is a massive, physics-based flight overhaul for Valheim. Why trudge through the muddy swamps or sail across treacherous oceans when you can claim your birthright and take to the skies like a true Norse god?

Four craftable tiers of glowing runic wings await — and with **2.0.0**, flight stopped being a thing you craft and became a thing you learn. **2.0.1 finishes the thought.** The numbers printed on a set of wings are no longer what you get the moment you put them on; they are what **mastery** buys. A novice in Dragon wings is a novice. The sky is earned one wingbeat at a time — and now the mod keeps a **logbook** of every one of them.

**2.1.0 makes the wings themselves worth keeping.** They are no longer cape clones wearing a new icon — they are the mod's own items, with their own armour, their own resistances and their own **upgrade path**, and they no longer need Jotunn to exist.

**2.1.4 gives the skill its memory back.** From 2.1.0 to 2.1.2 the game quietly discarded your Valkyrie Flight level every time it loaded your character. It keeps it again — and if you lost one along the way, there is now a console command to put it back. **Update the server and every client together**: a 2.1.4 server will not let those three builds connect, because a client that is still deleting its own player's levels is not something a server can protect anyone from.

---

## ⚠️ Upgrading

> **Your config upgrades itself — again.** The mod stamps a layout version on its config file and migrates old files on first launch, whether they come from 1.1.x, 2.0.0, or anywhere in between. Any setting still sitting at its old default moves to the new balance; anything you or your server admin actually changed is **kept exactly as you set it**. A backup is written beside the config before a single value is touched, and the log lists what moved and what stayed. No config deleting, no lost admin work.
>
> **Coming from 1.1.x?** A 2.0.x client cannot join a 1.1.x server and vice versa — that is deliberate, since recipes, balance, and the skill all changed shape.
>
> **Coming from 2.0.0?** Patch releases still talk to each other, so a 2.0.1 client will connect to a 2.0.0 server. It just won't fly by the same rules: the 2.0.1 balance knobs don't exist on a 2.0.0 server to be synced, so update both ends together if you want everyone earning the sky at the same rate.
>
> **Coming from 2.0.2?** Nothing in your config changes, and a 2.0.3 client still connects to a 2.0.2 server. One behaviour does change: transforming some other back item into the wings through **AzuExtendedPlayerInventory's vanity slots** used to hand over real flight. It no longer does — flight comes from the wings you actually have equipped, and from nothing else. Real wings worn *underneath* a vanity cape still fly exactly as they always have.
>
> **Coming from 2.0.3?** The boat fix is decided entirely on your own machine, so updating your game is enough — a 2.0.4 client behaves itself on a 2.0.2 or 2.0.3 server, with nothing to change at the server end. Shipmates still on an older build will keep opening their wings on the swell until they update too.
>
> **Coming from 2.0.4?** Update the server and every client together — 2.1.0 will not talk to an older build. **Jotunn is no longer required**; you can remove it if nothing else on your list wants it. Coming straight here to 2.1.4, your wings, your Valkyrie Flight levels and your logbook all survive: the items keep their names and the skill keeps its identity, so nothing in your inventory or your save is touched. (If you have already been running 2.1.0, 2.1.1 or 2.1.2, read the next note.) Two config defaults move on their own — the Crude and Troll workbench requirement drops from level 3 to level 2, because upgrading needs a *higher* station than crafting and a workbench stops at level 5. If your admin had set those by hand, their values are kept as always.
>
> **Coming from 2.1.0, 2.1.1 or 2.1.2?** Those three builds reset your **Valkyrie Flight** level every time they loaded your character — a check in the game drops any skill it does not recognise, and the patch that made this one recognisable left with Jotunn in 2.1.0. 2.1.4 fixes it. **Update the server and every client together:** a 2.1.4 server refuses those three builds outright, because your character file lives on your own machine and a server has no other way to stop a stale client from deleting your levels. Nothing in your config changes, and nothing else on your character was ever affected.
>
> **Your lost level is handed back to you automatically, once, the first time you load in.** Nothing survives on the character itself, but the server still remembers what you had from the last time you flew, and 2.1.4 gives it back — with a message on screen, no commands and nothing to ask an admin for. It only ever raises a level, never lowers one, and it happens once per character. Anyone who *has* flown on a 2.1.0–2.1.2 client is past saving that way, because the server's record of them was overwritten too; `resetskill ValkyrieFlight` then `raiseskill ValkyrieFlight 86` puts them back by hand.
>
> **Coming from 2.1.4?** The automatic restore was in that build and gave everyone nothing — the server lost its record of what each pilot had a moment after reading it, answered "never heard of you", and the game then counted the repair as done. 2.1.5 fixes the cause and **disregards every mark 2.1.4 left**, so if you were one of the pilots it shrugged at, your restore is owed again: **updating your own game is enough**, and it happens on your next login.
>
> **Server admins:** the snapshot is taken the first time a 2.1.4 or newer server reads its flight registry, and written to `flight_skill_restore.dat` beside your export files. It is written once and never rewritten — **keep a copy.** Set `RestoreLostSkillLevels` to false if you would rather do it yourself.

---

## 🦅 What Awaits in the Sky

- **The Valkyrie Flight skill:** Flap and glide to grow a true Valheim skill, right in your skills panel. The sky remembers your effort — veterans flap for **45% of the stamina**, strike the air with **+50% lift**, sink **60% slower** on the glide, and cut through the wind **35% faster**. And like every skill, death takes its tithe.
- **Altitude is earned, not bought.** A tier's flight ceiling is what a **master** reaches. At skill 0 you get roughly a third of it, rising with every level. The Dragon wings that carry a veteran to 1300m will hold a beginner near 455m — put the hours in and the ceiling rises with you.
- **The higher wings must be earned too.** Troll, Lox and Dragon wings refuse to *beat* until you have the Valkyrie Flight levels to command them (15, 30 and 50 by default). They will still unfurl and still glide, always — and gliding is exactly how the skill is earned, so no one is ever grounded, only slowed down.
- **A logbook of your saga:** The mod quietly records every second you spend on the wing — time flown, distance, wingbeats, your highest altitude, your longest flight, your steepest stoop, which biomes you have crossed, and a fistful of stranger numbers besides. Read it in game with the `wov` console command. It lives on your character, so it travels with you.
- **Four Tiers of Ascension:** From crude bronze-nailed hides to god-tier wings woven with Eitr and a dragon's grief. Each tier raises the lift, the glide, the ceiling and the stamina economy you can *grow into*.
- **Wings that upgrade like the armour they are.** Every tier goes to **quality 4** at its crafting station, exactly as a chestpiece does. Upgrades are **deliberately expensive** — reaching quality 4 on Dragon wings costs 300 Feathers, 150 Eitr, 72 Scale Hide and 6 Dragon Tears on top of the craft — and they buy **armour first, flight second**: a fully upgraded pair is never a substitute for the tier above it.
- **Real armour and real resistances.** The wings carry their own stats now instead of a cape's. Dragon wings are **very resistant to fire** — they were cloned from the Feather Cape, which is *weak* to it, so until now a dragon's wings burned faster than bare skin. Lox wings shrug off frost, Troll wings blunt a club, and every tier makes leaving the ground cheaper.
- **Physics-Driven Flight:** Leap from a peak and jump again to unfurl your wings — the wind catches you instantly. Flap to climb, pitch your gaze downward to dive-bomb, level off to ride the glide. Land, swim, or step onto a ship's deck and the wings stow themselves.
- **No Fall Damage While Gliding:** The wind never lets a winged Viking break on the stones. Open wings mean a safe landing, always.
- **The Price of Flight:** The Allfather demands stamina. Every flap costs energy — higher tiers and higher skill both lighten the tribute, but the sky is never free.
- **Breathtaking Runic Visuals:** An articulated draconic wing skeleton, procedurally generated in real-time, bound by a shimmering membrane of woven Norse runes. Runic energy bleeds from your wingtips into the wind — and every other player sees your wings beat exactly when yours do, fully synchronised in multiplayer.
- **Hand-Forged Icons:** Every wing tier and the skill itself carries its own crafted sigil in your inventory. No more borrowed cape art.
- **Server Admin Sovereignty:** Every stat, recipe, armour value, resistance, XP rate and bonus curve is server-enforced config. Admins rule the skies; clients cannot override.
- **No Jotunn, no framework:** The wings, the recipes, the upgrade path, the Valkyrie Flight skill and the `wov` command are all registered against the game directly. One DLL, one dependency: BepInEx.

## 🕹️ How to Fly — the 60-Second Saga

1. **Craft wings and wear them** — they live in your cape slot.
2. **Jump while airborne** to unfurl. Falling fast unfurls them on its own — the wind refuses to waste a Viking.
3. **Jump again to flap** and climb. Each beat costs stamina — and the higher tiers will not beat at all until your Valkyrie Flight is high enough to command them.
4. **Look down to dive** — the steeper your gaze, the faster you fall upon your enemies. Level your eyes to stretch the glide.
5. **Touch ground, water, or a ship's deck** and the wings fold away.

Every flap and every second on the wind raises **Valkyrie Flight**. Fly often. Fly far. The sky keeps the ledger — and so, now, does your logbook.

> **Stuck with wings that won't beat?** Jump off something and glide. Glide time is the one thing that always pays, at every tier, at every level. That is the ladder: fall with style until you have earned the right to climb.

## 🛠️ The Wing Tiers & Crafting
*(All values below are default settings. Server admins can customize every recipe and stat in the config.)*

### 📋 Crafting Quick Reference

| Tier | Wings | Station | Flap needs | Recipe Ingredients |
| :--- | :--- | :--- | :--- | :--- |
| 🟢 **Tier 1** | **Crude Wings** | Workbench (Lvl 2) | — | 10× Bronze Nails, 10× Deer Hide, 20× Leather Scraps, 5× Troll Hide, 20× Feathers |
| 🔵 **Tier 2** | **Troll Hide Wings** | Workbench (Lvl 2) | Flight **15** | 15× Troll Hide, 10× Iron Nails, 30× Feathers |
| ⚪ **Tier 3** | **Lox Feathered Cloak** | Forge (Lvl 3) | Flight **30** | 10× Lox Pelt, 20× Silver, 10× Linen Thread, 30× Feathers |
| 🟣 **Tier 4** | **Dragon Valkyrie Wings** | Galdr Table (Lvl 1) | Flight **50** | 40× Feathers, 20× Eitr, 10× Scale Hide, 2× Dragon Tears |

### 📈 What Each Tier Is Worth — Novice vs Master

Every number below is a range, because every number now depends on you. The left value is a brand-new flier at **Valkyrie Flight 0**; the right is a **master at 100**. *Any* tier can be glided at any level — the requirement above is only for flapping.

| Tier | Flight Ceiling | Glide Speed | Flap Lift | Stamina / Flap |
| :--- | :--- | :--- | :--- | :--- |
| 🟢 Crude | 46m → **130m** | 8 → **10.8** | 11 → **16.5** | 16 → **7.2** |
| 🔵 Troll | 53m → **150m** | 12 → **16.2** | 13 → **19.5** | 13 → **5.9** |
| ⚪ Lox | 67m → **190m** | 16 → **21.6** | 16 → **24** | 10 → **4.5** |
| 🟣 Dragon | 455m → **1300m** | 24 → **32.4** | 20 → **30** | 7 → **3.2** |

Level glide sink follows the same rule: **2.5 m/s** when you start, **1.0 m/s** once you have mastered it — the difference between a hop and a crossing.

Every flight number above is for **quality 1** wings. A fully upgraded pair adds **+24% ceiling, +15% glide speed, +15% flap lift and −18% stamina per flap** on top — noticeable, and deliberately nowhere near the jump to the next tier.

---

### 🛡️ Armour, Resistances & Upgrades

The wings are the mod's own items now, not capes wearing new art, and they carry their own stats. Each tier upgrades to **quality 4** at its crafting station like any piece of armour — and the upgrades are **meant to hurt**.

| Tier | Armour (q1 → q4) | Resistances | Worn perks |
| :--- | :--- | :--- | :--- |
| 🟢 Crude | 2 → **5** | — | Jump stamina −10% |
| 🔵 Troll | 4 → **10** | Blunt: slightly resistant | Jump −15%, Run −5%, Sneak −15% |
| ⚪ Lox | 7 → **16** | Frost: resistant | Jump −20%, Run −10% |
| 🟣 Dragon | 10 → **22** | **Fire: very resistant**, Frost: resistant | Jump −25%, Run −15%, Sneak −10%, Eitr regen +10% |

> **The dragon does not burn.** Every tier was cloned from a vanilla cape, and the Dragon wings came from the Feather Cape — which ships a **Fire: very weak** modifier. So up to 2.0.4, the wings of a dragon made you burn *faster*. They are very resistant to fire now, as they always should have been.

**Upgrade costs.** Valheim charges *(quality − 1) ×* the per-level amount, so each step up costs more than the last. The right-hand column is the total for taking a fresh pair all the way to quality 4 — on top of the craft.

| Tier | Per quality level | Total to reach quality 4 | Station needed at q4 |
| :--- | :--- | :--- | :--- |
| 🟢 Crude | 25× Feathers, 15× Leather Scraps, 8× Bronze Nails, 6× Troll Hide | 150 / 90 / 48 / 36 | Workbench **5** |
| 🔵 Troll | 35× Feathers, 12× Troll Hide, 10× Iron Nails, 5× Silver | 210 / 72 / 60 / 30 | Workbench **5** |
| ⚪ Lox | 40× Feathers, 8× Lox Pelt, 25× Silver, 12× Linen Thread | 240 / 48 / 150 / 72 | Forge **6** |
| 🟣 Dragon | 50× Feathers, 25× Eitr, 12× Scale Hide, 1× Dragon Tear | 300 / 150 / 72 / **6 Dragon Tears** | Galdr Table **4** |

Upgrading demands a *higher* station than crafting — one level per quality step. That is why Crude and Troll wings ask for a level 2 workbench rather than level 3: a workbench tops out at level 5, and a floor of 3 would have put quality 4 permanently out of reach. Every one of these numbers is config.

---

### 🦅 Tier Breakdown & Flight Stats

🟢 **Tier 1: Crude Wings**
*Hides of the lowlands stretched over a bronze-nailed frame. Your first honest step into the sky.*
- **Crafting Station:** Workbench (Level 2)
- **Recipe:**
  - 10× Bronze Nails
  - 10× Deer Hide
  - 20× Leather Scraps
  - 5× Troll Hide
  - 20× Feathers
- **Flight Stats** *(novice → master)***:** Glide Speed: 8 → **10.8** | Flap Lift: 11 → **16.5** | Flight Ceiling: 46m → **130m** | Stamina: 16 → **7.2** per flap
- **Armour:** 2 → **5** at quality 4 | **Worn:** jump stamina −10%
- **Upgrade (per level):** 25× Feathers, 15× Leather Scraps, 8× Bronze Nails, 6× Troll Hide
- **Flapping requires:** nothing. These are the wings that teach you.

🔵 **Tier 2: Troll Hide Wings**
*Woven from the thick blue hide of trolls and riveted with iron, granting keener aerodynamics and a lighter burden.*
- **Crafting Station:** Workbench (Level 2)
- **Recipe:**
  - 15× Troll Hide
  - 10× Iron Nails
  - 30× Feathers
- **Flight Stats** *(novice → master)***:** Glide Speed: 12 → **16.2** | Flap Lift: 13 → **19.5** | Flight Ceiling: 53m → **150m** | Stamina: 13 → **5.9** per flap
- **Armour:** 4 → **10** at quality 4 | **Resists:** blunt (slight) | **Worn:** jump −15%, run −5%, sneak −15%
- **Upgrade (per level):** 35× Feathers, 12× Troll Hide, 10× Iron Nails, 5× Silver
- **Flapping requires:** Valkyrie Flight **15**

⚪ **Tier 3: Lox Feathered Cloak**
*Heavy, powerful wings of lox pelt bound in silver and linen — massive lift fit for a fully armored warrior.*
- **Crafting Station:** Forge (Level 3)
- **Recipe:**
  - 10× Lox Pelt
  - 20× Silver
  - 10× Linen Thread
  - 30× Feathers
- **Flight Stats** *(novice → master)***:** Glide Speed: 16 → **21.6** | Flap Lift: 16 → **24** | Flight Ceiling: 67m → **190m** | Stamina: 10 → **4.5** per flap
- **Armour:** 7 → **16** at quality 4 | **Resists:** frost | **Worn:** jump −20%, run −10%
- **Upgrade (per level):** 40× Feathers, 8× Lox Pelt, 25× Silver, 12× Linen Thread
- **Flapping requires:** Valkyrie Flight **30**

🟣 **Tier 4: Dragon Valkyrie Wings**
*Scale, Eitr, and the tears of a dragon, woven into dominion over the Tenth World.*
- **Crafting Station:** Galdr Table (Level 1)
- **Recipe:**
  - 40× Feathers
  - 20× Eitr
  - 10× Scale Hide
  - 2× Dragon Tears
- **Flight Stats** *(novice → master)***:** Glide Speed: 24 → **32.4** | Flap Lift: 20 → **30** | Flight Ceiling: 455m → **1300m** *(Reach the World Tree — once you have earned it!)* | Stamina: 7 → **3.2** per flap
- **Armour:** 10 → **22** at quality 4 | **Resists:** fire (very), frost | **Worn:** jump −25%, run −15%, sneak −10%, eitr regen +10%
- **Upgrade (per level):** 50× Feathers, 25× Eitr, 12× Scale Hide, 1× Dragon Tear
- **Flapping requires:** Valkyrie Flight **50**

---

## ⚔️ The Skill: Valkyrie Flight

The sky is a harsh tutor, but a fair one. **Valkyrie Flight** grows from doing — 0.4 XP per flap, 0.2 XP per second on the glide — and pays you back at every level:

| At skill 100 (scales linearly on the way up) | Default |
| :--- | :--- |
| Flap stamina cost | **−55%** |
| Flap lift | **+50%** |
| Glide sink rate (longer flights) | **−60%** |
| Horizontal glide speed | **+35%** |
| Flight ceiling | **×1.00** — from **×0.35** at level 0 |

That last line is the heart of 2.0.1. Skill is no longer a bonus sprinkled on top of flight you already had; it is the thing that gives you the flight in the first place. The tier you wear sets your ceiling *at mastery*, and your level decides how much of that ceiling is actually yours today.

**Two honest promises:** gliding always works, at every tier and every level — so the skill can always be earned. And diving is untouched by skill, because pointing yourself at the ground is intent, and the mountain does not care how practiced you are.

Death docks the skill like any other; the Valkyries respect only the living. Every XP rate, gate and bonus curve lives in the config under **"6. Valkyrie Flight Skill"** and in each tier's own section.

The skill answers to Valheim's own cheats as **`ValkyrieFlight`** — `raiseskill ValkyrieFlight 40`, `resetskill ValkyrieFlight` — and both commands offer it in tab completion. One word, no space: the console splits what you type on spaces and cannot be given a quoted name. `raiseskill` **adds** to your current level rather than setting it, so to land on an exact number, reset first and then raise. These are vanilla cheats and need what vanilla cheats need — cheats enabled, and admin on a server.

If a level ever goes missing, the server hands it back on your next login — once per character, upward only. Your level is also **mirrored onto your character** alongside the flight logbook, where Valheim's skill loader cannot reach it, and the mod checks at startup that the patch keeping the skill is really attached. Neither is how the skill normally persists — they are there so that a future game update or a mod conflict cannot delete anyone's progress without saying so.

## 📖 The Flight Logbook

Your character keeps a saga of its own time in the air. Open the console and type:

| Command | What it tells you |
| :--- | :--- |
| `wov log` | Time flown, distance, flights, wingbeats, and your records — highest altitude, longest flight, top speed, steepest stoop |
| `wov oddities` | The stranger corners: hours flown by night, times you ran dry of stamina mid-air, times the wings refused you, where you tend to land, which biomes you have crossed, and how long you have spent in each tier of wings |
| `wov export` | Writes the server's flight statistics file out now (on a server or a solo world) |
| `wov where` | Tells you where that file lives |

The logbook rides along in your character save, so it survives worlds, servers and reinstalls, and every player keeps their own. It is a record of what you did, nothing more — no cheat, no advantage. Switch it off with `EnableFlightLog` if you would rather not be counted at all.

### 🐦 Server Statistics (and BarrkBOT)

Everything the logbook measures is **client-side physics** — only your own game knows how long you glided or how high you got. So a server that wants to know its fliers has to be told: each client sends its running totals up to the server about once a minute, and the server writes them all into one file at

```
BepInEx/config/WingsOfTheValkyrie/barrkbot_flight.json
```

That file is what lets **BarrkBOT** answer "how long have I flown?" on Discord. It carries every player's time flown, distance, records and oddities, keyed by character with the display name inside, plus notes telling any reader what must *not* be done with the numbers (flight time is not playtime; flight distance shares no base with a sailing counter; the event counters must never be summed).

- Don't want to be in it? Set **`PublishFlightStats`** to `false` on your own machine. You keep your logbook; the server just never hears about it.
- Running the server? The same setting turns the whole export off, and `FlightStatsExportFolder` moves it — though moving it out of that folder is how you make the reader stop finding it, so leave it alone unless you know what is reading it.
- The numbers are whatever each client reports. That is fine for a logbook among people who know each other; it is not evidence, and nothing should be built on it that needs to be.
- **Everyone needs 2.0.1 for this.** A 2.0.0 client on a 2.0.1 server simply never reports and never appears in the file, quietly and with no error anywhere.

## 📜 Config & Server Sync

Everything binds to `BepInEx/config/wubarrk.wingsofthevalkyrie.cfg` — per-tier ceilings, glide speeds, flap forces, stamina costs, flap skill requirements, crafting stations, full recipes, **upgrade recipes, armour, resistances, weight, durability and worn stat modifiers**, the base sink and dive rates, the shared `[8. Wing Upgrades]` curve, and the entire skill section. All gameplay entries are **admin-only and server-enforced**: the server pushes its values to every client live, and non-admins cannot override them. Changes land on the wings **immediately** — an admin retuning armour or a server pushing its copy down does not need anyone to restart. If a `CraftingStation` is set to a prefab the game does not have — a typo, or a bench from a mod you no longer run — that tier simply cannot be crafted and the log names it; it never quietly becomes craftable with no bench at all. The `[0. Meta] ConfigVersion` line is the migration stamp — leave it be.

Two entries are worth knowing by name. `DamageModifiers` takes `Type:Modifier` pairs — `Fire:VeryResistant,Frost:Resistant` — over the usual damage types and Valheim's own modifier names. `UpgradeRequirements` is charged **per quality level**, so `Feathers:25` means 25 feathers for quality 2, 50 for quality 3 and 75 for quality 4. Set `[8. Wing Upgrades] MaxQuality` to `1` to switch upgrading off entirely.

Want the old 2.0.0 feel back? Set `CeilingAtNovice` to `1` and every tier's `MinSkillToFlap` to `0`, and altitude and flapping stop being gated entirely.

The logbook section (`7. Flight Logbook`) is deliberately **not** server-synced. Whether your own flights are recorded and whether they are shared with the server are yours to decide, and where a server keeps its statistics file is a property of that machine rather than a rule of the game.

## 📥 Installation

**With Gale (recommended):** just install Wings of the Valkyrie — dependencies come with it.

**Manual install:**
1. Install **[BepInExPack Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/)**. That is the whole dependency list — **Jotunn is no longer required** as of 2.1.0, and can be removed if nothing else on your list wants it.
2. Drop `WingsoftheValkyrie.dll` into `BepInEx/plugins`.
3. Take to the skies!

Every player and the server must run the **same** version, and this is enforced at the door rather than discovered later: a client carrying the mod is refused by a server without it, and a client without it is refused by a server that has it. If wings did somehow reach the ground on a server that lacked the mod, that server would delete them permanently — but the connect check means it never gets that far.

<div align="center">
  <i>Created with ❤️ by Wubarrk</i>
</div>

---
join the Mists of Avalor Open BETA find it only on [Hexium](https://valheim.hexium.gg/mods/Wubarrk/Mists_of_Avalor_BETA)
<br>
<small>

**License:** MIT — use, modify, and redistribute freely, including commercially; just keep the copyright notice. See `LICENSE.md` for the full text.

</small>
