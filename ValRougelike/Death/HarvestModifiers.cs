using Deathlink.Common;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deathlink.Death
{
    /// <summary>
    /// Applies a death choice's resource multiplier (<c>bonusModifer</c>) to harvested drops.
    /// The modifier is a true multiplier: values above 1.0 increase drops, values below 1.0
    /// decrease them (down to nothing). Trees, mine rocks and destructibles all build their
    /// drops through <see cref="DropTable.GetDropList"/> before spawning, so we rescale that
    /// list in place; pickables instead drop one unit at a time, so we rescale each drop's stack.
    /// </summary>
    public static class HarvestModifiers
    {
        // Set true only while a harvestable is spawning its DropTable drops, so the shared
        // DropTable.GetDropList postfix rescales *harvest* drops and nothing else (containers,
        // creatures, etc. also use DropTable). Unity runs all of this on the main thread, so a
        // plain static flag brackets the call reliably.
        internal static bool ScalingActive;

        // ---- shared helpers -------------------------------------------------

        private static bool HasResourceModifiers()
        {
            var cfg = Deathlink.pcfg();
            return cfg.ResourceModifiers != null && cfg.ResourceModifiers.Count > 0;
        }

        /// <summary>
        /// Only the harvester on their own machine gets the modifier. Requires a local player,
        /// configured resource modifiers, and (when hit info is present) that the attacker is the
        /// local player. Mirrors the guards the previous additive patches used.
        /// </summary>
        private static bool ShouldScaleHarvest(HitData hit)
        {
            if (Player.m_localPlayer == null) { return false; }
            if (!HasResourceModifiers()) { return false; }
            if (hit == null) { return false; }
            return hit.m_attacker == Player.m_localPlayer.GetZDOID();
        }

        /// <summary>
        /// Scales an integer count by a (possibly fractional) multiplier while preserving the
        /// expected value: <c>floor(count*m)</c> plus one more with probability equal to the
        /// fractional part. Can return 0, so a modifier below 1.0 may remove a drop entirely.
        /// </summary>
        public static int ScaleCount(int baseCount, float m)
        {
            if (baseCount <= 0) { return 0; }
            float scaled = baseCount * m;
            int whole = Mathf.FloorToInt(scaled);
            float frac = scaled - whole;
            if (frac > 0f && UnityEngine.Random.value < frac) { whole += 1; }
            return whole;
        }

        /// <summary>
        /// Rebuilds a <see cref="DropTable.GetDropList"/> result (one GameObject entry per dropped
        /// unit) with each prefab's unit count scaled by its configured resource modifier. Prefabs
        /// with no modifier (1f) keep their original count.
        /// </summary>
        public static void ScaleDropListInPlace(List<GameObject> drops)
        {
            if (drops == null || drops.Count == 0) { return; }

            // Count units per prefab, keeping first-seen order for a stable rebuild.
            Dictionary<GameObject, int> counts = new Dictionary<GameObject, int>();
            List<GameObject> order = new List<GameObject>();
            foreach (GameObject go in drops)
            {
                if (go == null) { continue; }
                if (counts.TryGetValue(go, out int c)) { counts[go] = c + 1; }
                else { counts[go] = 1; order.Add(go); }
            }

            drops.Clear();
            foreach (GameObject go in order)
            {
                float m = Deathlink.pcfg().GetResouceEarlyCache(go);
                int finalCount = (m == 1f) ? counts[go] : ScaleCount(counts[go], m);
                if (finalCount != counts[go])
                {
                    Logger.LogDebug($"Scaling harvest drop {go.name}: {counts[go]} -> {finalCount} (x{m})");
                }
                for (int i = 0; i < finalCount; i++) { drops.Add(go); }
            }
        }

        /// <summary>
        /// Rolls the chance-based harvest bonus loot (<c>DeathLootModifiers</c> with the Harvesting
        /// action) and spawns it. This is a separate feature from the multiplier and fires once per
        /// harvest event.
        /// </summary>
        private static void SpawnHarvestBonusLoot(Vector3 position)
        {
            List<KeyValuePair<GameObject, int>> harvestloot = Deathlink.pcfg().RollHarvestLoot();
            if (harvestloot.Count == 0) { return; }
            foreach (var kvp in harvestloot)
            {
                for (int i = 0; i < kvp.Value; i++)
                {
                    Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0, 360), 0f);
                    UnityEngine.Object.Instantiate(kvp.Key, position, rotation);
                }
            }
        }

        // ---- shared DropTable rescale (trees, destructibles, mine rocks) ----

        // GetDropList() returns a freshly allocated list, so mutating __result in place is safe.
        [HarmonyPatch(typeof(DropTable), nameof(DropTable.GetDropList), new Type[0])]
        public static class ScaleHarvestDropList
        {
            private static void Postfix(List<GameObject> __result)
            {
                if (!ScalingActive || !HasResourceModifiers()) { return; }
                ScaleDropListInPlace(__result);
            }
        }

        // ---- per-harvestable brackets --------------------------------------
        // Each bracket turns scaling on for the duration of the vanilla destroy/mine call (so only
        // that call's GetDropList gets rescaled) and, on the way out, rolls the chance-based harvest
        // bonus loot. __state records whether *this* call owns the scope, so nested/re-entrant
        // destroys don't clear the flag out from under an outer bracket. The Finalizer runs even if
        // the original throws, so the flag can never get stuck on.

        [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Destroy))]
        public static class TreeLogHarvestScale
        {
            private static void Prefix(HitData hitData, out bool __state)
            {
                __state = false;
                if (!ScalingActive && ShouldScaleHarvest(hitData))
                {
                    ScalingActive = true;
                    __state = true;
                }
            }

            private static void Finalizer(TreeLog __instance, bool __state)
            {
                if (!__state) { return; }
                ScalingActive = false;
                SpawnHarvestBonusLoot(__instance.transform.position);
            }
        }

        [HarmonyPatch(typeof(Destructible), nameof(Destructible.Destroy))]
        public static class DestructibleHarvestScale
        {
            private static void Prefix(Destructible __instance, HitData hit, out bool __state)
            {
                __state = false;
                // Rocks that spawn a fracture (a MineRock5) drop via that fracture, not here; let the
                // MineRock5 bracket handle those so the modifier isn't applied twice.
                if (__instance.m_spawnWhenDestroyed != null) { return; }
                // Only treat destructibles that actually drop harvest resources (parity with the old
                // patch, which required a DropOnDestroyed drop table).
                DropOnDestroyed drops = __instance.GetComponent<DropOnDestroyed>();
                if (drops == null || drops.m_dropWhenDestroyed == null || drops.m_dropWhenDestroyed.IsEmpty()) { return; }
                if (!ScalingActive && ShouldScaleHarvest(hit))
                {
                    ScalingActive = true;
                    __state = true;
                }
            }

            private static void Finalizer(Destructible __instance, bool __state)
            {
                if (!__state) { return; }
                ScalingActive = false;
                SpawnHarvestBonusLoot(__instance.transform.position);
            }
        }

        // The real vanilla mine drop happens in DamageArea (owner-only), not in RPC_SetAreaHealth
        // (which runs on every client). DamageArea is private, so it is patched by name.
        [HarmonyPatch(typeof(MineRock5), "DamageArea")]
        public static class MineRockHarvestScale
        {
            private static void Prefix(HitData hit, out bool __state)
            {
                __state = false;
                if (!ScalingActive && ShouldScaleHarvest(hit))
                {
                    ScalingActive = true;
                    __state = true;
                }
            }

            // __result is DamageArea's return value: true only when the hit area was destroyed and
            // therefore actually dropped, so bonus loot only rolls on a genuine harvest.
            private static void Finalizer(MineRock5 __instance, bool __state, bool __result)
            {
                if (!__state) { return; }
                ScalingActive = false;
                if (__result) { SpawnHarvestBonusLoot(__instance.transform.position); }
            }
        }

        // ---- pickables ------------------------------------------------------
        // Pickable.Drop is called once per dropped unit (stack 1 for the main item, its stack for
        // each extra drop). Rewrite the stack by the multiplier and let vanilla handle placement;
        // a scaled result of 0 cancels the drop, which is how a modifier below 1.0 reduces yield.
        [HarmonyPatch(typeof(Pickable), nameof(Pickable.Drop))]
        public static class ScalePickableDrop
        {
            private static bool Prefix(GameObject prefab, ref int stack)
            {
                if (Player.m_localPlayer == null || !HasResourceModifiers() || prefab == null) { return true; }
                float m = Deathlink.pcfg().GetResouceEarlyCache(prefab);
                if (m == 1f) { return true; }

                int count = ScaleCount(stack, m);
                if (count <= 0) { return false; } // reduced away — drop nothing
                stack = count;                    // vanilla spawns one stack of the scaled amount
                return true;
            }
        }
    }
}
