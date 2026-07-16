using HarmonyLib;
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
                        { "Wood", new DeathResourceModifier() { prefabs = new List<string>() { "Wood", "FineWood", "RoundLog", "YggdrasilWood", "Blackwood" }, bonusModifer = 1.1f, bonusActions = new List<ResourceGainTypes>(){ ResourceGainTypes.Harvesting } } }
                    },
                    SkillModifiers = new Dictionary<string, DeathSkillModifier>() {
                        { "All", new DeathSkillModifier() { bonusModifer = 1.05f, skill = Skills.SkillType.All } }
                    },
                }
            },
            {
                "Rougelike2", new DeathChoiceLevel() {
                    DisplayName = "Raider",
                    DeathStyle = new DeathProgressionDetails() { itemLossStyle = ItemLossStyle.DeathlinkBased, itemSavedStyle = ItemSavedStyle.Tombstone, minEquipmentKept = 2, maxEquipmentKept = 6, minItemsKeptChoices = 1, maxItemsKeptChoices = 3, minSkillLossPercentage = 0.02f, maxSkillLossPercentage = 0.14f },
                    DeathLootModifiers = new Dictionary<string, DeathLootModifier>() { },
                    ResourceModifiers = new Dictionary<string, DeathResourceModifier> {
                        { "Wood", new DeathResourceModifier() { prefabs = new List<string>() { "Wood", "FineWood", "RoundLog", "YggdrasilWood", "Blackwood" }, bonusModifer = 1.2f, bonusActions = new List<ResourceGainTypes>(){ ResourceGainTypes.Harvesting } } },
                        { "Ore", new DeathResourceModifier() { prefabs = new List<string>() { "CopperOre", "TinOre", "IronScrap", "SilverOre", "BlackMetalScrap", "CopperScrap", "FlametalOreNew" }, bonusModifer = 1.2f, bonusActions = new List<ResourceGainTypes>(){ ResourceGainTypes.Harvesting } } }
                    },
                    SkillModifiers = new Dictionary<string, DeathSkillModifier>() {
                        { "All", new DeathSkillModifier() { bonusModifer = 1.1f, skill = Skills.SkillType.All } }
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
                        { "Wood", new DeathResourceModifier() { prefabs = new List<string>() { "Wood", "FineWood", "RoundLog", "YggdrasilWood", "Blackwood" }, bonusModifer = 1.5f, bonusActions = new List<ResourceGainTypes>(){ ResourceGainTypes.Harvesting } } },
                        { "Ore", new DeathResourceModifier() { prefabs = new List<string>() { "CopperOre", "TinOre", "IronScrap", "SilverOre", "BlackMetalScrap", "CopperScrap", "FlametalOreNew" }, bonusModifer = 1.5f, bonusActions = new List<ResourceGainTypes>(){ ResourceGainTypes.Harvesting } } }
                    },
                    SkillModifiers = new Dictionary<string, DeathSkillModifier>() {
                        { "All", new DeathSkillModifier() { bonusModifer = 1.2f, skill = Skills.SkillType.All } }
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
                        { "Wood", new DeathResourceModifier() { prefabs = new List<string>() { "Wood", "FineWood", "RoundLog", "YggdrasilWood", "Blackwood", "ElderBark" }, bonusModifer = 2.0f, bonusActions = new List<ResourceGainTypes>(){ ResourceGainTypes.Harvesting } } },
                        { "Stone", new DeathResourceModifier() { prefabs = new List<string>() { "Flint", "Stone", "BlackMarble", "Grausten" }, bonusModifer = 2.0f, bonusActions = new List<ResourceGainTypes>(){ ResourceGainTypes.Harvesting } } },
                        { "Ore", new DeathResourceModifier() { prefabs = new List<string>() { "CopperOre", "TinOre", "IronScrap", "SilverOre", "BlackMetalScrap", "CopperScrap", "FlametalOreNew" }, bonusModifer = 2.0f, bonusActions = new List<ResourceGainTypes>(){ ResourceGainTypes.Harvesting } } }
                    },
                    SkillModifiers = new Dictionary<string, DeathSkillModifier>() {
                        { "All", new DeathSkillModifier() { bonusModifer = 1.3f, skill = Skills.SkillType.All } }
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

        internal static void Init() {
            try {
                UpdateDeathLevelsConfig(File.ReadAllText(ValConfig.deathChoicesPath));
            } catch (Exception e) {
                Jotunn.Logger.LogWarning($"There was an error updating the Death choice Level values, defaults will be used. Exception: {e}");
            }

            try {
                UpdatePlayerConfigSettings(File.ReadAllText(ValConfig.playerSettingsPath));
            } catch (Exception e) {
                Jotunn.Logger.LogWarning($"There was an error updating the player choice configs, defaults will be used. Exception: {e}");
            }
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

        public static void CheckAndSetPlayerDeathConfig(Player player) {
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
                        Logger.LogDebug("Player preference setting is not an available config, removing player choice.");
                        player.PlayerRemoveUniqueKey(DeathChoiceKey);
                        // restart the check
                        CheckAndSetPlayerDeathConfig(player);
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
            playerDeathConfiguration = DeathLevels.First().Value;
            // Reapplies any configured default and rewrites the networked damage modifiers.
            CheckAndSetPlayerDeathConfig(player);
            WritePlayerChoices();
            Logger.LogInfo("Local player's death choice has been reset.");
        }

        internal static void CheckYamlConfig() {
            if (Player.m_localPlayer == null) {
                Logger.LogWarning("Local player not defined, skipping setup.");
                DeathLevels = defaultDeathLevels;
                Logger.LogDebug($"Player preference setting is not an available config, using fallback {DeathLevels.First().Key}");
                playerDeathConfiguration = DeathLevels.First().Value;
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
                    Logger.LogDebug("Player preference setting is not an available config, using fallback");
                    playerDeathConfiguration = DeathLevels.First().Value;
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

        public static string PlayerSettingsDefaultConfig()
        {
            return DataObjects.yamlserializer.Serialize(playerSettings);
        }

        public static string DeathLevelsYamlDefaultConfig()
        {
            return DataObjects.yamlserializer.Serialize(DeathLevels);
        }

        public static void WriteDeathChoices()
        {
            File.WriteAllText(ValConfig.deathChoicesPath, yamlserializer.Serialize(DeathLevels));
        }

        public static void WritePlayerChoices()
        {
            File.WriteAllText(ValConfig.playerSettingsPath, yamlserializer.Serialize(playerSettings));
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

        public static void UpdateDeathLevelsConfig(string rawyaml) {
            var parsed = yamldeserializer.Deserialize<Dictionary<string, DeathChoiceLevel>>(rawyaml);
            // Deserialize returns null for an empty/all-comment file (it does not throw, so the
            // caller's try/catch never sees it). An empty dictionary is just as unusable since the
            // resolver relies on DeathLevels.First(). Fall back to the built-in defaults in both cases.
            if (parsed == null || parsed.Count == 0) {
                Logger.LogWarning("DeathChoices config is empty or could not be parsed, using built-in defaults.");
                DeathLevels = defaultDeathLevels;
                return;
            }
            DeathLevels = parsed;
        }

        public static void UpdatePlayerConfigSettings(string rawyaml) {
            var added_cfgs = yamldeserializer.Deserialize<Dictionary<long, DeathConfiguration>>(rawyaml);

            foreach (var kvp in added_cfgs) {
                if (playerSettings.ContainsKey(kvp.Key)) { continue; }
                playerSettings.Add(kvp.Key, kvp.Value);
            }
        }
    }
}
