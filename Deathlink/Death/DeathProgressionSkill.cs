using System;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Managers;
using UnityEngine;
using Deathlink.Common;
using Deathlink.external;

namespace Deathlink.Death;

public static class DeathProgressionSkill
{
    public static Skills.SkillType DeathSkill = 0;
    private static long baselinePlayerId = 0L;
    private static float nextTickTime = 0f;
    private static float _bossKills = 0f;
    private static float _enemykills = 0f;
    private static float _piecesBuilt = 0f;
    private static float _mineAmount = 0f;
    private static float _treesChopped = 0f;
    private static float _craftAndUpgrades = 0f;

    public static void SetupDeathSkill()
    {
        SkillConfig deathskill = new SkillConfig();
        deathskill.Name = LocalizationManager.Instance.TryTranslate("$death_skill");
        deathskill.Description = LocalizationManager.Instance.TryTranslate("$death_skill_description");
        deathskill.Icon = Deathlink.EmbeddedResourceBundle.LoadAsset<Sprite>("Assets/Custom/Icons/death_skill.png");
        deathskill.Identifier = "midnightsfx.deathskill";
        deathskill.IncreaseStep = 0.1f;
        DeathSkill = SkillManager.Instance.AddSkill(deathskill);
        if (!SkillsChanges.skills_to_avoid_standard_death_penalty.Contains(DeathSkill)) {
            SkillsChanges.skills_to_avoid_standard_death_penalty.Add(DeathSkill);
        }
    }

    public static float DeathSkillCalculatePercentWithBonus()
    {
        float percentage = Player.m_localPlayer != null ? Player.m_localPlayer.GetSkillFactor(DeathSkill) : 0f;
        return Mathf.Clamp(percentage, 0.01f, 1f);
    }

    [HarmonyPatch(typeof(Player), nameof(Player.RaiseSkill))]
    public class Deathskill_EXP_Patch
    {
        public static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer || Game.instance == null) { return; }
            PlayerProfile profile = Game.instance.GetPlayerProfile();
            var stats = profile.m_playerStats[PlayerProfile.c_RawStats].m_stats;
            float bkillstat = stats[PlayerStatType.BossKills];
            float killstat = stats[PlayerStatType.EnemyKills];
            float builtpieces = stats[PlayerStatType.Builds];
            float treesChopped = stats[PlayerStatType.TreeChops];
            float miningAmount = stats[PlayerStatType.Mines];
            float craftAndUpgrade = stats[PlayerStatType.CraftsOrUpgrades];

            // The totals are per character, so another character's numbers are no baseline for this one.
            bool hasBaseline = profile.GetPlayerID() == baselinePlayerId;
            // RaiseSkill only runs while the player is doing something, so the interval is measured on
            // the clock rather than summed from the frames this happens to run on.
            if (hasBaseline && Time.time < nextTickTime) { return; }
            baselinePlayerId = profile.GetPlayerID();
            nextTickTime = Time.time + ValConfig.SkillProgressUpdateCheckInterval.Value;

            float bosskillxp = Mathf.Max(0f, bkillstat - _bossKills) * ValConfig.SkillGainOnBossKills.Value;
            float killxp = Mathf.Max(0f, killstat - _enemykills) * ValConfig.SkillGainOnKills.Value;
            float buildxp = Mathf.Max(0f, builtpieces - _piecesBuilt) * ValConfig.SkillGainOnBuilding.Value;
            float treeharvestxp = Mathf.Max(0f, treesChopped - _treesChopped) * ValConfig.SkillGainOnResourceGathering.Value;
            float mineharvestxp = Mathf.Max(0f, miningAmount - _mineAmount) * ValConfig.SkillGainOnResourceGathering.Value;
            float craftupgradexp = Mathf.Max(0f, craftAndUpgrade - _craftAndUpgrades) * ValConfig.SkillGainOnCrafts.Value;
            _bossKills = bkillstat;
            _enemykills = killstat;
            _piecesBuilt = builtpieces;
            _treesChopped = treesChopped;
            _mineAmount = miningAmount;
            _craftAndUpgrades = craftAndUpgrade;

            float total_xp_from_actions = bosskillxp + killxp + buildxp + treeharvestxp + mineharvestxp + craftupgradexp;
            if (!hasBaseline || total_xp_from_actions <= 0f) { return; }

            // The log is negative in the first second after a respawn, which would lower the skill and
            // hand out negative Almanac/WackyMMO XP.
            float skillbonus = Mathf.Max(0f, ((float)Math.Log(__instance.m_timeSinceDeath) / 5) * 0.5f);
            float curved_xp = (skillbonus * total_xp_from_actions);

            if (Deathlink.RustyAlmanacClassesLoaded) {
                int almXP = Mathf.RoundToInt(curved_xp * ValConfig.AlmanacClassesXPGainScale.Value);
                Logger.LogDebug($"Almanac XP Gain {almXP}");
                AlmanacClasses.API.ClassesAPI.AddEXP(almXP);
            }
            if (Deathlink.WackyMMOLoaded) {
                int wackyXP = Mathf.RoundToInt(curved_xp * ValConfig.WackyMMOXPGainScale.Value);
                Logger.LogDebug($"WackyMMO XP Gain {wackyXP}");
                EpicMMOSystem_API.AddExp(wackyXP);
            }

            // DeathSkillRate scales how fast the level's Deathlink skill climbs.
            float skill_rate = Deathlink.pcfg().DeathSkillRate;
            Logger.LogDebug($"DeathProgression skill bonus from survival (survive time: {__instance.m_timeSinceDeath}) {skillbonus} x {total_xp_from_actions} = {curved_xp}, death skill rate {skill_rate}");
            __instance.RaiseSkill(DeathSkill, curved_xp * skill_rate);
        }
    }
}