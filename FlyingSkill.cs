using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace WingsoftheValkyrie
{
    /// <summary>
    /// The "Valkyrie Flight" custom skill. Level scales flap stamina cost, flap lift, glide
    /// sink rate and glide speed (applied in <see cref="FlightController"/>); XP comes from
    /// flapping and from time spent gliding. Skill progress lives in the player save like any
    /// vanilla skill, so the death penalty and skill-loss protection come for free.
    ///
    /// Registration no longer goes through Jotunn's SkillManager, but the SkillType value is
    /// computed exactly as Jotunn computed it -- Math.Abs of the identifier's stable hash. That
    /// is not a stylistic choice: Skills.Save writes the skill under its integer SkillType, so
    /// a different number would orphan every level every player has earned since 1.9.0.
    /// </summary>
    [HarmonyPatch]
    public static class FlyingSkill
    {
        // Must never change. It is the identity of the skill in every player's save file.
        private const string Identifier = "wubarrk.wingsofthevalkyrie.flying";

        private const string SkillName = "Valkyrie Flight";
        private const string SkillDescription = "Mastery of the Valkyrie's wings. Cheaper, stronger wingbeats and longer glides.";

        public static Skills.SkillType SkillType { get; private set; } = Skills.SkillType.None;

        /// <summary>The localisation key vanilla builds for a skill's display name:
        /// SkillsDialog does Localize("$skill_" + m_skill.ToString().ToLower()), and for a value
        /// outside the enum ToString() is just the number.</summary>
        private static string LocalizationKey;

        private static Skills.SkillDef _definition;

        // Localization.AddWord is private, so there is no supported way to add a translation.
        private static readonly MethodInfo AddWordMethod =
            AccessTools.Method(typeof(Localization), "AddWord", new[] { typeof(string), typeof(string) });

        // Read instead of Localization.instance, which BUILDS the singleton on first touch --
        // its constructor loads the language resources and runs SetupLanguage. Doing that from a
        // BepInEx Awake would drag the whole localisation system up before the game asks for it.
        private static readonly FieldInfo LocalizationInstanceField =
            AccessTools.Field(typeof(Localization), "m_instance");

        // Glide XP is awarded in whole-second ticks; the fractional remainder carries across
        // glides so short hops still add up instead of being rounded away every landing.
        private static float _glideSeconds;

        public static void Register()
        {
            try
            {
                // Jotunn refused any identifier whose hash landed inside the vanilla enum's
                // range, and so do we: a collision would silently share a save slot with a
                // real skill.
                int hash = Math.Abs(Identifier.GetStableHashCode());
                if (hash <= (int)Skills.SkillType.All)
                {
                    Log.LogError($"The Valkyrie Flight skill identifier hashes to {hash}, which collides with vanilla's skill range. The skill will not be registered.");
                    return;
                }

                SkillType = (Skills.SkillType)hash;
                LocalizationKey = "skill_" + hash.ToString();

                _definition = new Skills.SkillDef
                {
                    m_skill = SkillType,
                    m_description = SkillDescription,
                    m_increseStep = 1f,   // vanilla's spelling
                    m_icon = IconLoader.Load("skill_valkyrie_flight.png")
                };

                AddLocalizationWord();
                Log.LogInfo($"Valkyrie Flight registered as skill {hash}.");
            }
            catch (Exception ex)
            {
                SkillType = Skills.SkillType.None;
                Log.LogError($"Could not register the Valkyrie Flight skill; flight will work but will not level. Reason: {ex}");
            }
        }

        /// <summary>
        /// Teaches Localization the skill's name. Missing translations come back as "[skill_123]"
        /// and are cached, so this has to be in place before anything asks -- and again after
        /// every language switch, because SetupLanguage clears the whole table. It is therefore
        /// called from three places, and does nothing if the table is not built yet.
        /// </summary>
        private static void AddLocalizationWord()
        {
            if (AddWordMethod == null || LocalizationKey == null || LocalizationInstanceField == null) return;

            var localization = LocalizationInstanceField.GetValue(null) as Localization;
            if (localization == null) return;

            AddWordMethod.Invoke(localization, new object[] { LocalizationKey, SkillName });
        }

        [HarmonyPatch(typeof(Localization), "SetupLanguage")]
        [HarmonyPostfix]
        private static void SetupLanguagePostfix()
        {
            AddLocalizationWord();
        }

        /// <summary>
        /// Skills.m_skills is the per-character list SkillDefs are looked up in, and it is
        /// deep-copied onto every Player instance, so the definition has to be added per
        /// character rather than once globally.
        /// </summary>
        [HarmonyPatch(typeof(Skills), "Awake")]
        [HarmonyPostfix]
        private static void SkillsAwakePostfix(Skills __instance)
        {
            if (_definition == null || __instance == null || __instance.m_skills == null) return;

            // A character exists, so localisation certainly does. This is the pass that matters
            // if the language was set up before our patches were applied.
            AddLocalizationWord();

            foreach (Skills.SkillDef existing in __instance.m_skills)
            {
                if (existing != null && existing.m_skill == SkillType) return;
            }

            __instance.m_skills.Add(_definition);
        }

        /// <summary>Whether the skill actually registered. When it did not, nothing can raise it,
        /// so every gate that depends on it has to stand aside rather than lock a player out of
        /// flight they can no longer earn their way into.</summary>
        public static bool IsAvailable => SkillType != Skills.SkillType.None;

        /// <summary>Skill factor 0..1 (level/100) used to scale flight stats. 0 when unavailable.</summary>
        public static float Factor(Player player)
        {
            if (player == null || SkillType == Skills.SkillType.None) return 0f;

            Skills skills = player.GetSkills();
            return skills != null ? skills.GetSkillFactor(SkillType) : 0f;
        }

        /// <summary>Skill level 0..100, as shown in the skills panel. 0 when unavailable, which
        /// is the safe answer: an unregistered skill must not hand out flight it did not earn.</summary>
        public static float Level(Player player)
        {
            if (player == null || SkillType == Skills.SkillType.None) return 0f;

            Skills skills = player.GetSkills();
            return skills != null ? skills.GetSkillLevel(SkillType) : 0f;
        }

        public static void AddFlapXP(Player player)
        {
            if (player == null || SkillType == Skills.SkillType.None) return;

            float xp = ModConfig.SkillXpPerFlap.Value;
            if (xp > 0f) player.RaiseSkill(SkillType, xp);
        }

        public static void AccumulateGlideXP(Player player, bool gliding, float deltaTime)
        {
            if (!gliding || player == null || SkillType == Skills.SkillType.None) return;

            _glideSeconds += deltaTime;
            if (_glideSeconds < 1f) return;
            _glideSeconds -= 1f;

            float xp = ModConfig.SkillXpPerGlideSecond.Value;
            if (xp > 0f) player.RaiseSkill(SkillType, xp);
        }
    }
}
