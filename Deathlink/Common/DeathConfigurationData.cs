using HarmonyLib;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static Deathlink.Common.DataObjects;

namespace Deathlink.Common
{
    internal static class DeathConfigurationData
    {
        public static readonly Dictionary<string, DeathChoiceLevel> defaultDeathLevels = new Dictionary<string, DeathChoiceLevel>()
        {
            {
                "Vanilla", new DeathChoiceLevel() {
                    DisplayName = "Vanilla",
                    // Where a player lands when the level they had stored no longer exists.
                    Fallback = true,
                    DeathStyle = new DeathProgressionDetails() { itemLossStyle = ItemLossStyle.None, foodLossOnDeath = true, itemSavedStyle = ItemSavedStyle.Tombstone, minSkillLossPercentage = 0.05f, maxSkillLossPercentage = 0.05f },
                    DeathLootModifiers = new Dictionary<string, DeathLootModifier>() { },
                    ResourceModifiers = new Dictionary<string, DeathResourceModifier> { },
                    SkillModifiers = new Dictionary<string, DeathSkillModifier>() { },
                }
            },
            {
                "Rougelike1", new DeathChoiceLevel() {
                    DisplayName = "ShieldBearer",
                    DeathStyle = new DeathProgressionDetails() { itemLossStyle = ItemLossStyle.DeathlinkBased, foodLossUsesDeathlink = true, itemSavedStyle = ItemSavedStyle.Tombstone, minEquipmentKept = 3, maxEquipmentKept = 9, minItemsKept = 3, maxItemsKept = 15, minItemsKeptChoices = 1, maxItemsKeptChoices = 5, minSkillLossPercentage = 0.03f, maxSkillLossPercentage = 0.13f },
                    DeathLootModifiers = new Dictionary<string, DeathLootModifier>() { },
                    ResourceModifiers = new Dictionary<string, DeathResourceModifier> {
                        { "Wood", new DeathResourceModifier() { prefabs = new List<string>() { "Wood", "FineWood", "RoundLog", "YggdrasilWood", "Blackwood" }, BonusModifier = 1.1f, bonusActions = new List<ResourceGainTypes>(){ ResourceGainTypes.Harvesting } } }
                    },
                    SkillModifiers = new Dictionary<string, DeathSkillModifier>() {
                        { "All", new DeathSkillModifier() { BonusModifier = 1.05f, skill = Skills.SkillType.All } }
                    },
                }
            },
            {
                "Rougelike2", new DeathChoiceLevel() {
                    DisplayName = "Raider",
                    DeathStyle = new DeathProgressionDetails() { itemLossStyle = ItemLossStyle.DeathlinkBased, itemSavedStyle = ItemSavedStyle.Tombstone, minEquipmentKept = 2, maxEquipmentKept = 6, minItemsKeptChoices = 1, maxItemsKeptChoices = 3, minSkillLossPercentage = 0.02f, maxSkillLossPercentage = 0.14f },
                    DeathLootModifiers = new Dictionary<string, DeathLootModifier>() { },
                    ResourceModifiers = new Dictionary<string, DeathResourceModifier> {
                        { "Wood", new DeathResourceModifier() { prefabs = new List<string>() { "Wood", "FineWood", "RoundLog", "YggdrasilWood", "Blackwood" }, BonusModifier = 1.2f, bonusActions = new List<ResourceGainTypes>(){ ResourceGainTypes.Harvesting } } },
                        { "Ore", new DeathResourceModifier() { prefabs = new List<string>() { "CopperOre", "TinOre", "IronScrap", "SilverOre", "BlackMetalScrap", "CopperScrap", "FlametalOreNew" }, BonusModifier = 1.2f, bonusActions = new List<ResourceGainTypes>(){ ResourceGainTypes.Harvesting } } }
                    },
                    SkillModifiers = new Dictionary<string, DeathSkillModifier>() {
                        { "All", new DeathSkillModifier() { BonusModifier = 1.1f, skill = Skills.SkillType.All } }
                    },
                }
            },
            {
                "Rougelike3", new DeathChoiceLevel() {
                    DisplayName = "Berserker",
                    DamageTakenModifier = 1.15f,
                    DamageDoneModifier = 1.1f,
                    DeathStyle = new DeathProgressionDetails() { itemLossStyle = ItemLossStyle.DeathlinkBased, itemSavedStyle = ItemSavedStyle.OnCharacter, minEquipmentKept = 0, maxEquipmentKept = 3, minItemsKeptChoices = 0, maxItemsKeptChoices = 2, minSkillLossPercentage = 0.05f, maxSkillLossPercentage = 0.2f },
                    DeathLootModifiers = new Dictionary<string, DeathLootModifier>() {
                        { "AmberPearl", new DeathLootModifier() { chance = 0.05f, prefab = "AmberPearl", bonusActions = new List<ResourceGainTypes>() { ResourceGainTypes.Kills } } }
                    },
                    ResourceModifiers = new Dictionary<string, DeathResourceModifier> {
                        { "Wood", new DeathResourceModifier() { prefabs = new List<string>() { "Wood", "FineWood", "RoundLog", "YggdrasilWood", "Blackwood" }, BonusModifier = 1.5f, bonusActions = new List<ResourceGainTypes>(){ ResourceGainTypes.Harvesting } } },
                        { "Ore", new DeathResourceModifier() { prefabs = new List<string>() { "CopperOre", "TinOre", "IronScrap", "SilverOre", "BlackMetalScrap", "CopperScrap", "FlametalOreNew" }, BonusModifier = 1.5f, bonusActions = new List<ResourceGainTypes>(){ ResourceGainTypes.Harvesting } } }
                    },
                    SkillModifiers = new Dictionary<string, DeathSkillModifier>() {
                        { "All", new DeathSkillModifier() { BonusModifier = 1.2f, skill = Skills.SkillType.All } }
                    },
                }
            },
            {
                "Hardcore", new DeathChoiceLevel() {
                    DisplayName = "Deathbringer",
                    DamageTakenModifier = 1.25f,
                    DamageDoneModifier = 1.15f,
                    DeathStyle = new DeathProgressionDetails() { itemLossStyle = ItemLossStyle.DestroyAll, minSkillLossPercentage = 0.05f, maxSkillLossPercentage = 0.25f },
                    DeathLootModifiers = new Dictionary<string, DeathLootModifier>() {
                        { "AmberPearl", new DeathLootModifier() { chance = 0.05f, prefab = "AmberPearl", bonusActions = new List<ResourceGainTypes>() { ResourceGainTypes.Kills } } },
                        { "SmallHealthPotion", new DeathLootModifier() { chance = 0.01f, prefab = "MeadHealthMinor", bonusActions = new List<ResourceGainTypes>() { ResourceGainTypes.Kills } } }
                    },
                    ResourceModifiers = new Dictionary<string, DeathResourceModifier> {
                        { "Wood", new DeathResourceModifier() { prefabs = new List<string>() { "Wood", "FineWood", "RoundLog", "YggdrasilWood", "Blackwood", "ElderBark" }, BonusModifier = 2.0f, bonusActions = new List<ResourceGainTypes>(){ ResourceGainTypes.Harvesting } } },
                        { "Stone", new DeathResourceModifier() { prefabs = new List<string>() { "Flint", "Stone", "BlackMarble", "Grausten" }, BonusModifier = 2.0f, bonusActions = new List<ResourceGainTypes>(){ ResourceGainTypes.Harvesting } } },
                        { "Ore", new DeathResourceModifier() { prefabs = new List<string>() { "CopperOre", "TinOre", "IronScrap", "SilverOre", "BlackMetalScrap", "CopperScrap", "FlametalOreNew" }, BonusModifier = 2.0f, bonusActions = new List<ResourceGainTypes>(){ ResourceGainTypes.Harvesting } } }
                    },
                    SkillModifiers = new Dictionary<string, DeathSkillModifier>() {
                        { "All", new DeathSkillModifier() { BonusModifier = 1.3f, skill = Skills.SkillType.All } }
                    },
                }
            }
        };

        public static Dictionary<long, DeathConfiguration> playerSettings = new Dictionary<long, DeathConfiguration>() { };

        public static Dictionary<string, DeathChoiceLevel> DeathLevels = defaultDeathLevels;

        public static DeathChoiceLevel playerDeathConfiguration = new DeathChoiceLevel() { DeathStyle = new DeathProgressionDetails() {
            foodLossOnDeath = true,
            foodLossUsesDeathlink = false,
            itemLossStyle = ItemLossStyle.None,
            minItemsKept = 0,
            maxItemsKept = 0,
            minEquipmentKept = 0,
            maxEquipmentKept = 0,
            skillLossOnDeath = true,
            maxSkillLossPercentage = 0.05f,
            minSkillLossPercentage = 0.05f,
            itemSavedStyle = ItemSavedStyle.Tombstone,
            nonSkillCheckedItemAction = NonSkillCheckedItemAction.Tombstone
        }
        };

        // Loading, watching, validating and syncing both yaml files is handled by Common/Config; the
        // registrations live in DeathlinkConfigFiles.cs. What is left here is what to DO with the values.

        /// <summary>
        /// The level a player falls back to when the one they had stored is gone: the configured
        /// DefaultDeathChoice if it resolves, otherwise the level marked Fallback, otherwise the first in
        /// the file. Centralised so file ORDER stops being load-bearing -- previously every fallback path
        /// took DeathLevels.First(), so simply reordering DeathChoices.yaml changed where orphaned
        /// players landed.
        /// </summary>
        public static string ResolveFallbackKey() {
            if (DeathLevels == null || DeathLevels.Count == 0) { return null; }

            string configured = GetValidDefaultChoiceKey();
            if (configured != null) { return configured; }

            foreach (KeyValuePair<string, DeathChoiceLevel> level in DeathLevels) {
                if (level.Value != null && level.Value.Fallback) { return level.Key; }
            }
            return DeathLevels.First().Key;
        }

        public static DeathChoiceLevel ResolveFallbackLevel() {
            string key = ResolveFallbackKey();
            if (key != null && DeathLevels.TryGetValue(key, out DeathChoiceLevel level)) { return level; }
            return defaultDeathLevels["Vanilla"];
        }

        [HarmonyPatch(typeof(Player))]
        public static class SetupPlayerDeathlink
        {
            [HarmonyPostfix]
            [HarmonyPatch(nameof(Player.Load))]
            static void Postfix(Player __instance) {
                CheckAndSetPlayerDeathConfig(__instance);
            }
        }

        // Armed at startup and re-armed whenever the levels or the prefab table are replaced.
        private static bool prefabValidationPending = true;

        /// <summary>
        /// The deferred half of DeathChoices.yaml validation. Prefab names cannot be resolved during
        /// Awake -- ZNetScene does not exist yet -- so the startup pass deliberately skips them and this
        /// runs the check the first time the levels are actually used: when a player loads their
        /// character or picks a Deathlink choice. Cheap to call repeatedly; it does nothing once the
        /// check has run, and nothing yet if the prefab table still is not up.
        /// </summary>
        internal static void EnsurePrefabsValidated() {
            if (prefabValidationPending == false) { return; }
            // Not ready yet. Stays armed, so the next player load or choice retries it.
            if (Utils.PrefabsAvailable() == false) { return; }

            // Cleared before the pass, not after, so a re-entrant call cannot start a second one.
            prefabValidationPending = false;
            YamlConfigManager.RevalidatePrefabDependent();
        }

        /// <summary>
        /// Re-arms the deferred prefab check unconditionally. For a new ZNetScene: the prefab table it
        /// brings is a different one, so whatever was checked against the last table has to be redone.
        /// </summary>
        internal static void ArmPrefabValidation() {
            prefabValidationPending = true;
        }

        /// <summary>
        /// Re-arms the deferred check only when the load that just happened could not check prefab names
        /// for itself -- the startup load, or the server's copy arriving during connect, both of which
        /// run before ZNetScene exists.
        ///
        /// An in-world reload (hand edit, broadcast, in-game edit) validated prefab names for real inside
        /// LoadFrom and has already logged the result, so arming there would only make the next player
        /// load repeat the same report.
        /// </summary>
        internal static void ArmPrefabValidationIfUnchecked() {
            if (Utils.PrefabsAvailable()) { return; }
            prefabValidationPending = true;
        }

        public static void CheckAndSetPlayerDeathConfig(Player player) {
            // The lazy prefab pass. This method is the one point every way of arriving at a resolved
            // death choice funnels through -- the Player.Load postfix, the selection popup, an admin
            // reset and the config apply hook -- so hanging the check here covers all of them once.
            EnsurePrefabsValidated();

            if (ValConfig.UsePrivateKeysForDeathChoice.Value) {
                Logger.LogDebug($"Checking private keys configurations for Deathlink");
                if (!player.PlayerHasUniqueKey(DeathChoiceKey)) {
                    string defaultChoice = GetValidDefaultChoiceKey();
                    if (defaultChoice != null) {
                        Logger.LogInfo($"No stored Deathlink choice, assigning configured default '{defaultChoice}'.");
                        player.AddUniqueKeyValue(DeathChoiceKey, defaultChoice);
                    }
                }
                if (player.PlayerHasUniqueKey(DeathChoiceKey)) {
                    player.TryGetUniqueKeyValue(DeathChoiceKey, out string selectedDeathConfig);
                    if (DeathLevels.ContainsKey(selectedDeathConfig)) {
                        Logger.LogDebug($"Player deathlink configurations set {selectedDeathConfig}");
                        playerDeathConfiguration = DeathLevels[selectedDeathConfig];
                    } else {
                        // Info, not debug: this is what an admin sees after renaming or deleting a level,
                        // and it is the only signal that a player was moved off it.
                        Logger.LogInfo($"Stored death choice '{selectedDeathConfig}' is no longer configured; clearing it for this player.");
                        player.PlayerRemoveUniqueKey(DeathChoiceKey);

                        // Resolved straight through rather than by re-entering this method. The recursion
                        // terminated only because the key had just been removed, which is a fragile thing
                        // to depend on.
                        string replacement = GetValidDefaultChoiceKey();
                        if (replacement != null) {
                            Logger.LogInfo($"Assigning configured default '{replacement}' instead.");
                            player.AddUniqueKeyValue(DeathChoiceKey, replacement);
                            playerDeathConfiguration = DeathLevels[replacement];
                        } else {
                            // No configured default: leave them keyless so the selection popup reappears,
                            // but do not leave the previous level's penalties applied in the meantime.
                            Logger.LogInfo("No default is configured, the selection popup will be shown.");
                            playerDeathConfiguration = ResolveFallbackLevel();
                        }
                    }
                }
            } else {
                CheckYamlConfig();
            }
            // Push the resolved damage multipliers onto the player's networked ZDO so every
            // client can read them when applying combat damage (see DamageModifiers).
            StoreDamageModifiersOnPlayer(player);
        }

        /// <summary>
        /// Persists the local player's damage take/deal multipliers onto their character ZDO.
        /// The player owns their own ZDO, so this replicates to every other client and lets the
        /// machine that owns a hit's target look up the correct multiplier for both the attacker
        /// and the target. Always written (even when 1f) so switching to a choice without a
        /// modifier overwrites any stale value from a previous choice.
        /// </summary>
        public static void StoreDamageModifiersOnPlayer(Player player) {
            if (player == null) { return; }
            ZNetView nview = player.m_nview;
            if (nview == null || !nview.IsValid()) { return; }
            ZDO zdo = nview.GetZDO();
            if (zdo == null) { return; }
            zdo.Set(DamageTakenModifierKey, playerDeathConfiguration.DamageTakenModifier);
            zdo.Set(DamageDoneModifierKey, playerDeathConfiguration.DamageDoneModifier);
            Logger.LogDebug($"Stored damage modifiers on player ZDO: taken {playerDeathConfiguration.DamageTakenModifier}, done {playerDeathConfiguration.DamageDoneModifier}");
        }

        /// <summary>
        /// Clears the local player's stored death choice (and change counter) and re-applies the
        /// resolved configuration immediately so an admin reset takes effect without a relog. Reverts
        /// the in-memory config to a clean baseline first so a reset with no configured default falls
        /// back to Vanilla instead of leaving the previous choice's penalties/damage modifiers active.
        /// When no default is configured the player is left without a choice key, so the selection
        /// popup re-appears on the next inventory open.
        /// </summary>
        public static void ResetLocalPlayerChoice() {
            Player player = Player.m_localPlayer;
            if (player == null) {
                Logger.LogWarning("Cannot reset death choice, local player is not set.");
                return;
            }
            player.PlayerRemoveUniqueKey(DeathChoiceKey);
            player.PlayerRemoveUniqueKey(DeathChoiceChangesKey);
            // Drop the previous choice so CheckAndSetPlayerDeathConfig can't re-store stale modifiers.
            playerDeathConfiguration = ResolveFallbackLevel();
            // Reapplies any configured default and rewrites the networked damage modifiers.
            CheckAndSetPlayerDeathConfig(player);
            WritePlayerChoices();
            Logger.LogInfo("Local player's death choice has been reset.");
        }

        internal static void CheckYamlConfig() {
            if (Player.m_localPlayer == null) {
                Logger.LogWarning("Local player not defined, skipping setup.");
                Logger.LogDebug($"Using fallback death level {ResolveFallbackKey()}");
                playerDeathConfiguration = ResolveFallbackLevel();
                return;
            }
            long playerID = Player.m_localPlayer.GetPlayerID();
            Logger.LogDebug($"Setting up Deathlink player configuration with id {playerID}");
            Logger.LogDebug($"Checking stored configurations for {playerID} {string.Join(",", playerSettings.Keys)}");
            if (playerSettings.ContainsKey(playerID)) {
                string selectedDeathConfig = playerSettings[playerID].DeathChoiceLevel;
                if (DeathLevels.ContainsKey(selectedDeathConfig)) {
                    Logger.LogDebug($"Player deathlink configurations set {selectedDeathConfig}");
                    playerDeathConfiguration = DeathLevels[selectedDeathConfig];
                } else {
                    Logger.LogInfo($"Stored death choice '{selectedDeathConfig}' is no longer configured, using fallback '{ResolveFallbackKey()}'.");
                    playerDeathConfiguration = ResolveFallbackLevel();
                }
            } else {
                string defaultChoice = GetValidDefaultChoiceKey();
                if (defaultChoice != null) {
                    Logger.LogInfo($"No stored Deathlink choice for {playerID}, assigning configured default '{defaultChoice}'.");
                    playerSettings.Add(playerID, new DeathConfiguration() { DeathChoiceLevel = defaultChoice });
                    playerDeathConfiguration = DeathLevels[defaultChoice];
                    WritePlayerChoices();
                }
            }
        }

        // Goes through the config manager rather than File.WriteAllText so the documented header block at
        // the top of CharacterSettings.yaml survives the write.
        public static void WritePlayerChoices()
        {
            YamlConfigManager.WriteCurrentToDisk(YamlConfigManager.CharacterSettings);
        }

        /// <summary>
        /// Returns the configured default death choice key if it is set and matches a known
        /// death level, otherwise null (meaning the selection popup should be used).
        /// </summary>
        public static string GetValidDefaultChoiceKey()
        {
            string configured = ValConfig.DefaultDeathChoice.Value;
            if (string.IsNullOrEmpty(configured)) { return null; }
            if (DeathLevels.ContainsKey(configured)) { return configured; }
            Logger.LogWarning($"Configured DefaultDeathChoice '{configured}' is not a known death choice, the selection popup will be used instead.");
            return null;
        }

        /// <summary>
        /// How many times the player has already changed their death choice from the compendium.
        /// </summary>
        public static int GetPlayerChangesUsed(Player player)
        {
            if (player == null) { return 0; }
            if (ValConfig.UsePrivateKeysForDeathChoice.Value) {
                if (player.TryGetUniqueKeyValue(DeathChoiceChangesKey, out string raw) && int.TryParse(raw, out int used)) {
                    return used;
                }
                return 0;
            }
            long playerID = player.GetPlayerID();
            if (playerSettings.ContainsKey(playerID)) { return playerSettings[playerID].ChangesUsed; }
            return 0;
        }

        /// <summary>
        /// Records that the player has used one of their allowed death choice changes.
        /// </summary>
        public static void IncrementPlayerChangesUsed(Player player)
        {
            if (player == null) { return; }
            int used = GetPlayerChangesUsed(player) + 1;
            if (ValConfig.UsePrivateKeysForDeathChoice.Value) {
                player.PlayerRemoveUniqueKey(DeathChoiceChangesKey);
                player.AddUniqueKeyValue(DeathChoiceChangesKey, used.ToString());
            } else {
                long playerID = player.GetPlayerID();
                if (playerSettings.ContainsKey(playerID)) {
                    playerSettings[playerID].ChangesUsed = used;
                } else {
                    playerSettings.Add(playerID, new DeathConfiguration() { ChangesUsed = used });
                }
                WritePlayerChoices();
            }
        }

        /// <summary>
        /// True when the player still has at least one death choice change available.
        /// </summary>
        public static bool PlayerCanChangeChoice(Player player)
        {
            if (player == null) { return false; }
            return GetPlayerChangesUsed(player) < ValConfig.AllowedDeathChoiceChanges.Value;
        }

        // Apply hook for CharacterSettings.yaml. Additive on purpose: this file is save data that the
        // server owns, and a client must not have entries it already knows about overwritten by a
        // partial payload.
        public static void MergePlayerSettings(Dictionary<long, DeathConfiguration> incoming) {
            if (incoming == null) { return; }
            foreach (var kvp in incoming) {
                if (playerSettings.ContainsKey(kvp.Key)) { continue; }
                playerSettings.Add(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// One-shot migration for files written by 0.10.x or earlier.
        ///
        /// Those versions always wrote "skillInfluence: true" into every resource and skill modifier --
        /// the field's initializer was true and it carried no [DefaultValue], so the serializer emitted
        /// it whether or not anyone had asked for it. The field was also never read, so every one of
        /// those bonuses actually applied in full. Now that skillInfluence works, leaving those values
        /// in place would silently weaken every existing config, so they are cleared once on upgrade.
        ///
        /// The old misspelling "bonusModifer" is the marker: only a pre-0.11 file can contain it. The
        /// rewrite that follows this migration emits the corrected spelling, which erases the marker --
        /// so this runs exactly once, and an admin who deliberately turns skillInfluence on afterwards
        /// keeps it.
        /// </summary>
        public static bool MigrateLegacySkillInfluence(Dictionary<string, DeathChoiceLevel> levels) {
            if (levels == null || WrittenBeforeSkillInfluenceWorked(levels) == false) { return false; }

            int cleared = 0;
            foreach (DeathChoiceLevel level in levels.Values) {
                if (level == null) { continue; }
                if (level.ResourceModifiers != null) {
                    foreach (DeathResourceModifier modifier in level.ResourceModifiers.Values) {
                        if (modifier == null || modifier.skillInfluence == false) { continue; }
                        modifier.skillInfluence = false;
                        cleared++;
                    }
                }
                if (level.SkillModifiers == null) { continue; }
                foreach (DeathSkillModifier modifier in level.SkillModifiers.Values) {
                    if (modifier == null || modifier.skillInfluence == false) { continue; }
                    modifier.skillInfluence = false;
                    cleared++;
                }
            }

            if (cleared > 0) {
                Logger.LogInfo($"DeathChoices.yaml predates Deathlink 0.11. SkillInfluence was written into " +
                    $"every modifier by older versions but never applied, so it has been turned off on {cleared} " +
                    "modifier(s) to keep those bonuses working exactly as they did. Turn it back on for any bonus " +
                    "you want to scale with Deathlink skill.");
            }

            // True even when nothing was cleared, so the file is still rewritten in the current format.
            // That is what removes the legacy spelling and stops this migration running again.
            return true;
        }

        private static bool WrittenBeforeSkillInfluenceWorked(Dictionary<string, DeathChoiceLevel> levels) {
            foreach (DeathChoiceLevel level in levels.Values) {
                if (level == null) { continue; }
                if (level.ResourceModifiers != null) {
                    foreach (DeathResourceModifier modifier in level.ResourceModifiers.Values) {
                        if (modifier != null && modifier.ReadWithLegacySpelling) { return true; }
                    }
                }
                if (level.SkillModifiers == null) { continue; }
                foreach (DeathSkillModifier modifier in level.SkillModifiers.Values) {
                    if (modifier != null && modifier.ReadWithLegacySpelling) { return true; }
                }
            }
            return false;
        }

        /// <summary>
        /// Validate hook for DeathChoices.yaml. <paramref name="previous"/> is null on the first load and
        /// the currently-live levels on every load after that, which is what lets this report levels that
        /// were REMOVED -- the edit whose consequences an admin is least likely to predict.
        /// </summary>
        public static ValidationReport ValidateDeathLevels(Dictionary<string, DeathChoiceLevel> next, Dictionary<string, DeathChoiceLevel> previous) {
            ValidationReport report = new ValidationReport();

            if (next == null || next.Count == 0) {
                return report.Error("it defines no death levels, so there would be nothing for a player to choose");
            }

            int fallbacks = 0;
            foreach (KeyValuePair<string, DeathChoiceLevel> level in next) {
                DeathChoiceLevel value = level.Value;
                if (value == null) {
                    report.Error($"level '{level.Key}' has no settings under it");
                    continue;
                }
                if (value.Fallback) { fallbacks++; }
                if (value.DeathStyle == null) {
                    report.Error($"level '{level.Key}' has an empty DeathStyle block");
                    continue;
                }

                if (value.DeathSkillRate <= 0f) {
                    report.Warn($"level '{level.Key}' has DeathSkillRate {value.DeathSkillRate}; Deathlink skill will never increase on it.");
                } else if (value.DeathSkillRate > 10f) {
                    report.Warn($"level '{level.Key}' has DeathSkillRate {value.DeathSkillRate}, which will max the Deathlink skill very quickly.");
                }

                if (value.DeathStyle.minSkillLossPercentage > value.DeathStyle.maxSkillLossPercentage) {
                    report.Warn($"level '{level.Key}' has MinSkillLossPercentage above MaxSkillLossPercentage; " +
                        "skill loss will get WORSE as Deathlink skill increases.");
                }

                ValidateResourceModifiers(level.Key, value, report);
                ValidateLootModifiers(level.Key, value, report);
            }

            if (fallbacks == 0) {
                report.Warn($"no level is marked 'Fallback: true'. '{next.First().Key}' is being used because it is " +
                    "first in the file, which means reordering the file would change it. Mark one level as the fallback.");
            } else if (fallbacks > 1) {
                report.Warn($"{fallbacks} levels are marked 'Fallback: true'. The first one in the file wins.");
            }

            string configuredDefault = ValConfig.DefaultDeathChoice != null ? ValConfig.DefaultDeathChoice.Value : null;
            if (string.IsNullOrEmpty(configuredDefault) == false && next.ContainsKey(configuredDefault) == false) {
                report.Warn($"the DefaultDeathChoice setting names '{configuredDefault}', which is not a level in this " +
                    "file. New players will be shown the selection popup instead." +
                    ConfigValidation.SuggestKey(configuredDefault, next.Keys));
            }

            if (previous != null) {
                foreach (string key in previous.Keys) {
                    if (next.ContainsKey(key)) { continue; }
                    report.Warn($"level '{key}' was removed. Players who had it selected will be moved to " +
                        $"'{(next.ContainsKey(ResolveFallbackKey() ?? "") ? ResolveFallbackKey() : next.First().Key)}' " +
                        "or re-prompted the next time they load.");
                }
            }

            return report;
        }

        private static void ValidateResourceModifiers(string levelKey, DeathChoiceLevel level, ValidationReport report) {
            if (level.ResourceModifiers == null) { return; }

            // Which entry first claimed each prefab, so an overlap can name both sides. A prefab listed
            // twice in one level is not fatal -- first entry wins -- but only one of the two bonuses will
            // ever apply, which is almost never what was intended.
            Dictionary<string, string> claimedBy = new Dictionary<string, string>();

            foreach (KeyValuePair<string, DeathResourceModifier> entry in level.ResourceModifiers) {
                if (entry.Value == null || entry.Value.prefabs == null) { continue; }
                foreach (string prefab in entry.Value.prefabs) {
                    if (claimedBy.TryGetValue(prefab, out string owner)) {
                        report.Warn($"level '{levelKey}' lists prefab '{prefab}' in both '{owner}' and '{entry.Key}'. " +
                            $"Only '{owner}' will apply.");
                        continue;
                    }
                    claimedBy[prefab] = entry.Key;
                    WarnIfUnknownPrefab(levelKey, $"ResourceModifiers.{entry.Key}", prefab, report);
                }
            }
        }

        private static void ValidateLootModifiers(string levelKey, DeathChoiceLevel level, ValidationReport report) {
            if (level.DeathLootModifiers == null) { return; }
            foreach (KeyValuePair<string, DeathLootModifier> entry in level.DeathLootModifiers) {
                if (entry.Value == null) { continue; }
                if (string.IsNullOrEmpty(entry.Value.prefab)) {
                    report.Warn($"level '{levelKey}' loot entry '{entry.Key}' has no Prefab set, it will be skipped.");
                    continue;
                }
                WarnIfUnknownPrefab(levelKey, $"DeathLootModifiers.{entry.Key}", entry.Value.prefab, report);
                if (entry.Value.chance <= 0f || entry.Value.chance > 1f) {
                    report.Warn($"level '{levelKey}' loot entry '{entry.Key}' has Chance {entry.Value.chance}; " +
                        "it is a 0-1 probability, so this will never drop or always drops.");
                }
            }
        }

        // Silent until the prefab table exists. The startup load runs from Awake, long before ZNetScene
        // and ObjectDB, so every name in the file would come back unresolved; EnsurePrefabsValidated
        // re-runs this pass once the game can actually answer. See Utils.PrefabsAvailable for why the
        // obvious `PrefabManager.Instance == null` test does not work.
        private static void WarnIfUnknownPrefab(string levelKey, string where, string prefab, ValidationReport report) {
            if (Utils.PrefabsAvailable() == false) { return; }
            if (PrefabManager.Instance.GetPrefab(prefab) != null) { return; }
            report.Warn($"level '{levelKey}' {where} names prefab '{prefab}', which does not exist. It will be skipped.");
        }
    }
}
