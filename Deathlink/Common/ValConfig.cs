using BepInEx;
using BepInEx.Configuration;
using Deathlink.Death;
using Jotunn;
using Jotunn.Entities;
using Jotunn.Managers;
using System;
using System.Collections;
using System.IO;
using static ZNet;

namespace Deathlink.Common;

public class ValConfig
{
    public static ConfigFile cfg;
    public static ConfigEntry<bool> EnableDebugMode;
    public static ConfigEntry<string> ItemsNotSkillChecked;
    public static ConfigEntry<float> SkillGainOnKills;
    public static ConfigEntry<float> SkillGainOnBossKills;
    public static ConfigEntry<float> SkillGainOnCrafts;
    public static ConfigEntry<float> SkillGainOnResourceGathering;
    public static ConfigEntry<float> SkillGainOnBuilding;
    public static ConfigEntry<bool> ShowDeathMapMarker;
    public static ConfigEntry<bool> UsePrivateKeysForDeathChoice;
    public static ConfigEntry<string> DefaultDeathChoice;
    public static ConfigEntry<int> AllowedDeathChoiceChanges;
    //public static ConfigEntry<bool> EffectRemovalOnDeath;
    public static ConfigEntry<bool> EnableAlmanacClassesXPLossOnDeath;
    public static ConfigEntry<float> AlmanacClassesXPLossScale;
    public static ConfigEntry<float> AlmanacClassesXPGainScale;
    public static ConfigEntry<bool> EnableWackyMMOXPLossOnDeath;
    public static ConfigEntry<float> WackyMMOXPLossScale;
    public static ConfigEntry<float> WackyMMOXPGainScale;
    public static ConfigEntry<bool> EnableLeaderboard;
    public static ConfigEntry<float> LeaderboardSyncInterval;

    public static ConfigEntry<float> ConfigApplyDelay;
    public static ConfigEntry<float> ConfigPollIntervalSeconds;

    // Read by Common/Config. Derived from the plugin so it cannot drift from the mod's name.
    internal static readonly string cfgFolder = Deathlink.PluginName;
    const string leaderboardCfg = "leaderboard.yaml";
    internal static String leaderboardPath = Path.Combine(Paths.ConfigPath, cfgFolder, leaderboardCfg);

    // DeathChoices.yaml and CharacterSettings.yaml are owned by Common/Config -- paths, defaults,
    // watching, validation and the sync RPCs all come from there. See DeathlinkConfigFiles.cs.
    public static CustomRPC resetChoiceRPC;

    public static ConfigEntry<float> SkillProgressUpdateCheckInterval;

    public ValConfig(ConfigFile Config)
    {
        // ensure all the config values are created
        cfg = Config;
        cfg.SaveOnConfigSet = true;
        CreateConfigValues(Config);
        SetupConfigRPCs();

        // A client must not reload: Jotunn has already replaced its in-memory values with the server's,
        // and a reload would clobber them with whatever this machine has on disk.
        ConfigFileWatcher.Register(cfg.ConfigFilePath, OnMainConfigFileChanged);
    }

    private static void OnMainConfigFileChanged(string _) {
        if (ZNet.instance == null || ZNet.instance.IsServer() == false) { return; }
        Logger.LogInfo("Configuration file has been changed, reloading settings.");
        cfg.Reload();
    }

    public static string GetSecondaryConfigDirectoryPath() {
        var patchesFolderPath = Path.Combine(Paths.ConfigPath, cfgFolder);
        var dirInfo = Directory.CreateDirectory(patchesFolderPath);

        return dirInfo.FullName;
    }

    public void SetupConfigRPCs()
    {
        // Resetting a player's choice is a targeted admin action against a person, not a config file, so
        // it stays here rather than moving into the config framework.
        resetChoiceRPC = NetworkManager.Instance.AddRPC("DEATHLK_RESET", OnServerRecieveResetRPC, OnClientRecieveResetRPC);

        LeaderboardData.SetupRPC();
    }

    // Create Configuration and load it.
    private void CreateConfigValues(ConfigFile Config)
    {
        ItemsNotSkillChecked = BindServerConfig("DeathProgression", "ItemsNotSkillChecked", "Tin,TinOre,Copper,CopperOre,CopperScrap,Bronze,Iron,IronScrap,Silver,SilverOre,DragonEgg,chest_hildir1,chest_hildir2,chest_hildir3,BlackMetal,BlackMetalScrap,DvergrNeedle,MechanicalSpring,FlametalNew,FlametalOreNew", "List of items that are not rolled to be saved through death progression.");

        SkillGainOnKills = BindServerConfig("DeathSkillGain", "SkillGainOnKills", 5f, "Skill Gain from killing non-boss creatures.");
        SkillGainOnBossKills = BindServerConfig("DeathSkillGain", "SkillGainOnBossKills", 20f, "Skill Gain from killing boss creatures.");
        SkillGainOnCrafts = BindServerConfig("DeathSkillGain", "SkillGainOnCrafts", 0.8f, "Skill Gain from crafting.");
        SkillGainOnResourceGathering = BindServerConfig("DeathSkillGain", "SkillGainOnResourceGathering", 0.1f, "Skill Gain from resource gathering.");
        SkillGainOnBuilding = BindServerConfig("DeathSkillGain", "SkillGainOnBuilding", 0.5f, "Skill Gain from building.");

        SkillProgressUpdateCheckInterval = BindServerConfig("DeathSkillGain", "SkillProgressUpdateCheckInterval", 1f, "How frequently skill gains are computed and added. More frequently means smaller xp gains more often.", true, 0.1f, 5f);

        ShowDeathMapMarker = BindServerConfig("DeathTweaks", "ShowDeathMapMarker", true, "Whether or not a map marker is placed on your death location.");

        UsePrivateKeysForDeathChoice = BindServerConfig("Config", "UsePrivateKeysForDeathChoice", true, "If true, death configuration is stored and checked as a private key on the player. Key value is synced when connecting to a server. False uses the yaml configuration flatfile.", advanced: true);

        DefaultDeathChoice = BindServerConfig("Config", "DefaultDeathChoice", "", "Death choice key (eg. Vanilla, Rougelike1) that new players are automatically assigned the first time they join, skipping the selection popup. Leave empty to show the selection popup instead.");
        AllowedDeathChoiceChanges = BindServerConfig("Config", "AllowedDeathChoiceChanges", 1, "How many times a player may change their Deathlink choice from the compendium. 0 disables the change button.", valmin: 0, valmax: 10);

        EnableAlmanacClassesXPLossOnDeath = BindServerConfig("ModIntegrations", "EnableAlmanacClassesXPLossOnDeath", true, "If true, XP loss also happens for characters Alamanc Class level.");
        AlmanacClassesXPLossScale = BindServerConfig("ModIntegrations", "AlmanacClassesXPLossScale", 1.0f, "How strong the XP loss for Almanac is, lower = less XP loss, higher = more XP loss.");
        AlmanacClassesXPGainScale = BindServerConfig("ModIntegrations", "AlmanacClassesXPGainScale", 20f, "How much Almanac Classes XP is gained based on Deathlink actions. This is gained at an inregular interval based on deathlink skill gains.");
        EnableWackyMMOXPLossOnDeath = BindServerConfig("ModIntegrations", "EnableWackyMMOXPLossOnDeath", true, "If true, XP loss also happens for characters WackyMMO level.");
        WackyMMOXPLossScale = BindServerConfig("ModIntegrations", "WackyMMOXPLossScale", 1.0f, "How strong the XP loss for WackyMMO is, lower = less XP loss, higher = more XP loss.");
        WackyMMOXPGainScale = BindServerConfig("ModIntegrations", "WackyMMOXPGainScale", 1.0f, "How strong the XP gain for WackyMMO is, lower = less XP gain, higher = more XP gain.");

        EnableLeaderboard = BindServerConfig("Leaderboard", "EnableLeaderboard", true, "Whether the server-tracked leaderboard (shown in the Trophies tab) is enabled.");
        LeaderboardSyncInterval = BindServerConfig("Leaderboard", "LeaderboardSyncInterval", 30f, "How often (in minutes) clients report their stats to the server and the server broadcasts the leaderboard back to clients.", false, 5f, 120f);

        // Read by Common/Config. The poll interval drives how quickly a hand edit to DeathChoices.yaml is
        // noticed; the apply delay coalesces the two writes most editors make when saving one file.
        ConfigPollIntervalSeconds = BindServerConfig("Config", "Config Poll Interval", 30f, "Seconds between checks for edits to this mod's yaml config files and its BepInEx config file. Lower reacts faster to a hand edit, higher does less disk work.", true, 1f, 300f);
        ConfigApplyDelay = BindServerConfig("Config", "Config Apply Delay", 1f, "Delay in seconds before a changed config file is applied in-game. Coalesces a burst of rapid edits into a single apply. Set to 0 to apply instantly.", true, 0f, 10f);

        // Debugmode
        EnableDebugMode = Config.Bind("Client config", "EnableDebugMode", false,
            new ConfigDescription("Enables Debug logging.",
            null,
            new ConfigurationManagerAttributes { IsAdvanced = true }));
        EnableDebugMode.SettingChanged += Logger.enableDebugLogging;
        Logger.CheckEnableDebugLogging();
    }

    private static IEnumerator OnServerRecieveResetRPC(long sender, ZPackage package)
    {
        string platformID = package.ReadString();
        Logger.LogInfo($"Received reset request for platform ID {platformID} from sender {sender}.");
        // Find the target charID, peer UID, and the local player's platform ID in one pass
        long charID = 0L;
        long targetPeerUID = 0L;
        string localPlayerPlatformId = "";
        foreach (ZNet.PlayerInfo player in ZNet.instance.GetPlayerList()) {
            Logger.LogInfo($"Checking player {player.m_userInfo.m_displayName} with platform ID {player.m_userInfo.m_id.m_userID} and character ID {player.m_characterID.ID} | requested: {platformID}");
            if (player.m_characterID.ID == 0) {
                Logger.LogWarning($"Player {player.m_userInfo.m_displayName} has an invalid character ID of 0. This player is invalid and will be skipped.");
                continue;
            }
            long pCharID = player.m_characterID.ID;
            if (pCharID == 0L) {
                ZDO zDO = ZDOMan.instance.GetZDO(player.m_characterID);
                pCharID = zDO.GetLong(ZDOVars.s_playerID, 0L);
            }
            if (player.m_userInfo.m_id.m_userID == platformID) {
                Logger.LogInfo($"Match found for player {player.m_userInfo.m_displayName} with platform ID {platformID} and character ID {pCharID}.");
                charID = pCharID;
                if (DeathConfigurationData.playerSettings.ContainsKey(charID)) {
                    Logger.LogInfo($"Removing stored death configuration for character ID {charID}.");
                    DeathConfigurationData.playerSettings.Remove(charID);
                }
                foreach(ZNetPeer peer in ZNet.instance.GetPeers()) {
                    if (peer.m_socket != null && peer.m_socket.GetHostName() == platformID) {
                        Logger.LogInfo($"Match found for peer {peer.m_playerName} with UID {peer.m_uid} and platform ID {platformID}. Setting targetPeerUID for reset RPC.");
                        targetPeerUID = peer.m_uid;
                        break;
                    }
                }
            }

            try {
                // This is only for handling hosted local server resets
                if (Player.m_localPlayer != null && player.m_characterID == Player.m_localPlayer.m_nview.GetZDO().m_uid) {
                    localPlayerPlatformId = player.m_userInfo.m_id.m_userID;
                }
            } catch {
                Logger.LogWarning($"Failed to get platform ID for player {player.m_userInfo.m_displayName} with character ID {player.m_characterID.ID}. This may cause issues with resetting death choices for the local player.");
            }
        }
        DeathConfigurationData.WritePlayerChoices();

        Logger.LogInfo($"Reset requested for platform ID {platformID} (charID: {charID}, peerUID: {targetPeerUID}), local platform ID: {localPlayerPlatformId}.");

        if (!string.IsNullOrEmpty(localPlayerPlatformId) && localPlayerPlatformId == platformID) {
            // The reset target is the host's own local player. The host is not a remote peer, so
            // there is nothing to forward to; apply the reset directly on this machine.
            DeathConfigurationData.ResetLocalPlayerChoice();
        } else if (targetPeerUID != 0L) {
            ZPackage fwdPackage = new ZPackage();
            fwdPackage.Write(platformID);
            resetChoiceRPC.SendPackage(targetPeerUID, fwdPackage);
        } else {
            Logger.LogWarning($"No connected target found for platform ID {platformID}; reset was not delivered to any client.");
        }

        yield return null;
    }

    private static IEnumerator OnClientRecieveResetRPC(long sender, ZPackage package)
    {
        Logger.LogInfo("Reset requested for local players death choice.");
        DeathConfigurationData.ResetLocalPlayerChoice();

        yield return null;
    }

    /// <summary>
    ///  Helper to bind configs for bool types
    /// </summary>
    /// <param name="config_file"></param>
    /// <param name="catagory"></param>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="description"></param>
    /// <param name="acceptableValues"></param>>
    /// <param name="advanced"></param>
    /// <returns></returns>
    public static ConfigEntry<bool> BindServerConfig(string catagory, string key, bool value, string description, AcceptableValueBase acceptableValues = null, bool advanced = false)
    {
        return cfg.Bind(catagory, key, value,
            new ConfigDescription(description,
                acceptableValues,
            new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
            );
    }

    /// <summary>
    /// Helper to bind configs for int types
    /// </summary>
    /// <param name="config_file"></param>
    /// <param name="catagory"></param>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="description"></param>
    /// <param name="advanced"></param>
    /// <param name="valmin"></param>
    /// <param name="valmax"></param>
    /// <returns></returns>
    public static ConfigEntry<int> BindServerConfig(string catagory, string key, int value, string description, bool advanced = false, int valmin = 0, int valmax = 150)
    {
        return cfg.Bind(catagory, key, value,
            new ConfigDescription(description,
            new AcceptableValueRange<int>(valmin, valmax),
            new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
            );
    }

    /// <summary>
    /// Helper to bind configs for float types
    /// </summary>
    /// <param name="config_file"></param>
    /// <param name="catagory"></param>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="description"></param>
    /// <param name="advanced"></param>
    /// <param name="valmin"></param>
    /// <param name="valmax"></param>
    /// <returns></returns>
    public static ConfigEntry<float> BindServerConfig(string catagory, string key, float value, string description, bool advanced = false, float valmin = 0, float valmax = 150)
    {
        return cfg.Bind(catagory, key, value,
            new ConfigDescription(description,
            new AcceptableValueRange<float>(valmin, valmax),
            new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
            );
    }
    
    /// <summary>
    /// Helper to bind configs for strings
    /// </summary>
    /// <param name="config_file"></param>
    /// <param name="catagory"></param>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="description"></param>
    /// <param name="advanced"></param>
    /// <returns></returns>
    public static ConfigEntry<string> BindServerConfig(string catagory, string key, string value, string description, AcceptableValueList<string> acceptableValues = null, bool advanced = false)
    {
        return cfg.Bind(catagory, key, value,
            new ConfigDescription(
                description,
                acceptableValues,
            new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
            );
    }
}
