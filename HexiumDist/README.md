# Wings of the Valkyrie
*By the grace of the Allfather, the skies of the Tenth World are no longer out of reach.*

**Wings of the Valkyrie** is a massive, physics-based flight overhaul for Valheim. Why trudge through the muddy swamps or sail across treacherous oceans when you can claim your birthright and take to the skies like a true Norse god?

Four craftable tiers of glowing runic wings await — and with **2.0.0**, flight is no longer just a thing you craft. **It is a thing you learn.** Every wingbeat feeds the new **Valkyrie Flight** skill, every tier bears its own hand-forged icon, and every set of wings must now be *earned* the honest Viking way: with metal, hide, and the occasional tear of a dragon.

---

## ⚠️ Upgrading from 1.1.x

> **Your config upgrades itself.** 2.0.0 stamps a version on its config file and migrates old files on first launch: any setting still at its old default moves to the new 2.0 balance, while anything you or your server admin actually changed is **kept exactly as you set it**. A backup is written beside the config before a single value is touched, and the log lists what moved and what stayed. No config deleting, no lost admin work.
>
> **Everyone must update together.** A 2.0.0 client cannot join a 1.1.x server and vice versa — that is deliberate, since recipes, balance, and the skill all changed shape.

---

## 🦅 What Awaits in the Sky

- **The Valkyrie Flight skill:** Flap and glide to grow a true Valheim skill, right in your skills panel. The sky remembers your effort — veterans flap for half the stamina, strike the air with +30% more lift, sink half as slowly on the glide, and cut through the wind faster. And like every skill, death takes its tithe.
- **Four Tiers of Ascension:** From crude bronze-nailed hides to god-tier wings woven with Eitr and a dragon's grief. Each tier grants superior lift, faster glide, a lighter stamina toll, and a higher flight ceiling.
- **Physics-Driven Flight:** Leap from a peak and jump again to unfurl your wings — the wind catches you instantly. Flap to climb, pitch your gaze downward to dive-bomb, level off to ride the glide. Land, swim, or step onto a ship's deck and the wings stow themselves.
- **No Fall Damage While Gliding:** The wind never lets a winged Viking break on the stones. Open wings mean a safe landing, always.
- **The Price of Flight:** The Allfather demands stamina. Every flap costs energy — higher tiers and higher skill both lighten the tribute, but the sky is never free.
- **Breathtaking Runic Visuals:** An articulated draconic wing skeleton, procedurally generated in real-time, bound by a shimmering membrane of woven Norse runes. Runic energy bleeds from your wingtips into the wind — and every other player sees your wings beat exactly when yours do, fully synchronised in multiplayer.
- **Hand-Forged Icons:** Every wing tier and the skill itself carries its own crafted sigil in your inventory. No more borrowed cape art.
- **Server Admin Sovereignty:** Every stat, recipe, XP rate, and bonus curve is server-enforced config. Admins rule the skies; clients cannot override.

## 🕹️ How to Fly — the 60-Second Saga

1. **Craft wings and wear them** — they live in your cape slot.
2. **Jump while airborne** to unfurl. Falling fast unfurls them on its own — the wind refuses to waste a Viking.
3. **Jump again to flap** and climb. Each beat costs stamina.
4. **Look down to dive** — the steeper your gaze, the faster you fall upon your enemies. Level your eyes to stretch the glide.
5. **Touch ground, water, or a ship's deck** and the wings fold away.

Every flap and every second on the wind raises **Valkyrie Flight**. Fly often. Fly far. The sky keeps the ledger.

## 🛠️ The Wing Tiers & Crafting
*(All numbers below are the default runes. Server admins may rewrite every one of them in the config.)*

🟢 **Tier 1: Crude Wings**
*Hides of the lowlands stretched over a bronze-nailed frame. Your first honest step into the sky.*
- **Forge:** Workbench (Level 3) | 10x Bronze Nails, 10x Deer Hide, 20x Leather Scraps, 5x Troll Hide, 20x Feathers
- **Glide Speed:** 10 | **Flap Lift:** 15 | **Flight Ceiling:** 120m
- **Stamina Tribute:** 10 per flap

🔵 **Tier 2: Troll Hide Wings**
*Woven from the thick blue hide of trolls and riveted with iron, granting keener aerodynamics and a lighter burden.*
- **Forge:** Workbench (Level 3) | 15x Troll Hide, 10x Iron Nails, 30x Feathers
- **Glide Speed:** 15 | **Flap Lift:** 18 | **Flight Ceiling:** 135m
- **Stamina Tribute:** 8 per flap

⚪ **Tier 3: Lox Feathered Cloak**
*Heavy, powerful wings of lox pelt bound in silver and linen — massive lift fit for a fully armored warrior.*
- **Forge:** Forge (Level 3) | 10x Lox Pelt, 20x Silver, 10x Linen Thread, 30x Feathers
- **Glide Speed:** 20 | **Flap Lift:** 22 | **Flight Ceiling:** 160m
- **Stamina Tribute:** 6 per flap

🟣 **Tier 4: Dragon Valkyrie Wings**
*Scale, Eitr, and the tears of a dragon, woven into dominion over the Tenth World.*
- **Forge:** Galdr Table | 40x Feathers, 20x Eitr, 10x Scale Hide, 2x Dragon Tears
- **Glide Speed:** 30 | **Flap Lift:** 28 | **Flight Ceiling:** 1100m *(Reach the World Tree!)*
- **Stamina Tribute:** 4 per flap

## ⚔️ The Skill: Valkyrie Flight

The sky is a harsh tutor, but a fair one. **Valkyrie Flight** grows from doing — 0.4 XP per flap, 0.2 XP per second on the glide — and pays you back at every level:

| At skill 100 (scales linearly on the way up) | Default |
| :--- | :--- |
| Flap stamina cost | **−50%** |
| Flap lift | **+30%** |
| Glide sink rate (longer flights) | **−50%** |
| Horizontal glide speed | **+15%** |

Diving is untouched by skill — pointing yourself at the ground is intent, and the mountain does not care how practiced you are. Death docks the skill like any other; the Valkyries respect only the living. Every XP rate and bonus curve lives in the config under **"6. Valkyrie Flight Skill"**.

## 📜 Config & Server Sync

Everything binds to `BepInEx/config/wubarrk.wingsofthevalkyrie.cfg` — per-tier ceilings, glide speeds, flap forces, stamina costs, crafting stations, full recipes, and the entire skill section. All gameplay entries are **admin-only and server-enforced**: the server pushes its values to every client live, and non-admins cannot override them. The `[0. Meta] ConfigVersion` line is the migration stamp — leave it be.

## 📥 Installation

**With Gale (recommended):** just install Wings of the Valkyrie — dependencies come with it.

**Manual install:**
1. Install **[BepInExPack Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/)** and **[Jotunn](https://valheim.thunderstore.io/package/ValheimModding/Jotunn/)**.
2. Drop `WingsoftheValkyrie.dll` into `BepInEx/plugins`.
3. Take to the skies!

<div align="center">
  <i>Created with ❤️ by Wubarrk</i>
</div>

---
join the Mists of Avalor Open BETA find it only on [Hexium](https://valheim.hexium.gg/mods/Wubarrk/Mists_of_Avalor_BETA)
<br>
<small>

**License to Modify:**
User may alter, translate, or create derivative works based on the original material, provided two conditions are met: 1) A request to alter the work is made and approved by the original author (waived in the case of simple translations), and 2) Full attribution is given to the original author along with a link to the original work (if still available).  All changes should be clearly marked or noted as modified from the original source.

Modification or reuse of the work does not grant the user any right to imply official endorsement or sponsorship by the original creator.

The permission granted herein is non-exclusive, and the original creator retains all rights, title, and interest in the original work.

</small>
