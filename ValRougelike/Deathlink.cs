using BepInEx;
using BepInEx.Logging;
using Deathlink.Common;
using Deathlink.Death;
using Deathlink.external;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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
        public const string PluginVersion = "0.10.2";

        public ValConfig cfg;
        internal static AssetBundle EmbeddedResourceBundle;
        internal static bool AzuEPILoaded = false;
        internal static bool RustyAlmanacClassesLoaded = false;
        internal static bool WackyMMOLoaded = false;

        // Use this class to add your own localization to the game
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();
        public static ManualLogSource Log;

        public void Awake()
        {
            Log = this.Logger;
            cfg = new ValConfig(Config);
            // Jotunn comes with its own Logger class to provide a consistent Log style for all mods using it
            AddLocalizations();
            EmbeddedResourceBundle = AssetUtils.LoadAssetBundleFromResources("Deathlink.AssetsEmbedded.deathless", typeof(Deathlink).Assembly);
            DeathProgressionSkill.SetupDeathSkill();


            // API Checks for other mods
            if (AzuExtendedPlayerInventory.API.IsLoaded()) {
                AzuEPILoaded = true;
            }
            if (BepInExUtils.GetPlugins().Keys.Contains("WackyMole.EpicMMOSystem")) {
                EpicMMOSystem_API.Init();
                if (EpicMMOSystem_API.state == EpicMMOSystem_API.API_State.Ready) {
                    WackyMMOLoaded = true;
                    Logger.LogInfo("WackMMO API loaded.");
                } else {
                    Logger.LogInfo("WackMMO API installed but API not ready.");
                }
            }
            if (BepInExUtils.GetPlugins().Keys.Contains("RustyMods.AlmanacClasses")) {
                RustyAlmanacClassesLoaded = true;
            } 

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
            DeathConfigurationData.Init();
            LeaderboardData.Init();
            StartCoroutine(LeaderboardData.SyncLoop());
            TerminalCommands.AddCommands();
            Logger.LogInfo("Death is not the end.");
            // To learn more about Jotunn's features, go to
            // https://valheim-modding.github.io/Jotunn/tutorials/overview.html

            //MinimapManager.OnVanillaMapDataLoaded += DeathConfigurationData.CheckAndSetPlayerDeathConfig;
        }

        public static DeathChoiceLevel pcfg(){
            return DeathConfigurationData.playerDeathConfiguration;
        }

        // This loads all localizations within the localization directory.
        // Localizations should be plain JSON objects with each of the two required entries being seperate eg:
        // "item_sword": "sword-name-here",
        // "item_sword_description": "sword-description-here",
        // the localization file itself should be a casematched language as defined by one of the "folder" language names from here:
        // https://valheim-modding.github.io/Jotunn/data/localization/language-list.html
        private void AddLocalizations()
        {
            Localization = LocalizationManager.Instance.GetLocalization();

            // Ensure localization folder exists
            var translationFolder = Path.Combine(BepInEx.Paths.ConfigPath, "Deathlink", "localizations");
            Directory.CreateDirectory(translationFolder);
            foreach (string embeddedResouce in typeof(Deathlink).Assembly.GetManifestResourceNames())
            {
                if (!embeddedResouce.Contains("Localizations")) { continue; }
                // Read the localization file

                string localization = ReadEmbeddedResourceFile(embeddedResouce);
                // since I use comments in the localization that are not valid JSON those need to be stripped
                string cleaned_localization = Regex.Replace(localization, @"\/\/.*", "");
                Dictionary<string, string> internal_localization = SimpleJson.SimpleJson.DeserializeObject<Dictionary<string, string>>(cleaned_localization);
                // Just the localization name
                var localization_name = embeddedResouce.Split('.');
                if (File.Exists($"{translationFolder}/{localization_name[2]}.json"))
                {
                    string cached_translation_file = File.ReadAllText($"{translationFolder}/{localization_name[2]}.json");
                    try
                    {
                        Dictionary<string, string> cached_localization = SimpleJson.SimpleJson.DeserializeObject<Dictionary<string, string>>(cached_translation_file);
                        UpdateLocalizationWithMissingKeys(internal_localization, cached_localization);
                        Logger.LogDebug($"Reading {translationFolder}/{localization_name[2]}.json");
                        File.WriteAllText($"{translationFolder}/{localization_name[2]}.json", SimpleJson.SimpleJson.SerializeObject(cached_localization));
                        string updated_local_translation = File.ReadAllText($"{translationFolder}/{localization_name[2]}.json");
                        Localization.AddJsonFile(localization_name[2], updated_local_translation);
                    }
                    catch
                    {
                        File.WriteAllText($"{translationFolder}/{localization_name[2]}.json", cleaned_localization);
                        Logger.LogDebug($"Reading {embeddedResouce}");
                        Localization.AddJsonFile(localization_name[2], cleaned_localization);
                    }
                }
                else
                {
                    File.WriteAllText($"{translationFolder}/{localization_name[2]}.json", cleaned_localization);
                    Logger.LogDebug($"Reading {embeddedResouce}");
                    Localization.AddJsonFile(localization_name[2], cleaned_localization);
                }
                Logger.LogDebug($"Added localization: '{localization_name[2]}'");
            }
        }

        private void UpdateLocalizationWithMissingKeys(Dictionary<string, string> internal_localization, Dictionary<string, string> cached_localization)
        {
            if (internal_localization.Keys.Count != cached_localization.Keys.Count)
            {
                Logger.LogDebug("Cached localization was missing some entries. They will be added.");
                foreach (KeyValuePair<string, string> entry in internal_localization)
                {
                    if (!cached_localization.ContainsKey(entry.Key))
                    {
                        cached_localization.Add(entry.Key, entry.Value);
                    }
                }
            }
        }

        // This reads an embedded file resouce name, these are all resouces packed into the DLL
        // they all have a format following this:
        // ValheimArmory.localizations.English.json
        private string ReadEmbeddedResourceFile(string filename)
        {
            using (var stream = typeof(Deathlink).Assembly.GetManifestResourceStream(filename))
            {
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Fisher-Yates style list sort for string lists.
        /// </summary>
        /// <param name="inputList"></param>
        /// <returns></returns>
        public static List<T> shuffleList<T>(List<T> inputList)
        {    //take any list of GameObjects and return it with Fischer-Yates shuffle
            int i = 0;
            int t = inputList.Count;
            int r = 0;
            T p = default(T);
            List<T> tempList = new List<T>();
            tempList.AddRange(inputList);

            while (i < t)
            {
                r = UnityEngine.Random.Range(i, tempList.Count);
                p = tempList[i];
                tempList[i] = tempList[r];
                tempList[r] = p;
                i++;
            }

            return tempList;
        }
    }
}