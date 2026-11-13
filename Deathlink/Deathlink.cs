using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Deathlink.Common;
using Deathlink.Death;
using Deathlink.external;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using static Deathlink.Common.DataObjects;

namespace Deathlink
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid, BepInDependency.DependencyFlags.HardDependency)]
    [NetworkCompatibility(CompatibilityLevel.ClientMustHaveMod, VersionStrictness.Minor)]
    [BepInDependency("Azumatt.AzuExtendedPlayerInventory", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("RustyMods.AlmanacClasses", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("WackyMole.EpicMMOSystem", BepInDependency.DependencyFlags.SoftDependency)]
    internal class Deathlink : BaseUnityPlugin
    {
        public const string PluginGUID = "MidnightsFX.Deathlink";
        public const string PluginName = "Deathlink";
        public const string PluginVersion = "0.13.0";

        internal static AssetBundle EmbeddedResourceBundle;
        internal static bool RustyAlmanacClassesLoaded = false;
        internal static bool WackyMMOLoaded = false;

        public static ManualLogSource Log;

        public void Awake()
        {
            Log = this.Logger;
            new ValConfig(Config);
            AddLocalizations();
            EmbeddedResourceBundle = AssetUtils.LoadAssetBundleFromResources("Deathlink.AssetsEmbedded.deathless", typeof(Deathlink).Assembly);
            DeathProgressionSkill.SetupDeathSkill();

            try {
                if (Chainloader.PluginInfos.ContainsKey("WackyMole.EpicMMOSystem")) {
                    EpicMMOSystem_API.Init();
                    WackyMMOLoaded = EpicMMOSystem_API.state == EpicMMOSystem_API.API_State.Ready;
                    Logger.LogInfo(WackyMMOLoaded ? "WackMMO API loaded." : "WackMMO API installed but API not ready.");
                }
                RustyAlmanacClassesLoaded = Chainloader.PluginInfos.ContainsKey("RustyMods.AlmanacClasses");
            } catch (Exception e) {
                Logger.LogWarning($"Could not connect to another mod's API, its integration is off: {e}");
            }

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
            // After Harmony, so a validator can see anything a patch registered.
            YamlConfigManager.Init();
            // After YamlConfigManager, because it edits a registered file.
            DeathChoiceEditor.Init();
            LeaderboardData.Init();
            StartCoroutine(LeaderboardData.SyncLoop());
            TerminalCommands.AddCommands();
            Logger.LogInfo("Death is not the end.");
        }

        public static DeathChoiceLevel pcfg(){
            return DeathConfigurationData.playerDeathConfiguration;
        }

        // Each embedded Localizations/<Language>.json is copied to the config folder so it can be edited
        // there. An edited copy is kept, and only gains the keys a newer release added.
        private void AddLocalizations()
        {
            // Set here, not in a static initialiser: one can run before BepInEx has registered this plugin,
            // and Jotunn then attributes the whole mod - its RPC names included - to Jotunn itself.
            CustomLocalization localization = LocalizationManager.Instance.GetLocalization();

            var translationFolder = Path.Combine(BepInEx.Paths.ConfigPath, "Deathlink", "localizations");
            Directory.CreateDirectory(translationFolder);
            foreach (string embeddedResource in typeof(Deathlink).Assembly.GetManifestResourceNames())
            {
                if (!embeddedResource.Contains("Localizations")) { continue; }

                string embedded = ReadEmbeddedResourceFile(embeddedResource);
                // "Deathlink.Localizations.<Language>.json"; the csproj pins these names.
                string language = embeddedResource.Split('.')[2];
                string cachedPath = Path.Combine(translationFolder, $"{language}.json");
                string text = embedded;
                if (File.Exists(cachedPath))
                {
                    try
                    {
                        Dictionary<string, string> cached = SimpleJson.SimpleJson.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(cachedPath));
                        Dictionary<string, string> current = SimpleJson.SimpleJson.DeserializeObject<Dictionary<string, string>>(embedded);
                        foreach (KeyValuePair<string, string> entry in current)
                        {
                            if (!cached.ContainsKey(entry.Key)) { cached.Add(entry.Key, entry.Value); }
                        }
                        text = SimpleJson.SimpleJson.SerializeObject(cached);
                    }
                    catch (Exception e)
                    {
                        Logger.LogWarning($"Could not read {cachedPath}, replacing it with the built-in {language} text: {e.Message}");
                    }
                }
                File.WriteAllText(cachedPath, text);
                localization.AddJsonFile(language, text);
                Logger.LogDebug($"Added localization: '{language}'");
            }
        }

        private string ReadEmbeddedResourceFile(string filename)
        {
            using (var stream = typeof(Deathlink).Assembly.GetManifestResourceStream(filename))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        // Fisher-Yates shuffle into a new list; the input is left as it was.
        public static List<T> shuffleList<T>(List<T> inputList)
        {
            List<T> shuffled = new List<T>(inputList);
            for (int i = 0; i < shuffled.Count; i++)
            {
                int r = UnityEngine.Random.Range(i, shuffled.Count);
                T swap = shuffled[i];
                shuffled[i] = shuffled[r];
                shuffled[r] = swap;
            }
            return shuffled;
        }
    }
}
