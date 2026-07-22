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

    [HarmonyPatch(typeof(Skills), nameof(Skills.OnDeath))]
    static class OnDeath_Patch
    {
        private static bool Prefix(Skills __instance)
        {
            // Skills not lowered on a soft death
            if (__instance.m_player.m_seman.HaveStatusEffect(SEMan.s_statusEffectSoftDeath)) {
                return false;
            }
            if (Deathlink.pcfg().DeathStyle.skillLossOnDeath) {
                // Do a decrease of skills, with our configuration
                
                float skill_based_diff = Mathf.Lerp(Deathlink.pcfg().DeathStyle.maxSkillLossPercentage, Deathlink.pcfg().DeathStyle.minSkillLossPercentage, DeathProgressionSkill.DeathSkillCalculatePercentWithBonus());
                Logger.LogDebug($"{skill_based_diff}");
                LowerConfigurableSkills(__instance, skill_based_diff);
            }

            // We are skipping the original skill decrease
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
            if (Deathlink.RustyAlmanacClassesLoaded) {
                if (ValConfig.EnableAlmanacClassesXPLossOnDeath.Value) {
                    int level = AlmanacClasses.API.ClassesAPI.GetLevel();
                    int xploss = Mathf.RoundToInt(level * factor * 500f * ValConfig.AlmanacClassesXPLossScale.Value) * -1;
                    Logger.LogDebug($"Almanac (lvl {level}) XP Loss: {xploss}");
                    AlmanacClasses.API.ClassesAPI.AddEXP(xploss);
                }
            }
            if (Deathlink.WackyMMOLoaded) {
                if (ValConfig.EnableWackyMMOXPLossOnDeath.Value) {
                    int level = EpicMMOSystem_API.GetLevel();
                    int xploss = Mathf.RoundToInt(level * factor * 500f * ValConfig.WackyMMOXPLossScale.Value) * -1;
                    Logger.LogDebug($"WackyMMO (lvl {level}) XP Loss: {xploss}");
                    EpicMMOSystem_API.AddExp(xploss);
                }
            }

            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, "$msg_skills_lowered");
        }
    }
    
    [HarmonyPatch(typeof(Skills), nameof(Skills.RaiseSkill))]
    static class SkillRaisePatch
    {
        private static void Prefix(Skills __instance, Skills.SkillType skillType, ref float factor) {
            // factor must be 'ref': Harmony only writes a value-type argument back to the original
            // method when the prefix parameter is by-ref. Without it the modifier below was silently
            // discarded and the death choice's SkillModifiers bonus never applied.
            float mod = Deathlink.pcfg().GetSkillBonusLazyCache(skillType);
            Logger.LogDebug($"{skillType} skillGain Modified {mod}");
            if (mod != 0f) {  factor *= mod; }
        }
    }
}