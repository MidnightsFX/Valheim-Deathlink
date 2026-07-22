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
    private static float lastSkillIncreaseTickTime = 0f;
    private static float timeSinceGameStart = 0;
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
        deathskill.Icon = Deathlink.EmbeddedResourceBundle.LoadAsset<Sprite>("Assets/Custom/Icons/death_skill.png"); ;
        deathskill.Identifier = "midnightsfx.deathskill";
        deathskill.IncreaseStep = 0.1f;
        DeathSkill = SkillManager.Instance.AddSkill(deathskill);
        if (!SkillsChanges.skills_to_avoid_standard_death_penalty.Contains(DeathSkill)) {
            SkillsChanges.skills_to_avoid_standard_death_penalty.Add(DeathSkill);
        }
    }


    public static float DeathSkillCalculatePercentWithBonus(float bonus = 0.0f, float min = 0.01f, float max = 1.0f)
    {
        float percentage = 0f;
        if (Player.m_localPlayer != null) {
            float player_skill_level = Player.m_localPlayer.GetSkillFactor(DeathSkill);
            percentage += player_skill_level;
        }
        
        percentage += bonus;
        if (percentage < min) {  percentage = min;  }
        if (percentage > max) { percentage = max; }
        return percentage;
    }


    [HarmonyPatch(typeof(Player), nameof(Player.RaiseSkill))]
    public class Deathskill_EXP_Patch
    {
        public static void Postfix(Player __instance)
        {
            timeSinceGameStart += Time.deltaTime;
            if (lastSkillIncreaseTickTime == 0f)
            {
                lastSkillIncreaseTickTime = timeSinceGameStart + ValConfig.SkillProgressUpdateCheckInterval.Value;
                PlayerProfile profile = Game.instance.GetPlayerProfile();
                _bossKills = profile.m_playerStats.m_stats[PlayerStatType.BossKills];
                _enemykills = profile.m_playerStats.m_stats[PlayerStatType.EnemyKills];
                _piecesBuilt = profile.m_playerStats.m_stats[PlayerStatType.Builds];
                _treesChopped = profile.m_playerStats.m_stats[PlayerStatType.TreeChops];
                _mineAmount = profile.m_playerStats.m_stats[PlayerStatType.Mines];
                _craftAndUpgrades = profile.m_playerStats.m_stats[PlayerStatType.CraftsOrUpgrades];
            }
            // Logger.Debug($"DeathSkill increase interval check: {timeSinceGameStart} > {lastSkillIncreaseTickTime}");
            if (timeSinceGameStart > lastSkillIncreaseTickTime)
            {
                // set when we should tick the next increase
                lastSkillIncreaseTickTime = timeSinceGameStart + ValConfig.SkillProgressUpdateCheckInterval.Value;

                PlayerProfile profile = Game.instance.GetPlayerProfile();
                float bkillstat = profile.m_playerStats.m_stats[PlayerStatType.BossKills];
                float killstat = profile.m_playerStats.m_stats[PlayerStatType.EnemyKills];
                float builtpieces = profile.m_playerStats.m_stats[PlayerStatType.Builds];
                float treesChopped = profile.m_playerStats.m_stats[PlayerStatType.TreeChops];
                float miningAmount = profile.m_playerStats.m_stats[PlayerStatType.Mines];
                float craftAndUpgrade = profile.m_playerStats.m_stats[PlayerStatType.CraftsOrUpgrades];
                float craftupgradexp = 0;
                float mineharvestxp = 0;
                float treeharvestxp = 0;
                float buildxp = 0;
                float killxp = 0;
                float bosskillxp = 0;
                if (bkillstat > _bossKills || killstat > _enemykills)
                {
                    bosskillxp = (bkillstat - _bossKills) * ValConfig.SkillGainOnBossKills.Value;
                    killxp = (killstat - _enemykills) * ValConfig.SkillGainOnKills.Value;
                    Logger.LogDebug($"DeathProgression kill skill bosskill: {bosskillxp} kill: {killxp}");
                    _bossKills = bkillstat;
                    _enemykills = killstat;
                }
                if (builtpieces > _piecesBuilt)
                {
                    buildxp = (builtpieces - _piecesBuilt) * ValConfig.SkillGainOnBuilding.Value;
                    Logger.LogDebug($"DeathProgression building skill: {buildxp}");
                    _piecesBuilt = builtpieces;
                }
                if (treesChopped > _treesChopped || miningAmount > _mineAmount)
                {
                    treeharvestxp = (treesChopped - _treesChopped) * ValConfig.SkillGainOnResourceGathering.Value;
                    mineharvestxp = (miningAmount - _mineAmount) * ValConfig.SkillGainOnResourceGathering.Value;
                    Logger.LogDebug($"DeathProgression harvesting skill tree_harvest: {treeharvestxp} mining: {mineharvestxp}");
                    _treesChopped = treesChopped;
                    _mineAmount = miningAmount;
                }
                if (craftAndUpgrade > _craftAndUpgrades)
                {
                    craftupgradexp = (craftAndUpgrade - _craftAndUpgrades) * ValConfig.SkillGainOnCrafts.Value;
                    Logger.LogDebug($"DeathProgression crafting skill crafting: {craftupgradexp}");
                    _craftAndUpgrades = craftAndUpgrade;
                }
                
                // calculate the skill bonus for how long the player has been alive
                float total_xp_from_actions = bosskillxp + killxp + buildxp + treeharvestxp + mineharvestxp + craftupgradexp;
                // Clamp to >= 0: Math.Log(m_timeSinceDeath) is negative in the first second after a
                // respawn, which would otherwise feed a negative value into RaiseSkill and briefly
                // reduce the death skill (and grant negative Almanac/WackyMMO XP).
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

                Logger.LogDebug($"DeathProgression skill bonus from survival (survive time: {__instance.m_timeSinceDeath}) {skillbonus} x {total_xp_from_actions} = {curved_xp}");
                Player.m_localPlayer.RaiseSkill(DeathSkill, curved_xp);
            }
        }
    }
}