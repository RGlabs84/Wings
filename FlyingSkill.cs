using Jotunn.Configs;
using Jotunn.Managers;

namespace WingsoftheValkyrie
{
    /// <summary>
    /// The "Valkyrie Flight" custom skill. Level scales flap stamina cost, flap lift, glide
    /// sink rate and glide speed (applied in <see cref="FlightController"/>); XP comes from
    /// flapping and from time spent gliding. Skill progress lives in the player save like any
    /// vanilla skill, so the death penalty and skill-loss protection come for free.
    /// </summary>
    public static class FlyingSkill
    {
        // Jotunn hashes the identifier into the SkillType value. It must never change:
        // a renamed identifier orphans every player's accumulated levels under the old hash.
        public static Skills.SkillType SkillType { get; private set; } = Skills.SkillType.None;

        // Glide XP is awarded in whole-second ticks; the fractional remainder carries across
        // glides so short hops still add up instead of being rounded away every landing.
        private static float _glideSeconds;

        public static void Register()
        {
            SkillType = SkillManager.Instance.AddSkill(new SkillConfig
            {
                Identifier = "wubarrk.wingsofthevalkyrie.flying",
                Name = "Valkyrie Flight",
                Description = "Mastery of the Valkyrie's wings. Cheaper, stronger wingbeats and longer glides.",
                Icon = IconLoader.Load("skill_valkyrie_flight.png"),
                IncreaseStep = 1f
            });
        }

        /// <summary>Skill factor 0..1 (level/100) used to scale flight stats. 0 when unavailable.</summary>
        public static float Factor(Player player)
        {
            if (player == null || SkillType == Skills.SkillType.None) return 0f;

            Skills skills = player.GetSkills();
            return skills != null ? skills.GetSkillFactor(SkillType) : 0f;
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
