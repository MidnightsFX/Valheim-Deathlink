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

    const string cfgFolder = "Deathlink";
    const string deathChoicesCfg = "DeathChoices.yaml";
    const string deathSettingsCfg = "CharacterSettings.yaml";
    const string leaderboardCfg = "leaderboard.yaml";
    internal static String deathChoicesPath = Path.Combine(Paths.ConfigPath, cfgFolder, deathChoicesCfg);
    internal static String playerSettingsPath = Path.Combine(Paths.ConfigPath, cfgFolder, deathSettingsCfg);
    internal static String leaderboardPath = Path.Combine(Paths.ConfigPath, cfgFolder, leaderboardCfg);

    private static CustomRPC deathChoiceRPC;
    private static CustomRPC characterSettingRPC;
    public static CustomRPC resetChoiceRPC;

    public static ConfigEntry<float> SkillProgressUpdateCheckInterval;
    
    public ValConfig(ConfigFile Config)
    {
        // ensure all the config values are created
        cfg = Config;
        cfg.SaveOnConfigSet = true;
        CreateConfigValues(Config);
        SetupConfigRPCs();
        LoadYamlConfigs();
    }

    public static string GetSecondaryConfigDirectoryPath() {
        var patchesFolderPath = Path.Combine(Paths.ConfigPath, cfgFolder);
        var dirInfo = Directory.CreateDirectory(patchesFolderPath);

        return dirInfo.FullName;
    }

    public void SetupConfigRPCs()
    {
        deathChoiceRPC = NetworkManager.Instance.AddRPC("DEATHLK_CH", OnServerRecieveConfigs, OnClientReceiveDeathChoiceConfigs);
        characterSettingRPC = NetworkManager.Instance.AddRPC("DEATHLK_PSET", OnServerRecievePlayerSettingsConfig, OnClientReceivePlayerSettingsConfigs);
        resetChoiceRPC = NetworkManager.Instance.AddRPC("DEATHLK_RESET", OnServerRecieveResetRPC, OnClientRecieveResetRPC);

        SynchronizationManager.Instance.AddInitialSynchronization(deathChoiceRPC, SendDeathChoices);
        SynchronizationManager.Instance.AddInitialSynchronization(characterSettingRPC, SendCharSettings);

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

        // Debugmode
        EnableDebugMode = Config.Bind("Client config", "EnableDebugMode", false,
            new ConfigDescription("Enables Debug logging.",
            null,
            new ConfigurationManagerAttributes { IsAdvanced = true }));
        EnableDebugMode.SettingChanged += Logger.enableDebugLogging;
        Logger.CheckEnableDebugLogging();
    }

    internal void LoadYamlConfigs()
    {
        string externalConfigFolder = ValConfig.GetSecondaryConfigDirectoryPath();
        string[] presentFiles = Directory.GetFiles(externalConfigFolder);
        bool foundDeathChoices = false;
        bool foundCharacterSettings = false;

        foreach (string configFile in presentFiles)
        {
            if (configFile.Contains(deathChoicesCfg))
            {
                Logger.LogDebug($"Found Deathchoice configuration: {configFile}");
                deathChoicesPath = configFile;
                foundDeathChoices = true;
            }

            if (configFile.Contains(deathSettingsCfg))
            {
                Logger.LogDebug($"Found Character configuration: {configFile}");
                playerSettingsPath = configFile;
                foundCharacterSettings = true;
            }
        }

        if (foundDeathChoices == false)
        {
            Logger.LogDebug("Death Choices missing, recreating.");
            using (StreamWriter writetext = new StreamWriter(deathChoicesPath))
            {
                String header = @"#################################################
# Deathlink - Death Choice Configuration
#################################################
";
                writetext.WriteLine(header);
                writetext.WriteLine(DeathConfigurationData.DeathLevelsYamlDefaultConfig());
            }
        }

        if (foundCharacterSettings == false)
        {
            Logger.LogDebug("Character Settings missing, recreating.");
            using (StreamWriter writetext = new StreamWriter(playerSettingsPath))
            {
                String header = @"#################################################
# Deathlink - Character settings
#################################################
";
                writetext.WriteLine(header);
                writetext.WriteLine(DeathConfigurationData.PlayerSettingsDefaultConfig());
            }
        }

        SetupFileWatcher(deathChoicesCfg);
        SetupFileWatcher(deathSettingsCfg);
    }

    private void SetupFileWatcher(string filtername)
    {
        FileSystemWatcher fw = new FileSystemWatcher();
        fw.Path = ValConfig.GetSecondaryConfigDirectoryPath();
        fw.NotifyFilter = NotifyFilters.LastWrite;
        fw.Filter = filtername;
        fw.Changed += new FileSystemEventHandler(UpdateConfigFileOnChange);
        fw.Created += new FileSystemEventHandler(UpdateConfigFileOnChange);
        fw.Renamed += new RenamedEventHandler(UpdateConfigFileOnChange);
        fw.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        fw.EnableRaisingEvents = true;
    }

    private static void UpdateConfigFileOnChange(object sender, FileSystemEventArgs e)
    {
        if (SynchronizationManager.Instance.PlayerIsAdmin == false)
        {
            Logger.LogDebug("Player is not an admin, and not allowed to change local configuration. Ignoring.");
            return;
        }
        if (!File.Exists(e.FullPath)) { return; }

        string filetext = File.ReadAllText(e.FullPath);
        var fileInfo = new FileInfo(e.FullPath);
        Logger.LogDebug($"Filewatch changes from: ({fileInfo.Name}) {fileInfo.FullName}");
        switch (fileInfo.Name)
        {
            case deathChoicesCfg:
                Logger.LogDebug("Triggering Death Choices Settings update.");
                DeathConfigurationData.UpdateDeathLevelsConfig(filetext);
                deathChoiceRPC.SendPackage(ZNet.instance.m_peers, SendFileAsZPackage(e.FullPath));
                break;
            //case deathSettingsCfg:
            //    Logger.LogDebug("Triggering Level Settings update.");
            //    // LevelSystemData.UpdateYamlConfig(filetext);
            //    characterSettingRPC.SendPackage(ZNet.instance.m_peers, SendFileAsZPackage(e.FullPath));
            //    break;
        }
    }

    private static IEnumerator OnClientReceiveDeathChoiceConfigs(long sender, ZPackage package) {
        var yaml = package.ReadString();
        DeathConfigurationData.UpdateDeathLevelsConfig(yaml);
        DeathConfigurationData.WriteDeathChoices();
        yield return null;
    }

    private static IEnumerator OnServerRecieveConfigs(long sender, ZPackage package)
    {
        yield return null;
    }

    private static IEnumerator OnServerRecievePlayerSettingsConfig(long sender, ZPackage package)
    {
        var yaml = package.ReadString();
        DeathConfigurationData.UpdatePlayerConfigSettings(yaml);
        DeathConfigurationData.WritePlayerChoices();
        yield return null;
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

    private static IEnumerator OnClientReceivePlayerSettingsConfigs(long sender, ZPackage package)
    {
        // only updates in memory
        var yaml = package.ReadString();
        DeathConfigurationData.UpdatePlayerConfigSettings(yaml);
        yield return null;
    }

    private static ZPackage SendFileAsZPackage(string filepath)
    {
        string filecontents = File.ReadAllText(filepath);
        ZPackage package = new ZPackage();
        package.Write(filecontents);
        return package;
    }

    private static ZPackage SendCharSettings()
    {
        return SendFileAsZPackage(playerSettingsPath);
    }
    private static ZPackage SendDeathChoices()
    {
        return SendFileAsZPackage(deathChoicesPath);
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