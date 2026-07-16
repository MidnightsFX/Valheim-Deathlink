using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Deathlink.Death
{
    public static class LootModifiers
    {
        [HarmonyPatch(typeof(CharacterDrop))]
        public static class CalculateLootByModifiers
        {
            //[HarmonyDebug]
            [HarmonyPostfix]
            [HarmonyPatch(nameof(CharacterDrop.GenerateDropList))]
            static void Postfix(List<KeyValuePair<GameObject, int>> __result, CharacterDrop __instance) {
                if (!__instance.m_character.m_localPlayerHasHit) { return; }
                if (Deathlink.pcfg().DeathLootModifiers == null || Deathlink.pcfg().DeathLootModifiers.Count == 0) { return; }

                List<KeyValuePair<GameObject, int>> modified = new List<KeyValuePair<GameObject, int>>();

                foreach (var kvp in __result) {
                    float mod = Deathlink.pcfg().GetResouceEarlyCache(kvp.Key.name);
                    if (mod == 1f) {
                        modified.Add(kvp); 
                    } else {
                        int amount = Mathf.RoundToInt(kvp.Value * mod);
                        if (amount >= 1){ modified.Add(new KeyValuePair<GameObject, int>(key: kvp.Key, value: amount)); }
                    }
                }
                //Logger.LogDebug($"Checking for kill random loot.");
                List<KeyValuePair<GameObject, int>> killloot = Deathlink.pcfg().RollKillLoot();
                if (killloot.Count > 0) {
                    //Logger.LogDebug($"Kill Dropping {killloot.Count} types of loot.");
                    foreach (var kvp in killloot) {
                        int i = 0;
                        while(i < kvp.Value) {
                            GameObject.Instantiate(kvp.Key, __instance.transform.position, Quaternion.identity);
                            i++;
                        }
                        
                    }
                }

                // Harmony only writes __result back when it is declared 'ref'; a plain reassignment
                // is discarded. Mutate the existing list in place so the scaled drops actually apply.
                __result.Clear();
                __result.AddRange(modified);
            }
        }
    }
}
