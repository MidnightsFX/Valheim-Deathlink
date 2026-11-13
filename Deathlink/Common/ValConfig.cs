using BepInEx;
using BepInEx.Configuration;
using Deathlink.Death;
using System.IO;

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
    public static ConfigEntry<bool> ShowQuickConfigButton;

    internal static readonly string cfgFolder = Deathlink.PluginName;
    const string leaderboardCfg = "leaderboard.yaml";
    internal static string leaderboardPath = Path.Combine(Paths.ConfigPath, cfgFolder, leaderboardCfg);

    public static ConfigEntry<float> SkillProgressUpdateCheckInterval;

    public ValConfig(ConfigFile Config)
    {
        cfg = Config;
        CreateConfigValues(Config);
        LeaderboardData.SetupRPC();
        ConfigFileWatcher.Register(cfg.ConfigFilePath, OnMainConfigFileChanged);
    }

    private static void OnMainConfigFileChanged(string _) {
        // A connected client must not reload: Jotunn has already replaced its in-memory values with the
        // server's, and a reload would clobber them with whatever this machine has on disk.
        if (ZNet.instance != null && ZNet.instance.IsServer() == false) { return; }
        Logger.LogInfo("Configuration file has been changed, reloading settings.");
        cfg.Reload();
    }

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

        UsePrivateKeysForDeathChoice = BindServerConfig("Config", "UsePrivateKeysForDeathChoice", true, "If true, each character's death choice is stored on the character and follows it to every world. False keeps choices in this server's CharacterSettings.yaml instead.", advanced: true);

        DefaultDeathChoice = BindServerConfig("Config", "DefaultDeathChoice", "", "Death choice key (eg. Vanilla, Rougelike1) that new players are automatically assigned the first time they join, skipping the selection popup. Leave empty to show the selection popup instead.");
        AllowedDeathChoiceChanges = BindServerConfig("Config", "AllowedDeathChoiceChanges", 1, "How many times a player may change their Deathlink choice from the compendium. 0 disables the change button.", valmin: 0, valmax: 10);

        EnableAlmanacClassesXPLossOnDeath = BindServerConfig("ModIntegrations", "EnableAlmanacClassesXPLossOnDeath", true, "If true, XP loss also happens for characters Alamanc Class level.");
        AlmanacClassesXPLossScale = BindServerConfig("ModIntegrations", "AlmanacClassesXPLossScale", 1.0f, "How strong the XP loss for Almanac is, lower = less XP loss, higher = more XP loss.");
        AlmanacClassesXPGainScale = BindServerConfig("ModIntegrations", "AlmanacClassesXPGainScale", 20f, "How much Almanac Classes XP is gained based on Deathlink actions. This is gained at an inregular interval based on deathlink skill gains.");
        EnableWackyMMOXPLossOnDeath = BindServerConfig("ModIntegrations", "EnableWackyMMOXPLossOnDeath", true, "If true, XP loss also happens for characters WackyMMO level.");
        WackyMMOXPLossScale = BindServerConfig("ModIntegrations", "WackyMMOXPLossScale", 1.0f, "How strong the XP loss for WackyMMO is, lower = less XP loss, higher = more XP loss.");
        WackyMMOXPGainScale = BindServerConfig("ModIntegrations", "WackyMMOXPGainScale", 1.0f, "How strong the XP gain for WackyMMO is, lower = less XP gain, higher = more XP gain.");

        EnableLeaderboard = BindServerConfig("Leaderboard", "EnableLeaderboard", true, "Whether the server-tracked leaderboard (opened from a button on the Deathlink compendium page) is enabled.");
        LeaderboardSyncInterval = BindServerConfig("Leaderboard", "LeaderboardSyncInterval", 30f, "How often (in minutes) clients report their stats to the server and the server broadcasts the leaderboard back to clients.", false, 5f, 120f);

        ConfigPollIntervalSeconds = BindServerConfig("Config", "Config Poll Interval", 30f, "Seconds between checks for edits to this mod's yaml config files and its BepInEx config file. A connected client keeps the server's values until it leaves. Lower reacts faster to a hand edit, higher does less disk work.", true, 1f, 300f);
        ConfigApplyDelay = BindServerConfig("Config", "Config Apply Delay", 1f, "Delay in seconds before a changed yaml config file is applied in-game. Coalesces a burst of rapid edits into a single apply. Set to 0 to apply instantly.", true, 0f, 10f);

        // A per-machine UI preference, so a plain client bind rather than BindServerConfig.
        ShowQuickConfigButton = Config.Bind("Client config", "ShowQuickConfigButton", true,
            new ConfigDescription("Show Deathlink in the shared in-game config launcher (bottom right of the main and pause menus).",
            null,
            new ConfigurationManagerAttributes { }));
        ShowQuickConfigButton.SettingChanged += (sender, args) => Death.DeathChoiceEditor.ApplyRegistration();

        EnableDebugMode = Config.Bind("Client config", "EnableDebugMode", false,
            new ConfigDescription("Enables Debug logging.",
            null,
            new ConfigurationManagerAttributes { IsAdvanced = true }));
        EnableDebugMode.SettingChanged += (sender, args) => Logger.UpdateLogLevel();
        Logger.UpdateLogLevel();
    }

    public static ConfigEntry<bool> BindServerConfig(string category, string key, bool value, string description, bool advanced = false)
    {
        return cfg.Bind(category, key, value,
            new ConfigDescription(description,
            null,
            new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
            );
    }

    public static ConfigEntry<int> BindServerConfig(string category, string key, int value, string description, bool advanced = false, int valmin = 0, int valmax = 150)
    {
        return cfg.Bind(category, key, value,
            new ConfigDescription(description,
            new AcceptableValueRange<int>(valmin, valmax),
            new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
            );
    }

    public static ConfigEntry<float> BindServerConfig(string category, string key, float value, string description, bool advanced = false, float valmin = 0, float valmax = 150)
    {
        return cfg.Bind(category, key, value,
            new ConfigDescription(description,
            new AcceptableValueRange<float>(valmin, valmax),
            new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
            );
    }

    public static ConfigEntry<string> BindServerConfig(string category, string key, string value, string description, bool advanced = false)
    {
        return cfg.Bind(category, key, value,
            new ConfigDescription(description,
            null,
            new ConfigurationManagerAttributes { IsAdminOnly = true, IsAdvanced = advanced })
            );
    }
}
