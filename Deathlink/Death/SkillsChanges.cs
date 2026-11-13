using System.Collections.Generic;
using HarmonyLib;
using Deathlink.Common;
using static Skills;
using UnityEngine;
using Deathlink.external;

namespace Deathlink.Death;

public static class SkillsChanges
{
    public static List<Skills.SkillType> skills_to_avoid_standard_death_penalty = new List<Skills.SkillType> { };

    // Vanilla calls this only on a hard death, so a soft death never reaches it.
    [HarmonyPatch(typeof(Skills), nameof(Skills.OnDeath))]
    static class OnDeath_Patch
    {
        private static bool Prefix(Skills __instance)
        {
            if (Deathlink.pcfg().DeathStyle.skillLossOnDeath) {
                float skill_based_diff = Mathf.Lerp(Deathlink.pcfg().DeathStyle.maxSkillLossPercentage, Deathlink.pcfg().DeathStyle.minSkillLossPercentage, DeathProgressionSkill.DeathSkillCalculatePercentWithBonus());
                Logger.LogDebug($"Lowering skills by a factor of {skill_based_diff} on death.");
                LowerConfigurableSkills(__instance, skill_based_diff);
            }
            return false;
        }

        private static void LowerConfigurableSkills(Skills skills, float factor)
        {
            foreach (KeyValuePair<SkillType, Skill> skillDatum in skills.m_skillData) {
                if (skills_to_avoid_standard_death_penalty.Contains(skillDatum.Key)) {
                    Logger.LogDebug($"Skipping lowering skill {skillDatum.Key} current level: {skillDatum.Value.m_level}");
                    continue;
                }
                float num = skillDatum.Value.m_level * factor;
                skillDatum.Value.m_level -= num;
                skillDatum.Value.m_accumulator = 0f;
            }
            if (Deathlink.RustyAlmanacClassesLoaded && ValConfig.EnableAlmanacClassesXPLossOnDeath.Value) {
                int level = AlmanacClasses.API.ClassesAPI.GetLevel();
                int xploss = Mathf.RoundToInt(level * factor * 500f * ValConfig.AlmanacClassesXPLossScale.Value) * -1;
                Logger.LogDebug($"Almanac (lvl {level}) XP Loss: {xploss}");
                AlmanacClasses.API.ClassesAPI.AddEXP(xploss);
            }
            if (Deathlink.WackyMMOLoaded && ValConfig.EnableWackyMMOXPLossOnDeath.Value) {
                int level = EpicMMOSystem_API.GetLevel();
                int xploss = Mathf.RoundToInt(level * factor * 500f * ValConfig.WackyMMOXPLossScale.Value) * -1;
                Logger.LogDebug($"WackyMMO (lvl {level}) XP Loss: {xploss}");
                EpicMMOSystem_API.AddExp(xploss);
            }

            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, "$msg_skills_lowered");
        }
    }

    [HarmonyPatch(typeof(Skills), nameof(Skills.RaiseSkill))]
    static class SkillRaisePatch
    {
        private static void Prefix(Skills __instance, Skills.SkillType skillType, ref float factor) {
            // factor must be 'ref': Harmony only writes a value-type argument back to the original
            // method when the prefix parameter is by-ref.
            float mod = Deathlink.pcfg().GetSkillBonusLazyCache(skillType);
            if (Logger.IsDebug) { Logger.LogDebug($"{skillType} skillGain Modified {mod}"); }
            if (mod != 0f) { factor *= mod; }
        }
    }
}