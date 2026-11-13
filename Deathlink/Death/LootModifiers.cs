using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using static Deathlink.Common.DataObjects;

namespace Deathlink.Death
{
    public static class LootModifiers
    {
        [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
        public static class CalculateLootByModifiers
        {
            static void Postfix(List<KeyValuePair<GameObject, int>> __result, CharacterDrop __instance) {
                if (!__instance.m_character.m_localPlayerHasHit) { return; }
                DeathChoiceLevel level = Deathlink.pcfg();
                bool scales = level.HasResourceModifiers(ResourceGainTypes.Kills);
                bool rollsLoot = level.HasLoot(ResourceGainTypes.Kills);
                if (!scales && !rollsLoot) { return; }

                // Harmony only writes __result back when it is declared 'ref'; a plain reassignment
                // is discarded, so the list is changed in place.
                if (scales) {
                    for (int i = __result.Count - 1; i >= 0; i--) {
                        KeyValuePair<GameObject, int> drop = __result[i];
                        float mod = level.GetResourceModifier(drop.Key, ResourceGainTypes.Kills);
                        if (mod == 1f) { continue; }
                        int amount = HarvestModifiers.ScaleCount(drop.Value, mod);
                        if (amount > 0) {
                            __result[i] = new KeyValuePair<GameObject, int>(drop.Key, amount);
                        } else {
                            __result.RemoveAt(i);
                        }
                    }
                }
                // Added to the drop list rather than spawned here, so vanilla drops it with the rest: spread out,
                // stamped with the world level, and with the ragdoll when the creature leaves one.
                if (rollsLoot) { __result.AddRange(level.RollLoot(ResourceGainTypes.Kills)); }
            }
        }
    }
}
