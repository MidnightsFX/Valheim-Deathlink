using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using static Deathlink.Common.DataObjects;

namespace Deathlink.Common
{
    internal static class Utils
    {

        public static CodeMatcher CreateLabelOffset(this CodeMatcher matcher, out Label label, int offset = 0)
        {
            return matcher.CreateLabelAt(matcher.Pos + offset, out label);
        }

        /// <summary>
        /// True when PrefabManager.GetPrefab can actually resolve vanilla names. ZNetScene.m_namedPrefabs
        /// is the first table GetPrefab consults and holds every registered prefab, item drops included,
        /// so it is the accurate test for "the prefab table exists".
        ///
        /// Do NOT use `PrefabManager.Instance == null` for this. Jotunn declares it as
        /// `_instance ??= new PrefabManager()`, so reading the property constructs it and the guard never
        /// trips -- which is what made the startup validation pass warn about every name in DeathChoices.
        ///
        /// Deliberately does not also require ObjectDB.instance: ObjectDB.Awake and ZNetScene.Awake have
        /// no guaranteed order, so gating on it could leave the headless validation pass a no-op with no
        /// player load left to retry it. By the time the player path runs ObjectDB is up, and GetPrefab
        /// consults it on its own.
        /// </summary>
        public static bool PrefabsAvailable()
        {
            return ZNetScene.instance != null
                && ZNetScene.instance.m_namedPrefabs != null
                && ZNetScene.instance.m_namedPrefabs.Count > 0;
        }

        public static bool PlayerHasUniqueKey(this Player player, string key)
        {
            foreach (string pkey in player.GetUniqueKeys()) {
                if (pkey.StartsWith(key)) { return true; }
            }
            return false;
        }

        public static bool PlayerRemoveUniqueKey(this Player player, string key)
        {
            List<string> keys = player.GetUniqueKeys();
            foreach (string pkey in keys) {
                if (pkey.StartsWith(key)) {
                    player.RemoveUniqueKey(pkey);
                    return true;
                }
            }
            return false;
        }

        public static void SafeInsertOrAppend(this Dictionary<ItemResults, List<ItemDrop.ItemData>> dict, ItemResults key, List<ItemDrop.ItemData> value)
        {
            if (!dict.ContainsKey(key)) {
                dict.Add(key, value);
            } else {
                dict[key].AddRange(value);
            }
        }
        public static void SafeInsertOrAppend(this Dictionary<ItemResults, List<ItemDrop.ItemData>> dict, ItemResults key, ItemDrop.ItemData value)
        {
            if (!dict.ContainsKey(key))
            {
                dict.Add(key, new List<ItemDrop.ItemData>() { value } );
            }
            else
            {
                dict[key].Add(value);
            }
        }
    }
}
