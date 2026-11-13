using HarmonyLib;
using Jotunn.Managers;
using System.Collections;
using System.Collections.Generic;
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
                // The main menu's character preview loads a Player too.
                if (__instance != Player.m_localPlayer) { return; }
                CheckAndSetPlayerDeathConfig(__instance);
            }
        }

        private static bool prefabValidationPending = true;

        // Prefab names cannot be resolved during Awake, before ZNetScene exists, so the startup
        // validation skips them and this runs that check the first time the levels are used.
        internal static void EnsurePrefabsValidated() {
            if (prefabValidationPending == false) { return; }
            if (Utils.PrefabsAvailable() == false) { return; }

            // Cleared before the pass, not after, so a re-entrant call cannot start a second one.
            prefabValidationPending = false;
            YamlConfigManager.RevalidatePrefabDependent();
        }

        internal static void ArmPrefabValidation() {
            prefabValidationPending = true;
        }

        // A load made once prefabs exist has already checked their names and logged the result, so
        // only a load made before ZNetScene exists needs the deferred pass.
        internal static void ArmPrefabValidationIfUnchecked() {
            if (Utils.PrefabsAvailable()) { return; }
            prefabValidationPending = true;
        }

        public static void CheckAndSetPlayerDeathConfig(Player player) {
            // Every way of arriving at a resolved choice comes through here, so the deferred check runs here.
            EnsurePrefabsValidated();

            if (ValConfig.UsePrivateKeysForDeathChoice.Value) {
                Logger.LogDebug($"Checking private keys configurations for Deathlink");
                if (!player.TryGetUniqueKeyValue(DeathChoiceKey, out _)) {
                    string defaultChoice = GetValidDefaultChoiceKey();
                    if (defaultChoice != null) {
                        Logger.LogInfo($"No stored Deathlink choice, assigning configured default '{defaultChoice}'.");
                        player.AddUniqueKeyValue(DeathChoiceKey, defaultChoice);
                    }
                }
                if (!player.TryGetUniqueKeyValue(DeathChoiceKey, out _)) {
                    // Not chosen yet. Without this the level of whichever character was loaded before stays in force.
                    playerDeathConfiguration = ResolveFallbackLevel();
                } else {
                    player.TryGetUniqueKeyValue(DeathChoiceKey, out string selectedDeathConfig);
                    if (DeathLevels.ContainsKey(selectedDeathConfig)) {
                        Logger.LogDebug($"Player deathlink configurations set {selectedDeathConfig}");
                        playerDeathConfiguration = DeathLevels[selectedDeathConfig];
                    } else {
                        // Info, not debug: it is an admin's only sign that a player was moved off a removed level.
                        Logger.LogInfo($"Stored death choice '{selectedDeathConfig}' is no longer configured; clearing it for this player.");
                        player.RemoveUniqueKeyValue(DeathChoiceKey);

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
            StoreDamageModifiersOnPlayer(player);
        }

        // On the player's own ZDO so whichever machine processes a hit can read both sides' multipliers.
        // Always written, even at 1, so a previous choice's value does not linger.
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

        // Takes effect without a relog. With no configured default the player is left without a choice,
        // so the selection popup comes back.
        public static void ResetLocalPlayerChoice() {
            Player player = Player.m_localPlayer;
            if (player == null) {
                Logger.LogWarning("Cannot reset death choice, local player is not set.");
                return;
            }
            player.RemoveUniqueKeyValue(DeathChoiceKey);
            player.RemoveUniqueKeyValue(DeathChoiceChangesKey);
            playerSettings.Remove(player.GetPlayerID());
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
                } else {
                    playerDeathConfiguration = ResolveFallbackLevel();
                }
            }
        }

        // Yaml mode only: private keys travel with the character. The server owns CharacterSettings.yaml,
        // so a client reports its own entry instead of writing the file.
        public static void WritePlayerChoices()
        {
            if (ValConfig.UsePrivateKeysForDeathChoice.Value) { return; }
            if (ZNet.instance != null && ZNet.instance.IsServer() == false) {
                ReportLocalChoiceToServer();
                return;
            }
            // Through the config manager so the header block at the top of the file survives the write.
            YamlConfigManager.WriteCurrentToDisk(YamlConfigManager.CharacterSettings);
        }

        private const string ChoiceReportRpc = Deathlink.PluginGUID + ".ChoiceReport";

        private static void ReportLocalChoiceToServer()
        {
            if (Player.m_localPlayer == null) { return; }
            ZRpc server = ZNet.instance.GetServerRPC();
            if (server == null) { return; }
            playerSettings.TryGetValue(Player.m_localPlayer.GetPlayerID(), out DeathConfiguration entry);
            ZPackage package = new ZPackage();
            package.Write(entry?.DeathChoiceLevel ?? "");
            package.Write(entry?.ChangesUsed ?? 0);
            server.Invoke(ChoiceReportRpc, package);
        }

        // Per-connection rather than routed, so the server identifies the reporter by its socket, not by
        // anything the client wrote.
        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        public static class RegisterChoiceReportRpc
        {
            public static void Postfix(ZNet __instance, ZNetPeer peer)
            {
                if (__instance.IsServer()) { peer.m_rpc.Register<ZPackage>(ChoiceReportRpc, OnServerReceiveChoice); }
            }
        }

        private static void OnServerReceiveChoice(ZRpc rpc, ZPackage package)
        {
            if (ValConfig.UsePrivateKeysForDeathChoice.Value) { return; }
            ZNetPeer peer = ZNet.instance.GetPeer(rpc);
            if (peer == null) { return; }
            string level = package.ReadString();
            int changesUsed = package.ReadInt();
            ZNet.instance.StartCoroutine(SaveReportedChoice(peer, level, changesUsed));
        }

        // A first choice is reported from Player.Load, before the game tells the server which character the
        // connection plays (Game.SpawnPlayer sends the character id after loading), so wait for that.
        private static IEnumerator SaveReportedChoice(ZNetPeer peer, string level, int changesUsed)
        {
            long playerID = 0L;
            for (int attempt = 0; attempt < 60 && playerID == 0L; attempt++) {
                if (ZNet.instance == null || !ZNet.instance.GetPeers().Contains(peer)) { yield break; }
                playerID = Utils.PlayerIdOfPeer(peer);
                if (playerID == 0L) { yield return new WaitForSeconds(0.5f); }
            }
            if (playerID == 0L) {
                Logger.LogWarning($"Could not tell which character {peer.m_playerName} plays, so their death choice was not saved.");
                yield break;
            }
            if (string.IsNullOrEmpty(level) && changesUsed == 0) {
                playerSettings.Remove(playerID);
            } else {
                playerSettings[playerID] = new DeathConfiguration() { DeathChoiceLevel = level, ChangesUsed = changesUsed };
            }
            YamlConfigManager.WriteCurrentToDisk(YamlConfigManager.CharacterSettings);
            Logger.LogDebug($"Saved the death choice of player {playerID}: '{level}', {changesUsed} changes used.");
        }

        // A client holds the server's CharacterSettings while connected; put its own file back on leaving.
        [HarmonyPatch(typeof(Game), "Shutdown")]
        public static class RestoreLocalCharacterSettings
        {
            public static void Postfix()
            {
                if (ZNet.instance == null || ZNet.instance.IsServer()) { return; }
                YamlConfigManager.ReloadFromDisk(YamlConfigManager.CharacterSettings, broadcast: false);
            }
        }

        public static string GetValidDefaultChoiceKey()
        {
            string configured = ValConfig.DefaultDeathChoice.Value;
            if (string.IsNullOrEmpty(configured)) { return null; }
            if (DeathLevels.ContainsKey(configured)) { return configured; }
            Logger.LogWarning($"Configured DefaultDeathChoice '{configured}' is not a known death choice, the selection popup will be used instead.");
            return null;
        }

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

        public static void IncrementPlayerChangesUsed(Player player)
        {
            if (player == null) { return; }
            int used = GetPlayerChangesUsed(player) + 1;
            if (ValConfig.UsePrivateKeysForDeathChoice.Value) {
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

        public static bool PlayerCanChangeChoice(Player player)
        {
            if (player == null) { return false; }
            return GetPlayerChangesUsed(player) < ValConfig.AllowedDeathChoiceChanges.Value;
        }

        // playerSettings becomes the file's own value, so writing the file saves exactly what is in memory.
        public static void ApplyPlayerSettings(Dictionary<long, DeathConfiguration> incoming) {
            playerSettings = incoming ?? new Dictionary<long, DeathConfiguration>();
        }

        // 0.10.x and earlier wrote skillInfluence: true into every modifier but never read it, so those
        // flags are cleared once on upgrade to keep the bonuses applying in full. Only such a file has the
        // old "bonusModifer" spelling, and the rewrite that follows removes it.
        public static bool MigrateLegacySkillInfluence(Dictionary<string, DeathChoiceLevel> levels) {
            if (levels == null) { return false; }
            List<DeathResourceModifier> resourceModifiers = levels.Values
                .Where(level => level?.ResourceModifiers != null)
                .SelectMany(level => level.ResourceModifiers.Values)
                .Where(modifier => modifier != null).ToList();
            List<DeathSkillModifier> skillModifiers = levels.Values
                .Where(level => level?.SkillModifiers != null)
                .SelectMany(level => level.SkillModifiers.Values)
                .Where(modifier => modifier != null).ToList();
            if (resourceModifiers.Any(modifier => modifier.ReadWithLegacySpelling) == false
                && skillModifiers.Any(modifier => modifier.ReadWithLegacySpelling) == false) {
                return false;
            }

            int cleared = 0;
            foreach (DeathResourceModifier modifier in resourceModifiers) {
                if (modifier.skillInfluence == false) { continue; }
                modifier.skillInfluence = false;
                cleared++;
            }
            foreach (DeathSkillModifier modifier in skillModifiers) {
                if (modifier.skillInfluence == false) { continue; }
                modifier.skillInfluence = false;
                cleared++;
            }

            if (cleared > 0) {
                Logger.LogInfo($"DeathChoices.yaml predates Deathlink 0.11. SkillInfluence was written into " +
                    $"every modifier by older versions but never applied, so it has been turned off on {cleared} " +
                    "modifier(s) to keep those bonuses working exactly as they did. Turn it back on for any bonus " +
                    "you want to scale with Deathlink skill.");
            }

            // True even when nothing was cleared, so the rewrite still removes the legacy spelling.
            return true;
        }

        // previous is null on the first load and the live levels after that, which is how removed levels are reported.
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
                if (level.Key.Any(char.IsWhiteSpace)) {
                    // The game stores a character's choice as "key value" and splits it on the first space.
                    report.Warn($"level '{level.Key}' has a space in its name, so a character cannot keep it as " +
                        "their choice and will be asked to choose again. Rename it without spaces.");
                }
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

                if (value.DeathStyle.itemLossStyle == ItemLossStyle.DestroyAll
                    && value.DeathStyle.equipmentSavedStyle != EquipmentSavedStyle.SameAsItems) {
                    report.Warn($"level '{level.Key}' sets EquipmentSavedStyle, but ItemLossStyle is DestroyAll, " +
                        "so no equipment is saved and it has no effect.");
                }
                if (value.DeathStyle.itemLossStyle == ItemLossStyle.ChooseEquipment) {
                    ValidateChooseEquipment(level.Key, value.DeathStyle, report);
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

        // ItemSavedStyle and EnableItemSavingChoices are ignored by ChooseEquipment too, but their zero
        // values are indistinguishable from "not set", so only EquipmentSavedStyle can be caught.
        private static void ValidateChooseEquipment(string levelKey, DeathProgressionDetails style, ValidationReport report) {
            if (style.equipmentSavedStyle != EquipmentSavedStyle.SameAsItems) {
                report.Warn($"level '{levelKey}' sets EquipmentSavedStyle, but ItemLossStyle is ChooseEquipment, which always " +
                    "leaves unpicked equipment in the tombstone and gives picked equipment back, so it has no effect.");
            }
            if (style.minEquipmentKept <= 0 && style.maxEquipmentKept <= 0) {
                report.Warn($"level '{levelKey}' is ChooseEquipment with MinEquipmentKept and MaxEquipmentKept both 0, so " +
                    "the player never gets to take anything back and everything stays in the tombstone.");
            } else if (style.minEquipmentKept > style.maxEquipmentKept) {
                report.Warn($"level '{levelKey}' has MinEquipmentKept above MaxEquipmentKept; the player will get to take " +
                    "back FEWER pieces as Deathlink skill increases.");
            }
        }

        private static void ValidateResourceModifiers(string levelKey, DeathChoiceLevel level, ValidationReport report) {
            if (level.ResourceModifiers == null) { return; }

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

        // Silent until the prefab table exists; EnsurePrefabsValidated re-runs the pass once it does.
        private static void WarnIfUnknownPrefab(string levelKey, string where, string prefab, ValidationReport report) {
            if (Utils.PrefabsAvailable() == false) { return; }
            if (PrefabManager.Instance.GetPrefab(prefab) != null) { return; }
            report.Warn($"level '{levelKey}' {where} names prefab '{prefab}', which does not exist. It will be skipped.");
        }
    }
}
