using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
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

        // The player id of the character a peer is playing, read from that character's ZDO. Zero when unknown.
        // The client names its own character (ZNet.RPC_CharacterID is unchecked), so the ZDO must at least be
        // owned by that connection. The id on it is still the client's word: it owns that ZDO.
        public static long PlayerIdOfPeer(ZNetPeer peer)
        {
            ZDO character = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(peer.m_characterID) : null;
            if (character == null || character.GetOwner() != peer.m_uid) { return 0L; }
            return character.GetLong(ZDOVars.s_playerID, 0L);
        }

        public static void SafeInsertOrAppend(this Dictionary<ItemResults, List<ItemDrop.ItemData>> dict, ItemResults key, List<ItemDrop.ItemData> value)
        {
            if (!dict.ContainsKey(key)) {
                dict.Add(key, value);
            } else {
                dict[key].AddRange(value);
            }
        }
    }
}
